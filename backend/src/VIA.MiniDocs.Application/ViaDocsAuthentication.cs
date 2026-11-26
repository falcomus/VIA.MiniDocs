namespace VIA.MiniDocs.Application;

public readonly record struct ViaDocsCredentialReference(Guid Value)
{
    public override string ToString() => "[redacted]";
}

public sealed record ViaDocsPreparedIdentity(Guid WorkspaceId, string Name, ViaDocsCredentialReference Credential)
{
    public override string ToString() => "[VIA.Docs identity]";
}

public sealed record ViaDocsBoundIdentity(ViaDocsPreparedIdentity Prepared, Guid ApplicationId);

public sealed class ViaDocsCredential(long generation, string secret)
{
    public long Generation { get; } = generation;
    public string Secret { get; } = secret;
    public override string ToString() => "[redacted]";
}

public sealed class ViaDocsInvitation(string token)
{
    public string Token { get; } = token;
    public override string ToString() => "[redacted]";
}

/// <summary>
/// Resolves the current credential for the exact workspace, normalized name and reference in the request.
/// Return null for invalid or superseded credentials. Each change, including reference replacement, advances
/// the persisted generation for the identity. Never return a superseded generation. Honor cancellation.
/// Fresh scopes must observe the current generation for the same authorized identity.
/// Signal expected source outages with <see cref="ViaDocsCredentialUnavailableException"/>.
/// </summary>
public interface IViaDocsCredentialProvider
{
    ValueTask<ViaDocsCredential?> ResolveCurrentAsync(ViaDocsPreparedIdentity identity, CancellationToken cancellationToken);
}

public sealed class ViaDocsCredentialUnavailableException(Exception? cause = null)
    : Exception("The VIA.Docs credential source is unavailable.", cause);

public enum ViaDocsFailureKind
{
    LocalRejection, Configuration, Authentication, Forbidden, NotVisible, Conflict, Limit,
    RateLimited, Unavailable, Contract, Timeout, Cancellation, UnknownOutcome, InvitationUnavailable,
    CredentialUnavailable
}

public sealed record ViaDocsFailure(ViaDocsFailureKind Kind, int? Status = null,
    TimeSpan? RetryAfter = null, ViaDocsFailureKind? Cause = null);

public abstract record ViaDocsResult<T>
{
    private ViaDocsResult() { }
    public sealed record Success(T Value) : ViaDocsResult<T>;
    public sealed record Failure(ViaDocsFailure Error,
        ViaDocsRegistrationCandidate? Candidate = null) : ViaDocsResult<T>;
}

public sealed record ViaDocsVerifiedIdentity(Guid ApplicationId, string NormalizedName);
public sealed record ViaDocsRegistrationCandidate(Guid ApplicationId, string NormalizedName);

public interface IViaDocsAuthentication
{
    Task<ViaDocsResult<ViaDocsVerifiedIdentity>> LoginAsync(ViaDocsBoundIdentity identity, CancellationToken cancellationToken);
    Task<ViaDocsResult<ViaDocsVerifiedIdentity>> FindAsync(ViaDocsPreparedIdentity identity, CancellationToken cancellationToken);
    Task<ViaDocsResult<ViaDocsRegistrationCandidate>> RedeemAsync(ViaDocsPreparedIdentity identity, ViaDocsInvitation invitation,
        CancellationToken cancellationToken);
}
