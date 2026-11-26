using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VIA.MiniDocs.Application;

namespace VIA.MiniDocs.Infrastructure.ViaDocs;

internal sealed partial class ViaDocsFiles(ViaDocsAuthentication authentication, ViaDocsAuthTransport transport,
    IOptions<ViaDocsOptions> options, TimeProvider clock) : IViaDocsFiles
{
    private ViaDocsOptions Settings => options.Value;
    private const string FilesRoute = "api/v1/files";

    private sealed record MetadataWire(Guid Id, string? Kind, string? FileName, string? ContentType, long SizeBytes,
        string? Sha256, DateTimeOffset CreatedUtc, DateTimeOffset UpdatedUtc, string? PreviewStatus, string? ScanStatus,
        int? PageCount);
    private sealed record PageWire(MetadataWire?[]? Files, string? NextCursor);
    private sealed record UploadWire(Guid Id, string? Kind, string? FileName, string? ContentType, long SizeBytes,
        DateTimeOffset CreatedUtc, string? PreviewStatus);

    public Task<ViaDocsResult<ViaDocsFilePage>> ListAsync(ViaDocsBoundIdentity identity, ViaDocsPageRequest page,
        CancellationToken cancellationToken)
    {
        if (page.Size is < 1 or > 100 || !ValidCursor(page.Cursor))
            return Local<ViaDocsFilePage>();
        var route = FilesRoute + "?kind=document&pageSize=" + page.Size.ToString(CultureInfo.InvariantCulture);
        if (page.Cursor is { } cursor)
            route += "&cursor=" + Uri.EscapeDataString(cursor);
        return ReadJsonAsync<PageWire, ViaDocsFilePage>(identity, route, wire => MapPage(wire, page), cancellationToken);
    }

    public Task<ViaDocsResult<ViaDocsFileMetadata>> GetMetadataAsync(ViaDocsBoundIdentity identity, ViaDocsFileId id,
        CancellationToken cancellationToken) => id.Value == Guid.Empty ? Local<ViaDocsFileMetadata>()
        : ReadJsonAsync<MetadataWire, ViaDocsFileMetadata>(identity, Route(id),
            wire => wire.Id == id.Value ? MapMetadata(wire) : null, cancellationToken);

    private async Task<ViaDocsResult<T>> ReadJsonAsync<TWire, T>(ViaDocsBoundIdentity identity, string route,
        Func<TWire, T?> map, CancellationToken cancellation) where TWire : class where T : class
    {
        using var budget = new ViaDocsDeadline(Settings.ShortRequestSeconds, clock, cancellation);
        var result = await ReadAsync(identity, route, HttpMethod.Get, null, null, budget.Token, cancellation);
        if (result is ViaDocsResult<HttpResponseMessage>.Failure failure)
            return new ViaDocsResult<T>.Failure(failure.Error);
        using var response = ((ViaDocsResult<HttpResponseMessage>.Success)result).Value;
        if (response.StatusCode != HttpStatusCode.OK)
            return ResponseFailure<T>(response);
        try
        {
            var wire = await ReadJsonAsync<TWire>(response, budget.Token);
            budget.Token.ThrowIfCancellationRequested();
            return wire is not null && map(wire) is { } mapped
                ? new ViaDocsResult<T>.Success(mapped) : Error<T>(ViaDocsFailureKind.Contract, 200);
        }
        catch (Exception exception) when (IsResponseError(exception))
        {
            return Error<T>(ReadFailure(exception, budget), 200);
        }
    }

    private async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellation) where T : class
    {
        if (!HasMedia(response, "application/json"))
            return null;
        var bytes = await ViaDocsAuthTransport.ReadBoundedAsync(response.Content, Settings.MetadataBytes, cancellation);
        return bytes is null ? null : JsonSerializer.Deserialize<T>(bytes, ViaDocsAuthTransport.JsonOptions);
    }

    private async Task<ViaDocsResult<HttpResponseMessage>> ReadAsync(ViaDocsBoundIdentity identity, string route,
        HttpMethod method, RangeHeaderValue? range, ViaDocsFileValidator? validator, CancellationToken deadline,
        CancellationToken cancellation)
    {
        if (Settings.Root is not { } root || !Settings.Enabled)
            return Error<HttpResponseMessage>(ViaDocsFailureKind.Configuration);
        return await authentication.SendReadAsync(identity, () =>
        {
            var request = new HttpRequestMessage(method, new Uri(root, route));
            request.Headers.Range = range;
            if (validator is not null)
                request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(validator.EntityTag));
            return request;
        }, cancellation, deadline);
    }

    private static ViaDocsFilePage? MapPage(PageWire wire, ViaDocsPageRequest request)
    {
        if (wire.Files is null || wire.Files.Length > request.Size || !ValidCursor(wire.NextCursor) ||
            (wire.NextCursor is not null && string.Equals(wire.NextCursor, request.Cursor, StringComparison.Ordinal)))
            return null;
        var files = new List<ViaDocsFileMetadata>(wire.Files.Length);
        var seen = new HashSet<ViaDocsFileId>();
        foreach (var item in wire.Files)
        {
            if (item is null || MapMetadata(item) is not { } metadata || !seen.Add(metadata.Id))
                return null;
            files.Add(metadata);
        }
        return new(files.AsReadOnly(), wire.NextCursor);
    }

    private static ViaDocsFileMetadata? MapMetadata(MetadataWire wire)
    {
        if (wire.Id == Guid.Empty || wire.Kind != "document" || !ValidName(wire.FileName) ||
            wire.ContentType != "application/pdf" || wire.SizeBytes <= 0 ||
            wire.Sha256 is not { Length: 64 } || !wire.Sha256.All(Uri.IsHexDigit) ||
            !Utc(wire.CreatedUtc) || !Utc(wire.UpdatedUtc) || wire.UpdatedUtc < wire.CreatedUtc ||
            string.IsNullOrWhiteSpace(wire.PreviewStatus) || wire.PageCount <= 0)
            return null;
        return new(new(wire.Id), wire.FileName!, wire.SizeBytes, wire.Sha256.ToLowerInvariant(), wire.CreatedUtc,
            wire.UpdatedUtc, ReadEnum<ViaDocsPreviewState>(wire.PreviewStatus),
            wire.ScanStatus is null ? null : ReadEnum<ViaDocsScanState>(wire.ScanStatus), wire.PageCount);
    }

    private static T ReadEnum<T>(string value) where T : struct, Enum =>
        Enum.GetNames<T>().Contains(value, StringComparer.Ordinal) ? Enum.Parse<T>(value) : default;

    private static bool Utc(DateTimeOffset value) => value != default && value.Offset == TimeSpan.Zero;
    private static bool ValidName(string? value) => value is { Length: > 0 and <= 255 } &&
        !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
    private static bool ValidCursor(string? value) => value is null ||
        (value.Length is > 0 and <= 128 && !value.Any(char.IsControl) && IsValidUtf16(value));

    private static bool IsValidUtf16(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (!char.IsSurrogate(value[index]))
                continue;
            if (index + 1 == value.Length || !char.IsSurrogatePair(value[index], value[index + 1]))
                return false;
            index++;
        }
        return true;
    }

    private static string Route(ViaDocsFileId id) => FilesRoute + "/" + id.Value.ToString("D");
    private static bool HasMedia(HttpResponseMessage response, string media) =>
        string.Equals(response.Content.Headers.ContentType?.MediaType, media, StringComparison.OrdinalIgnoreCase);
    private static ViaDocsResult<T>.Failure Error<T>(ViaDocsFailureKind kind, int? status = null) => new(new(kind, status));
    private static Task<ViaDocsResult<T>> Local<T>() => Task.FromResult<ViaDocsResult<T>>(Error<T>(ViaDocsFailureKind.LocalRejection));
    private ViaDocsResult<T>.Failure ResponseFailure<T>(HttpResponseMessage response) => new(transport.Describe(response));
    private static bool IsResponseError(Exception exception) =>
        exception is IOException or HttpRequestException or OperationCanceledException or JsonException or FormatException;
    private static ViaDocsFailureKind ReadFailure(Exception exception, ViaDocsDeadline budget) =>
        budget.Token.IsCancellationRequested || exception is OperationCanceledException ? budget.CancellationKind
        : exception is JsonException or FormatException ? ViaDocsFailureKind.Contract : ViaDocsFailureKind.Unavailable;
}
