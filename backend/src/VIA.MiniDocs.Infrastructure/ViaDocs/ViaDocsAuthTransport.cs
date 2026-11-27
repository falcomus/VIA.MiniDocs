using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VIA.MiniDocs.Application;

namespace VIA.MiniDocs.Infrastructure.ViaDocs;

internal sealed class ViaDocsAuthTransport(IHttpClientFactory factory, IOptions<ViaDocsOptions> options, TimeProvider clock)
{
    private sealed record LoginRequest(string Name, string Secret)
    {
        public override string ToString() => "[redacted]";
    }
    private sealed record InvitationRequest(string Token, string Name, string Secret)
    {
        public override string ToString() => "[redacted]";
    }
    private sealed record AuthResponse(Guid ApplicationId, string? Name, string? AccessToken, DateTimeOffset ExpiresUtc)
    {
        public override string ToString() => "[redacted]";
    }
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        AllowDuplicateProperties = false
    };
    private ViaDocsOptions Settings => options.Value;
    private int invitationUnavailable;
    private int active;
    internal bool InvitationUnavailable => Volatile.Read(ref invitationUnavailable) != 0;

    internal async Task<ViaDocsResult<ViaDocsSession>> SendLimitedAsync(Uri root, string name, Guid? expected,
        string secret, string? invitation, CancellationToken cancellation, CancellationToken deadline,
        Func<Task<ViaDocsFailureKind?>>? authorize = null)
    {
        if (Interlocked.Increment(ref active) > Settings.ConcurrentLogins)
        {
            Interlocked.Decrement(ref active);
            return Fail(ViaDocsFailureKind.Limit, false);
        }
        try
        {
            if (invitation is not null && InvitationUnavailable)
                return Fail(ViaDocsFailureKind.InvitationUnavailable, false);
            return await SendAsync(root, name, expected, secret, invitation, cancellation, deadline, authorize);
        }
        finally
        {
            Interlocked.Decrement(ref active);
        }
    }

    private async Task<ViaDocsResult<ViaDocsSession>> SendAsync(Uri root, string name, Guid? expected,
        string secret, string? invitation, CancellationToken cancellation, CancellationToken deadline,
        Func<Task<ViaDocsFailureKind?>>? authorize)
    {
        var dispatched = false;
        try
        {
            deadline.ThrowIfCancellationRequested();
            using var client = factory.CreateClient(ViaDocsRegistration.AuthClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(root, invitation is null
                ? "api/v1/applications/login" : "api/v1/applications/register-invitation"));
            request.Version = HttpVersion.Version11;
            request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
            // A fresh HTTP/1.1 exchange avoids stale pooled connections and HTTP/2 replay signals.
            request.Headers.ConnectionClose = true;
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var body = invitation is null
                ? JsonSerializer.SerializeToUtf8Bytes(new LoginRequest(name, secret), JsonOptions)
                : JsonSerializer.SerializeToUtf8Bytes(new InvitationRequest(invitation, name, secret), JsonOptions);
            request.Content = SingleSendContent.Json(body);
            if (authorize is not null && await authorize() is { } rejection)
                return Fail(rejection, false);
            deadline.ThrowIfCancellationRequested();
            dispatched = true;
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline);
            var status = (int)response.StatusCode;
            if (status != 200)
            {
                var failure = Classify(status, RetryHint(response));
                // A 404 can come from a proxy, so preserve uncertainty on the write.
                if (invitation is not null && status is 404 or 405)
                    Volatile.Write(ref invitationUnavailable, 1);
                // The status and payload do not authenticate the replying endpoint behind the proxy.
                if (invitation is not null)
                    failure = failure with { Kind = ViaDocsFailureKind.UnknownOutcome,
                        Cause = status is 404 or 405 ? ViaDocsFailureKind.InvitationUnavailable : failure.Kind };
                return new ViaDocsResult<ViaDocsSession>.Failure(failure);
            }

            if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
                return Fail(ViaDocsFailureKind.Contract, dispatched && invitation is not null, 200);

            var bytes = await ReadBoundedAsync(response.Content, Settings.AuthBytes, deadline);
            if (bytes is null)
                return Fail(ViaDocsFailureKind.Contract, dispatched && invitation is not null, 200);

            var dto = JsonSerializer.Deserialize<AuthResponse>(bytes.AsSpan(), JsonOptions);
            deadline.ThrowIfCancellationRequested();
            if (dto is null || dto.ApplicationId == Guid.Empty || (expected.HasValue && dto.ApplicationId != expected) ||
                !string.Equals(Normalize(dto.Name), name, StringComparison.Ordinal))
                return Fail(ViaDocsFailureKind.Contract, invitation is not null, 200);

            var renewAt = GetRenewal(dto);
            if (renewAt is null)
                return Fail(ViaDocsFailureKind.Contract, invitation is not null, 200,
                    invitation is null ? null : new ViaDocsRegistrationCandidate(dto.ApplicationId, name));

            deadline.ThrowIfCancellationRequested();
            return new ViaDocsResult<ViaDocsSession>.Success(new(dto.ApplicationId, name, dto.AccessToken!, renewAt.Value));
        }
        catch (OperationCanceledException)
        {
            var kind = cancellation.IsCancellationRequested ? ViaDocsFailureKind.Cancellation : ViaDocsFailureKind.Timeout;
            return Fail(kind, dispatched && invitation is not null);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            var kind = deadline.IsCancellationRequested
                ? cancellation.IsCancellationRequested ? ViaDocsFailureKind.Cancellation : ViaDocsFailureKind.Timeout
                : ViaDocsFailureKind.Unavailable;
            return Fail(kind, dispatched && invitation is not null);
        }
        catch (JsonException)
        {
            return Fail(ViaDocsFailureKind.Contract, dispatched && invitation is not null, 200);
        }
    }

    private DateTimeOffset? GetRenewal(AuthResponse dto)
    {
        var parts = dto.AccessToken?.Split('.');
        if (parts is not { Length: 3 } || parts.Any(part => part.Length == 0 ||
            part.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_'))))
            return null;
        try
        {
            using var header = JsonDocument.Parse(Decode(parts[0]));
            if (header.RootElement.ValueKind != JsonValueKind.Object ||
                !header.RootElement.TryGetProperty("alg", out var algorithm) || algorithm.GetString() != "HS256" ||
                Decode(parts[2]).Length != 32)
                return null;

            using var payload = JsonDocument.Parse(Decode(parts[1]));
            var claims = payload.RootElement;
            if (claims.ValueKind != JsonValueKind.Object ||
                claims.EnumerateObject().Count(item => item.Name is "sub" or "exp") != 2 ||
                !claims.TryGetProperty("sub", out var sub) || sub.ValueKind != JsonValueKind.String ||
                !Guid.TryParse(sub.GetString(), out var id) || id != dto.ApplicationId ||
                !claims.TryGetProperty("exp", out var exp) || exp.ValueKind != JsonValueKind.Number ||
                !exp.TryGetInt64(out var seconds) || dto.ExpiresUtc.ToUnixTimeSeconds() != seconds)
                return null;
            var renewAt = DateTimeOffset.FromUnixTimeSeconds(seconds).AddSeconds(-Settings.RenewalSeconds);
            return renewAt > clock.GetUtcNow() ? renewAt : null;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return null;
        }
    }

    private static byte[] Decode(string part) => Convert.FromBase64String(
        part.Replace('-', '+').Replace('_', '/').PadRight((part.Length + 3) / 4 * 4, '='));

    internal static string? Normalize(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim().ToLowerInvariant();

    internal static async Task<byte[]?> ReadBoundedAsync(HttpContent content, int limit, CancellationToken cancellation)
    {
        if (content.Headers.ContentLength > limit)
            return null;
        await using var stream = await content.ReadAsStreamAsync(cancellation);
        var buffer = new byte[limit + 1];
        var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, false, cancellation);
        return read > limit ? null : buffer.AsSpan(0, read).ToArray();
    }

    internal ViaDocsFailure Describe(HttpResponseMessage response) => Classify((int)response.StatusCode, RetryHint(response));

    private TimeSpan? RetryHint(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Retry-After", out var values))
            return null;
        var headers = values.Take(2).ToArray();
        if (headers.Length != 1 || !RetryConditionHeaderValue.TryParse(headers[0], out var value))
            return null;
        var delay = value.Delta ?? value.Date!.Value - clock.GetUtcNow();
        return delay < TimeSpan.Zero ? null : TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds, 300));
    }

    private static ViaDocsFailure Classify(int status, TimeSpan? retry) => new(status switch
    {
        400 or 415 => ViaDocsFailureKind.LocalRejection,
        401 => ViaDocsFailureKind.Authentication,
        403 => ViaDocsFailureKind.Forbidden,
        404 => ViaDocsFailureKind.NotVisible,
        409 => ViaDocsFailureKind.Conflict,
        413 => ViaDocsFailureKind.Limit,
        429 => ViaDocsFailureKind.RateLimited,
        >= 500 => ViaDocsFailureKind.Unavailable,
        _ => ViaDocsFailureKind.Contract
    }, status, retry);

    private static ViaDocsResult<ViaDocsSession> Fail(ViaDocsFailureKind kind, bool unknown, int? status = null,
        ViaDocsRegistrationCandidate? candidate = null) =>
        new ViaDocsResult<ViaDocsSession>.Failure(unknown
            ? new(ViaDocsFailureKind.UnknownOutcome, status, Cause: kind) : new(kind, status), candidate);

    private sealed class SingleSendContent(byte[] body) : HttpContent
    {
        private int serialized;

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref serialized, 1) != 0)
                throw new IOException("VIA.Docs request content cannot be sent twice.");
            return stream.WriteAsync(body.AsMemory(), cancellationToken).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = body.Length;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(body);
            base.Dispose(disposing);
        }

        internal static SingleSendContent Json(byte[] bytes)
        {
            var content = new SingleSendContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return content;
        }
    }
}
