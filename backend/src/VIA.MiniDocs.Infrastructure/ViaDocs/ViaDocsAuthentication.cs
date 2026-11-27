using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VIA.MiniDocs.Application;
using System.Net;
using System.Net.Http.Headers;

namespace VIA.MiniDocs.Infrastructure.ViaDocs;

internal sealed class ViaDocsAuthentication(IServiceProvider scope, IOptions<ViaDocsOptions> options,
    TimeProvider clock, ViaDocsAuthTransport transport, ViaDocsTokenCache cache, IHttpClientFactory httpClients,
    IServiceScopeFactory scopeFactory) : IViaDocsAuthentication
{
    public async Task<ViaDocsResult<ViaDocsVerifiedIdentity>> LoginAsync(ViaDocsBoundIdentity identity,
        CancellationToken cancellationToken)
    {
        var result = await GetSessionAsync(identity.Prepared, identity.ApplicationId, null, cancellationToken);
        return Map(result, session => new ViaDocsVerifiedIdentity(session.Id, session.Name));
    }

    public async Task<ViaDocsResult<ViaDocsVerifiedIdentity>> FindAsync(ViaDocsPreparedIdentity identity,
        CancellationToken cancellationToken)
    {
        var result = await GetSessionAsync(identity, null, null, cancellationToken);
        return Map(result, session => new ViaDocsVerifiedIdentity(session.Id, session.Name));
    }

    public async Task<ViaDocsResult<ViaDocsRegistrationCandidate>> RedeemAsync(ViaDocsPreparedIdentity identity,
        ViaDocsInvitation invitation, CancellationToken cancellationToken)
    {
        if (invitation.Token is not { Length: 64 } || !invitation.Token.All(Uri.IsHexDigit))
            return new ViaDocsResult<ViaDocsRegistrationCandidate>.Failure(new(ViaDocsFailureKind.LocalRejection));
        var result = await GetSessionAsync(identity, null, invitation.Token, cancellationToken);
        return Map(result, session => new ViaDocsRegistrationCandidate(session.Id, session.Name));
    }

    // The caller supplies an operation-wide deadline and owns the returned response.
    internal async Task<ViaDocsResult<HttpResponseMessage>> SendReadAsync(ViaDocsBoundIdentity identity,
        Func<HttpRequestMessage> createRequest, CancellationToken cancellation, CancellationToken deadline)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                deadline.ThrowIfCancellationRequested();
                var result = await GetSessionAsync(identity.Prepared, identity.ApplicationId, null, deadline);
                if (result is ViaDocsResult<ViaDocsSession>.Failure failure)
                    return new ViaDocsResult<HttpResponseMessage>.Failure(
                        failure.Error.Kind == ViaDocsFailureKind.Cancellation && !cancellation.IsCancellationRequested
                            ? failure.Error with { Kind = ViaDocsFailureKind.Timeout } : failure.Error);

                var session = ((ViaDocsResult<ViaDocsSession>.Success)result).Value;
                var root = options.Value.Root!;
                using var request = createRequest();
                if (request.RequestUri is not { IsAbsoluteUri: true } target ||
                    !target.AbsolutePath.StartsWith(root.AbsolutePath, StringComparison.Ordinal) ||
                    !string.Equals(target.GetLeftPart(UriPartial.Authority), root.GetLeftPart(UriPartial.Authority),
                        StringComparison.OrdinalIgnoreCase) || target.UserInfo.Length != 0 || target.Fragment.Length != 0 ||
                    (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head) || request.Content is not null)
                    return new ViaDocsResult<HttpResponseMessage>.Failure(new(ViaDocsFailureKind.LocalRejection));

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Jwt);
                using var client = httpClients.CreateClient(ViaDocsRegistration.ClientName);
                var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    var key = new ViaDocsTokenCache.Key(root, identity.Prepared.WorkspaceId, identity.ApplicationId, session.Name);
                    cache.Invalidate(key, session);
                    if (attempt == 0)
                    {
                        response.Dispose();
                        continue;
                    }
                }
                if (attempt != 0 || response.StatusCode is not (HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout))
                    return new ViaDocsResult<HttpResponseMessage>.Success(response);
                response.Dispose();
                await DelayRetryAsync(deadline);
            }
            catch (HttpRequestException exception)
            {
                if (attempt == 0 && exception.HttpRequestError == HttpRequestError.ConnectionError && !deadline.IsCancellationRequested)
                {
                    try
                    {
                        await DelayRetryAsync(deadline);
                        continue;
                    }
                    catch (OperationCanceledException)
                    {
                        return ReadCancellation(cancellation);
                    }
                }
                return deadline.IsCancellationRequested ? ReadCancellation(cancellation)
                    : new ViaDocsResult<HttpResponseMessage>.Failure(new(ViaDocsFailureKind.Unavailable));
            }
            catch (OperationCanceledException)
            {
                return ReadCancellation(cancellation);
            }
        }
    }

    private Task DelayRetryAsync(CancellationToken deadline) =>
        Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(15, 51)), clock, deadline);

    internal async Task<ViaDocsResult<HttpResponseMessage>> SendWriteAsync(ViaDocsBoundIdentity identity,
        HttpRequestMessage request, CancellationToken cancellation, CancellationToken deadline)
    {
        var dispatched = false;
        try
        {
            var result = await GetSessionAsync(identity.Prepared, identity.ApplicationId, null, deadline);
            if (result is ViaDocsResult<ViaDocsSession>.Failure failure)
                return new ViaDocsResult<HttpResponseMessage>.Failure(
                    failure.Error.Kind == ViaDocsFailureKind.Cancellation && !cancellation.IsCancellationRequested
                        ? failure.Error with { Kind = ViaDocsFailureKind.Timeout } : failure.Error);

            var session = ((ViaDocsResult<ViaDocsSession>.Success)result).Value;
            var root = options.Value.Root!;
            if (request.RequestUri is not { IsAbsoluteUri: true } target || !root.IsBaseOf(target) ||
                target.Scheme != Uri.UriSchemeHttps || target.UserInfo.Length != 0 || target.Query.Length != 0 ||
                target.Fragment.Length != 0 || (request.Method != HttpMethod.Post && request.Method != HttpMethod.Delete))
                return new ViaDocsResult<HttpResponseMessage>.Failure(new(ViaDocsFailureKind.LocalRejection));

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Jwt);
            request.Version = HttpVersion.Version11;
            request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
            request.Headers.ConnectionClose = true;
            using var client = httpClients.CreateClient(ViaDocsRegistration.WriteClientName);
            deadline.ThrowIfCancellationRequested();
            dispatched = true;
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                cache.Invalidate(new(root, identity.Prepared.WorkspaceId, identity.ApplicationId, session.Name), session);
            return new ViaDocsResult<HttpResponseMessage>.Success(response);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException)
        {
            var cause = cancellation.IsCancellationRequested ? ViaDocsFailureKind.Cancellation
                : deadline.IsCancellationRequested || exception is OperationCanceledException ? ViaDocsFailureKind.Timeout
                : exception is ViaDocsUploadException upload ? upload.Kind : ViaDocsFailureKind.Unavailable;
            if (exception is HttpRequestException { InnerException: ViaDocsUploadException inner } && !deadline.IsCancellationRequested)
                cause = inner.Kind;
            return new ViaDocsResult<HttpResponseMessage>.Failure(dispatched
                ? new(ViaDocsFailureKind.UnknownOutcome, Cause: cause) : new(cause));
        }
    }

    private static ViaDocsResult<HttpResponseMessage> ReadCancellation(CancellationToken cancellation) =>
        new ViaDocsResult<HttpResponseMessage>.Failure(new(cancellation.IsCancellationRequested
            ? ViaDocsFailureKind.Cancellation : ViaDocsFailureKind.Timeout));

    private async Task<ViaDocsResult<ViaDocsSession>> GetSessionAsync(ViaDocsPreparedIdentity identity,
        Guid? application, string? invitation, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
            return Fail(ViaDocsFailureKind.Cancellation);
        var settings = options.Value;
        if (!settings.Enabled || settings.Root is not { } root)
            return Fail(ViaDocsFailureKind.Configuration);
        if (invitation is not null && (!settings.InvitationsEnabled || transport.InvitationUnavailable))
            return Fail(ViaDocsFailureKind.InvitationUnavailable);

        var name = ViaDocsAuthTransport.Normalize(identity.Name);
        if (name is not { Length: >= 3 and <= 160 } || identity.WorkspaceId == Guid.Empty ||
            identity.Credential.Value == Guid.Empty || application == Guid.Empty)
            return Fail(ViaDocsFailureKind.LocalRejection);
        var normalized = identity with { Name = name };
        var provider = scope.GetService<IViaDocsCredentialProvider>();
        if (provider is null)
            return Fail(ViaDocsFailureKind.Configuration);
        var attempt = application is null ? scope.GetService<ProvisioningDispatchContext>()?.Current : null;
        var redeem = invitation is not null;
        var production = provider is IProvisioningDispatchCredentialProvider;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(settings.ShortRequestSeconds), clock);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation, timeout.Token);
        try
        {
            var credential = await ResolveAsync(provider, normalized, application, attempt, redeem, deadline.Token);
            deadline.Token.ThrowIfCancellationRequested();
            if (credential is null || credential.Generation <= 0 ||
                string.IsNullOrWhiteSpace(credential.Secret) || credential.Secret.Length is < 6 or > 4096)
                return Fail(ViaDocsFailureKind.Authentication);

            if (application is not Guid applicationId)
            {
                var unbound = await transport.SendLimitedAsync(root, name, null, credential.Secret, invitation,
                    cancellation, deadline.Token, production
                        ? () => VerifyCredentialAsync(normalized, null, attempt, redeem, credential.Generation,
                            cancellation, deadline.Token) : null);
                if (unbound is not ViaDocsResult<ViaDocsSession>.Success observed)
                    return unbound;
                // After HTTP the fence must still be valid. The pre-dispatch reserve has already served its purpose.
                var verificationAttempt = attempt is null ? null : attempt with { MinimumLeaseSeconds = 0 };
                var failure = await VerifyCredentialAsync(normalized, null, verificationAttempt, redeem,
                    credential.Generation, cancellation, deadline.Token);
                if (failure is null)
                    return unbound;
                return invitation is null ? Fail(failure.Value) : new ViaDocsResult<ViaDocsSession>.Failure(
                    new(ViaDocsFailureKind.UnknownOutcome, 200, Cause: failure),
                    new(observed.Value.Id, observed.Value.Name));
            }

            var key = new ViaDocsTokenCache.Key(root, identity.WorkspaceId, applicationId, name);
            var result = await cache.GetAsync(key, identity.Credential, credential.Generation,
                () => LoginSharedAsync(root, normalized, applicationId, credential, production), deadline.Token);
            if (result is not ViaDocsResult<ViaDocsSession>.Success success)
                return result;

            var verification = await VerifyCredentialAsync(normalized, applicationId, null, false,
                credential.Generation, cancellation, deadline.Token);
            if (verification is not null || success.Value.RenewAt <= clock.GetUtcNow())
            {
                cache.Invalidate(key, success.Value);
                return Fail(verification ?? ViaDocsFailureKind.Authentication);
            }
            return result;
        }
        catch (OperationCanceledException)
        {
            return Fail(cancellation.IsCancellationRequested ? ViaDocsFailureKind.Cancellation : ViaDocsFailureKind.Timeout);
        }
        catch (ViaDocsCredentialUnavailableException)
        {
            return Fail(cancellation.IsCancellationRequested ? ViaDocsFailureKind.Cancellation
                : deadline.IsCancellationRequested ? ViaDocsFailureKind.Timeout : ViaDocsFailureKind.CredentialUnavailable);
        }
    }

    private async Task<ViaDocsResult<ViaDocsSession>> LoginSharedAsync(Uri root, ViaDocsPreparedIdentity identity,
        Guid expected, ViaDocsCredential credential, bool production)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.ShortRequestSeconds), clock);
        var result = await transport.SendLimitedAsync(root, identity.Name, expected, credential.Secret, null,
            CancellationToken.None, timeout.Token, production
                ? () => VerifyCredentialAsync(identity, expected, null, false, credential.Generation,
                    CancellationToken.None, timeout.Token) : null);
        if (result is not ViaDocsResult<ViaDocsSession>.Success)
            return result;

        var failure = await VerifyCredentialAsync(identity, expected, null, false, credential.Generation,
            CancellationToken.None, timeout.Token);
        return failure is null ? result : Fail(failure.Value);
    }

    private static ValueTask<ViaDocsCredential?> ResolveAsync(IViaDocsCredentialProvider provider,
        ViaDocsPreparedIdentity identity, Guid? application, ProvisioningDispatch? attempt, bool redeem,
        CancellationToken token) => provider is IProvisioningDispatchCredentialProvider authorized
            ? authorized.ResolveAuthorizedAsync(identity, application, attempt, redeem, token)
            : attempt is null ? provider.ResolveCurrentAsync(identity, token) : ValueTask.FromResult<ViaDocsCredential?>(null);

    private async Task<ViaDocsFailureKind?> VerifyCredentialAsync(ViaDocsPreparedIdentity identity,
        Guid? application, ProvisioningDispatch? attempt, bool redeem, long generation,
        CancellationToken cancellation, CancellationToken deadline)
    {
        await using var verificationScope = scopeFactory.CreateAsyncScope();
        var provider = verificationScope.ServiceProvider.GetRequiredService<IViaDocsCredentialProvider>();
        try
        {
            deadline.ThrowIfCancellationRequested();
            var current = await ResolveAsync(provider, identity, application, attempt, redeem, deadline);
            deadline.ThrowIfCancellationRequested();
            return current?.Generation == generation && !string.IsNullOrWhiteSpace(current.Secret) &&
                current.Secret.Length is >= 6 and <= 4096 ? null : ViaDocsFailureKind.Authentication;
        }
        catch (OperationCanceledException)
        {
            return cancellation.IsCancellationRequested ? ViaDocsFailureKind.Cancellation : ViaDocsFailureKind.Timeout;
        }
        catch (ViaDocsCredentialUnavailableException)
        {
            return cancellation.IsCancellationRequested ? ViaDocsFailureKind.Cancellation
                : deadline.IsCancellationRequested ? ViaDocsFailureKind.Timeout : ViaDocsFailureKind.CredentialUnavailable;
        }
    }

    private static ViaDocsResult<ViaDocsSession> Fail(ViaDocsFailureKind kind) =>
        new ViaDocsResult<ViaDocsSession>.Failure(new(kind));

    private static ViaDocsResult<T> Map<T>(ViaDocsResult<ViaDocsSession> result, Func<ViaDocsSession, T> select) =>
        result switch
        {
            ViaDocsResult<ViaDocsSession>.Success success => new ViaDocsResult<T>.Success(select(success.Value)),
            ViaDocsResult<ViaDocsSession>.Failure failure => new ViaDocsResult<T>.Failure(failure.Error, failure.Candidate),
            _ => throw new InvalidOperationException("Unexpected VIA.Docs result.")
        };
}
