using System.Net;
using System.Text.Json;
using VIA.MiniDocs.Application;

namespace VIA.MiniDocs.Infrastructure.ViaDocs;

internal sealed partial class ViaDocsFiles
{
    public async Task<ViaDocsUpload> UploadAsync(ViaDocsBoundIdentity identity, ViaDocsPdfUpload upload,
        CancellationToken cancellationToken)
    {
        if (upload.Length > ViaDocsUploadContent.MaximumBytes)
            return new ViaDocsUpload.Failed(new(ViaDocsFailureKind.Limit));
        if (!upload.Content.CanRead || upload.Length <= 0 || !ValidName(upload.FileName) ||
            !IsValidUtf16(upload.FileName) || upload.FileName.IndexOfAny(['/', '\\', ':']) >= 0 ||
            upload.FileName is "." or "..")
            return new ViaDocsUpload.Failed(new(ViaDocsFailureKind.LocalRejection));
        if (Settings.Root is not { } root || !Settings.Enabled)
            return new ViaDocsUpload.Failed(new(ViaDocsFailureKind.Configuration));

        using var budget = new ViaDocsDeadline(Settings.UploadSeconds, clock, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(root, FilesRoute));
        var multipart = new ViaDocsMultipart();
        request.Content = multipart;
        var file = new ViaDocsUploadContent(upload.Content, upload.Length, Settings.UploadIdleSeconds, clock, budget.Token);
        multipart.Add(file, "file", upload.FileName);
        var result = await authentication.SendWriteAsync(identity, request, cancellationToken, budget.Token);
        if (result is ViaDocsResult<HttpResponseMessage>.Failure failure)
            return new ViaDocsUpload.Failed(failure.Error);
        using var response = ((ViaDocsResult<HttpResponseMessage>.Success)result).Value;
        var status = (int)response.StatusCode;
        if (status != 201)
            return new ViaDocsUpload.Failed(Unknown(response));
        ViaDocsUploadCandidate? candidate = null;
        try
        {
            if (!HasMedia(response, "application/json"))
                return UnknownUpload(status);
            var bytes = await ViaDocsAuthTransport.ReadBoundedAsync(response.Content, Settings.MetadataBytes, budget.Token);
            if (bytes is null)
                return UnknownUpload(status);
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { AllowDuplicateProperties = false });
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                document.RootElement.EnumerateObject().Count(property =>
                    string.Equals(property.Name, "id", StringComparison.OrdinalIgnoreCase)) != 1 ||
                !document.RootElement.TryGetProperty("id", out var identifier) || identifier.ValueKind != JsonValueKind.String ||
                !identifier.TryGetGuid(out var id) || id == Guid.Empty ||
                !ConsistentLocation(response, root, new(id)))
                return UnknownUpload(status);
            long? reported = document.RootElement.TryGetProperty("sizeBytes", out var size) &&
                size.ValueKind == JsonValueKind.Number && size.TryGetInt64(out var length) && length > 0 ? length : null;
            candidate = new(new(id), reported);
            budget.Token.ThrowIfCancellationRequested();
            var wire = document.RootElement.Deserialize<UploadWire>(ViaDocsAuthTransport.JsonOptions);
            return multipart.Completed && wire is not null && wire.Id == id && wire.Kind == "document" &&
                wire.ContentType == "application/pdf" && ValidName(wire.FileName) && Utc(wire.CreatedUtc) &&
                !string.IsNullOrWhiteSpace(wire.PreviewStatus) && reported == file.BytesSent && file.BytesSent > 0
                ? new ViaDocsUpload.Accepted(candidate, file.BytesSent)
                : UnknownUpload(status, candidate);
        }
        catch (Exception exception) when (IsResponseError(exception))
        {
            return UnknownUpload(status, candidate, ReadFailure(exception, budget));
        }
    }

    public async Task<ViaDocsResult<ViaDocsDeletion>> DeleteAsync(ViaDocsBoundIdentity identity, ViaDocsFileId id,
        CancellationToken cancellationToken)
    {
        if (id.Value == Guid.Empty)
            return Error<ViaDocsDeletion>(ViaDocsFailureKind.LocalRejection);
        if (Settings.Root is not { } root || !Settings.Enabled)
            return Error<ViaDocsDeletion>(ViaDocsFailureKind.Configuration);

        using var budget = new ViaDocsDeadline(Settings.ShortRequestSeconds, clock, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(root, Route(id)));
        var result = await authentication.SendWriteAsync(identity, request, cancellationToken, budget.Token);
        if (result is ViaDocsResult<HttpResponseMessage>.Failure failure)
            return new ViaDocsResult<ViaDocsDeletion>.Failure(failure.Error);
        using var response = ((ViaDocsResult<HttpResponseMessage>.Success)result).Value;
        if (budget.Token.IsCancellationRequested)
            return new ViaDocsResult<ViaDocsDeletion>.Failure(new(ViaDocsFailureKind.UnknownOutcome, (int)response.StatusCode,
                Cause: budget.CancellationKind));
        if (response.StatusCode == HttpStatusCode.NotFound)
            return Success(ViaDocsDeletion.NotVisibleOrMissing);
        if (response.StatusCode == HttpStatusCode.NoContent && response.Content.Headers.ContentLength is null or 0 &&
            !response.Content.Headers.Contains("Content-Range") && response.Headers.TransferEncoding.Count == 0)
            return Success(ViaDocsDeletion.LogicalDeletionConfirmed);
        return new ViaDocsResult<ViaDocsDeletion>.Failure(Unknown(response));
    }

    private static bool ConsistentLocation(HttpResponseMessage response, Uri root, ViaDocsFileId id)
    {
        if (!response.Headers.TryGetValues("Location", out var values))
            return true;
        var items = values.Take(2).ToArray();
        if (items.Length != 1)
            return false;
        var value = items[0];
        // VIA.Docs emits this canonical root-relative value even when a proxy adds a prefix.
        if (value == "/" + Route(id))
            return true;
        var expected = new Uri(root, Route(id));
        return Uri.TryCreate(value, UriKind.Absolute, out var location) &&
            location.UserInfo.Length == 0 && location.Query.Length == 0 && location.Fragment.Length == 0 &&
            string.Equals(location.AbsoluteUri, expected.AbsoluteUri, StringComparison.Ordinal);
    }

    private ViaDocsFailure Unknown(HttpResponseMessage response)
    {
        var description = transport.Describe(response);
        return description with { Kind = ViaDocsFailureKind.UnknownOutcome, Cause = description.Kind };
    }

    private static ViaDocsUpload.Failed UnknownUpload(int status, ViaDocsUploadCandidate? candidate = null,
        ViaDocsFailureKind cause = ViaDocsFailureKind.Contract) =>
        new(new(ViaDocsFailureKind.UnknownOutcome, status, Cause: cause), candidate);
}
