using System.Net;
using System.Net.Http.Headers;
using VIA.MiniDocs.Application;

namespace VIA.MiniDocs.Infrastructure.ViaDocs;

internal sealed partial class ViaDocsFiles
{
    public async Task<ViaDocsResult<ViaDocsOriginal>> GetContentAsync(ViaDocsBoundIdentity identity, ViaDocsFileId id,
        bool head, ViaDocsByteRange? range, ViaDocsFileValidator? validator, CancellationToken cancellationToken)
    {
        if (id.Value == Guid.Empty || !ValidValidator(identity, id, null, validator) || !TryRange(range, out var header))
            return Error<ViaDocsOriginal>(ViaDocsFailureKind.LocalRejection);

        using var headers = new ViaDocsDeadline(Settings.DownloadHeadersSeconds, clock, cancellationToken);
        var budget = new ViaDocsDeadline(Settings.DownloadSeconds, clock, cancellationToken);
        HttpResponseMessage? response = null;
        var transferred = false;
        try
        {
            var result = await ReadAsync(identity, Route(id) + "/content", head ? HttpMethod.Head : HttpMethod.Get,
                header, validator, headers.Token, cancellationToken);
            if (result is ViaDocsResult<HttpResponseMessage>.Failure failure)
                return new ViaDocsResult<ViaDocsOriginal>.Failure(failure.Error);
            response = ((ViaDocsResult<HttpResponseMessage>.Success)result).Value;
            headers.Token.ThrowIfCancellationRequested();
            var status = (int)response.StatusCode;
            if (status == 304)
                return Matches(response, validator) ? Success<ViaDocsOriginal>(new ViaDocsOriginal.NotModified(validator!))
                    : Error<ViaDocsOriginal>(ViaDocsFailureKind.Contract, status);
            var contentRange = response.Content.Headers.ContentRange;
            if (status == 416)
                return range is not null && contentRange is { Unit: "bytes", HasRange: false, Length: > 0 } &&
                    !TryResolve(range, contentRange.Length.Value, out _, out _)
                    ? Success<ViaDocsOriginal>(new ViaDocsOriginal.RangeUnsatisfiable(contentRange.Length.Value))
                    : Error<ViaDocsOriginal>(ViaDocsFailureKind.Contract, status);
            if (status is not (200 or 206))
                return ResponseFailure<ViaDocsOriginal>(response);
            if (!HasMedia(response, "application/pdf") || !IdentityEncoding(response) ||
                response.Headers.ETag is not { IsWeak: false } tag ||
                response.Content.Headers.ContentLength is not > 0)
                return Error<ViaDocsOriginal>(ViaDocsFailureKind.Contract, status);
            var length = response.Content.Headers.ContentLength.Value;
            ViaDocsContentRange? partial = null;
            if (status == 206)
            {
                if (range is null || contentRange is not { Unit: "bytes", From: { } from, To: { } to, Length: > 0 } ||
                    !TryResolve(range, contentRange.Length.Value, out var expectedFrom, out var expectedTo) ||
                    from != expectedFrom || to != expectedTo || length != to - from + 1)
                    return Error<ViaDocsOriginal>(ViaDocsFailureKind.Contract, status);
                partial = new(from, to, contentRange.Length.Value);
            }
            else if (response.Content.Headers.Contains("Content-Range"))
                return Error<ViaDocsOriginal>(ViaDocsFailureKind.Contract, status);

            var metadata = new ViaDocsContentHeaders(ViaDocsFileMedia.Pdf, length, new(identity, id, null, tag.Tag));
            IViaDocsBody? body = null;
            if (!head)
            {
                var stream = await response.Content.ReadAsStreamAsync(headers.Token);
                headers.Token.ThrowIfCancellationRequested();
                body = new ViaDocsResponseBody(response, stream, budget, clock, Settings.DownloadIdleSeconds, length, length);
                transferred = true;
            }
            return Success<ViaDocsOriginal>(partial is { } resolved
                ? new ViaDocsOriginal.Partial(metadata, resolved, body) : new ViaDocsOriginal.Full(metadata, body));
        }
        catch (Exception exception) when (IsResponseError(exception))
        {
            return Error<ViaDocsOriginal>(ReadFailure(exception, headers), response is null ? null : (int)response.StatusCode);
        }
        finally
        {
            if (!transferred)
            {
                response?.Dispose();
                budget.Dispose();
            }
        }
    }

