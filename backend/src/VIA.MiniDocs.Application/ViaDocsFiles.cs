namespace VIA.MiniDocs.Application;

public readonly record struct ViaDocsFileId(Guid Value)
{
    public override string ToString() => "[VIA.Docs file]";
}

public sealed record ViaDocsPageRequest(int Size = 50, string? Cursor = null)
{
    public override string ToString() => "[VIA.Docs page request]";
}

public enum ViaDocsPreviewState { Unknown, Pending, Ready, Failed, Unsupported }
public enum ViaDocsScanState { Unknown, NotScanned, Clean, Blocked }
public enum ViaDocsFileMedia { Pdf, Png }

public sealed record ViaDocsFileMetadata(ViaDocsFileId Id, string FileName, long Size, string Sha256,
    DateTimeOffset CreatedUtc, DateTimeOffset UpdatedUtc, ViaDocsPreviewState PreviewState, ViaDocsScanState? ScanState,
    int? PageCount)
{
    public override string ToString() => "[VIA.Docs file metadata]";
}

public sealed record ViaDocsFilePage(IReadOnlyList<ViaDocsFileMetadata> Files, string? NextCursor)
{
    public override string ToString() => "[VIA.Docs file page]";
}

public abstract record ViaDocsByteRange
{
    private ViaDocsByteRange() { }
    public sealed record Closed(long First, long Last) : ViaDocsByteRange;
    public sealed record Open(long First) : ViaDocsByteRange;
    public sealed record Suffix(long Length) : ViaDocsByteRange;
}

public sealed class ViaDocsFileValidator(ViaDocsBoundIdentity identity, ViaDocsFileId id, int? previewSize, string entityTag)
{
    public ViaDocsBoundIdentity Identity { get; } = identity;
    public ViaDocsFileId Id { get; } = id;
    public int? PreviewSize { get; } = previewSize;
    public string EntityTag { get; } = entityTag;
    public override string ToString() => "[VIA.Docs validator]";
}

public readonly record struct ViaDocsContentRange(long First, long Last, long Total);
public sealed record ViaDocsContentHeaders(ViaDocsFileMedia Media, long? Length, ViaDocsFileValidator Validator);

/// <summary>The operation result owns the asynchronous stream. Dispose even when no body is read.</summary>
public interface IViaDocsBody : IAsyncDisposable
{
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
}

public sealed class ViaDocsBodyException(ViaDocsFailureKind kind)
    : IOException("VIA.Docs response body could not be read.")
{
    public ViaDocsFailureKind Kind { get; } = kind;
}

public abstract record ViaDocsOriginal : IAsyncDisposable
{
    private ViaDocsOriginal() { }
    public sealed record Full(ViaDocsContentHeaders Headers, IViaDocsBody? Body) : ViaDocsOriginal;
    public sealed record Partial(ViaDocsContentHeaders Headers, ViaDocsContentRange Range, IViaDocsBody? Body) : ViaDocsOriginal;
    public sealed record NotModified(ViaDocsFileValidator Validator) : ViaDocsOriginal;
    public sealed record RangeUnsatisfiable(long Total) : ViaDocsOriginal;

    public ValueTask DisposeAsync() => this switch
    {
        Full { Body: { } body } => body.DisposeAsync(),
        Partial { Body: { } body } => body.DisposeAsync(),
        _ => ValueTask.CompletedTask
    };
}

public abstract record ViaDocsPreview : IAsyncDisposable
{
    private ViaDocsPreview() { }
    public sealed record Rendered(ViaDocsContentHeaders Headers, IViaDocsBody Body) : ViaDocsPreview;
    public sealed record Fallback : ViaDocsPreview;
    public sealed record NotModified(ViaDocsFileValidator Validator) : ViaDocsPreview;

    public ValueTask DisposeAsync() => this is Rendered image ? image.Body.DisposeAsync() : ValueTask.CompletedTask;
}

public sealed record ViaDocsPdfUpload(Stream Content, string FileName, long? Length = null)
{
    public override string ToString() => "[VIA.Docs PDF upload]";
}

public sealed record ViaDocsUploadCandidate(ViaDocsFileId Id, long? ReportedSize);
public abstract record ViaDocsUpload
{
    private ViaDocsUpload() { }
    public sealed record Accepted(ViaDocsUploadCandidate Candidate, long BytesSent) : ViaDocsUpload;
    public sealed record Failed(ViaDocsFailure Error, ViaDocsUploadCandidate? Candidate = null) : ViaDocsUpload;
}

public enum ViaDocsDeletion { LogicalDeletionConfirmed, NotVisibleOrMissing }

public interface IViaDocsFiles
{
    Task<ViaDocsResult<ViaDocsFilePage>> ListAsync(ViaDocsBoundIdentity identity, ViaDocsPageRequest page, CancellationToken cancellationToken);
    Task<ViaDocsResult<ViaDocsFileMetadata>> GetMetadataAsync(ViaDocsBoundIdentity identity, ViaDocsFileId id, CancellationToken cancellationToken);
    Task<ViaDocsResult<ViaDocsOriginal>> GetContentAsync(ViaDocsBoundIdentity identity, ViaDocsFileId id, bool head,
        ViaDocsByteRange? range, ViaDocsFileValidator? validator, CancellationToken cancellationToken);
    Task<ViaDocsResult<ViaDocsPreview>> GetPreviewAsync(ViaDocsBoundIdentity identity, ViaDocsFileId id, int size = 256,
        ViaDocsFileValidator? validator = null, CancellationToken cancellationToken = default);
    Task<ViaDocsUpload> UploadAsync(ViaDocsBoundIdentity identity, ViaDocsPdfUpload upload, CancellationToken cancellationToken);
    Task<ViaDocsResult<ViaDocsDeletion>> DeleteAsync(ViaDocsBoundIdentity identity, ViaDocsFileId id, CancellationToken cancellationToken);
}