    public async Task<ViaDocsResult<ViaDocsPreview>> GetPreviewAsync(ViaDocsBoundIdentity identity, ViaDocsFileId id,
        int size, ViaDocsFileValidator? validator, CancellationToken cancellationToken)
    {
        if (id.Value == Guid.Empty || size is < 1 or > 4096 || !ValidValidator(identity, id, size, validator))
            return Error<ViaDocsPreview>(ViaDocsFailureKind.LocalRejection);

        using var headers = new ViaDocsDeadline(Settings.DownloadHeadersSeconds, clock, cancellationToken);
        var budget = new ViaDocsDeadline(Settings.DownloadSeconds, clock, cancellationToken);
        HttpResponseMessage? response = null;
        var transferred = false;
        try
        {
            var result = await ReadAsync(identity, FormattableString.Invariant($"{Route(id)}/preview?size={size}"),
                HttpMethod.Get, null, validator, headers.Token, cancellationToken);
            if (result is ViaDocsResult<HttpResponseMessage>.Failure failure)
                return new ViaDocsResult<ViaDocsPreview>.Failure(failure.Error);
            response = ((ViaDocsResult<HttpResponseMessage>.Success)result).Value;
            headers.Token.ThrowIfCancellationRequested();
            var status = (int)response.StatusCode;
            if (status == 304)
                return Matches(response, validator) ? Success<ViaDocsPreview>(new ViaDocsPreview.NotModified(validator!))
                    : Error<ViaDocsPreview>(ViaDocsFailureKind.Contract, status);
            if (status != 200)
                return ResponseFailure<ViaDocsPreview>(response);
            if (response.Content.Headers.Contains("Content-Range") || !IdentityEncoding(response))
                return Error<ViaDocsPreview>(ViaDocsFailureKind.Contract, status);
            if (response.Content.Headers.ContentLength > Settings.PreviewBytes)
                return Error<ViaDocsPreview>(ViaDocsFailureKind.Limit, status);
            if (HasMedia(response, "image/svg+xml"))
                return Success<ViaDocsPreview>(new ViaDocsPreview.Fallback());
            if (!HasMedia(response, "image/png") || response.Content.Headers.ContentLength == 0 ||
                response.Headers.ETag is not { IsWeak: false } tag)
                return Error<ViaDocsPreview>(ViaDocsFailureKind.Contract, status);
            var stream = await response.Content.ReadAsStreamAsync(headers.Token);
            headers.Token.ThrowIfCancellationRequested();
            var length = response.Content.Headers.ContentLength;
            var body = new ViaDocsResponseBody(response, stream, budget, clock, Settings.DownloadIdleSeconds, Settings.PreviewBytes, length);
            transferred = true;
            return Success<ViaDocsPreview>(new ViaDocsPreview.Rendered(
                new(ViaDocsFileMedia.Png, length, new(identity, id, size, tag.Tag)), body));
        }
        catch (Exception exception) when (IsResponseError(exception))
        {
            return Error<ViaDocsPreview>(ReadFailure(exception, headers), response is null ? null : (int)response.StatusCode);
        }
        finally
        {
            if (!transferred)
            {
                response?.Dispose();
                budget.Dispose();
            }
        }
    }

    private static bool ValidValidator(ViaDocsBoundIdentity identity, ViaDocsFileId id, int? size, ViaDocsFileValidator? validator) =>
        validator is null || (validator.Identity == identity && validator.Id == id && validator.PreviewSize == size &&
            validator.EntityTag.Length <= 128 && EntityTagHeaderValue.TryParse(validator.EntityTag, out var tag) &&
            !tag.IsWeak && tag != EntityTagHeaderValue.Any);

    private static bool Matches(HttpResponseMessage response, ViaDocsFileValidator? validator) =>
        validator is not null && response.Headers.ETag is { IsWeak: false } tag && tag.Tag == validator.EntityTag &&
        !response.Content.Headers.Contains("Content-Range");

    private static bool IdentityEncoding(HttpResponseMessage response) => response.Content.Headers.ContentEncoding.Count == 0;

    private static bool TryRange(ViaDocsByteRange? range, out RangeHeaderValue? header)
    {
        header = range switch
        {
            ViaDocsByteRange.Closed closed when closed.First >= 0 && closed.Last >= closed.First => new(closed.First, closed.Last),
            ViaDocsByteRange.Open open when open.First >= 0 => new(open.First, null),
            ViaDocsByteRange.Suffix suffix when suffix.Length > 0 => new(null, suffix.Length),
            _ => null
        };
        return range is null || header is not null;
    }

    private static bool TryResolve(ViaDocsByteRange range, long total, out long first, out long last)
    {
        first = range switch
        {
            ViaDocsByteRange.Closed closed => closed.First,
            ViaDocsByteRange.Open open => open.First,
            ViaDocsByteRange.Suffix suffix => Math.Max(0, total - suffix.Length),
            _ => total
        };
        last = range is ViaDocsByteRange.Closed end ? Math.Min(end.Last, total - 1) : total - 1;
        return first < total && first <= last;
    }

    private static ViaDocsResult<T>.Success Success<T>(T value) => new(value);
}
