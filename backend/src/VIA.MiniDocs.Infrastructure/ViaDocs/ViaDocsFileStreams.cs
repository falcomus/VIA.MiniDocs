using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using VIA.MiniDocs.Application;

namespace VIA.MiniDocs.Infrastructure.ViaDocs;

internal sealed class ViaDocsDeadline : IDisposable
{
    private readonly CancellationTokenSource timer;
    private readonly CancellationTokenSource linked;
    internal CancellationToken Caller { get; }
    internal CancellationToken Token { get; }

    internal ViaDocsDeadline(int seconds, TimeProvider clock, CancellationToken caller)
    {
        Caller = caller;
        timer = new(TimeSpan.FromSeconds(seconds), clock);
        linked = CancellationTokenSource.CreateLinkedTokenSource(caller, timer.Token);
        Token = linked.Token;
    }

    internal ViaDocsFailureKind CancellationKind => Caller.IsCancellationRequested
        ? ViaDocsFailureKind.Cancellation : ViaDocsFailureKind.Timeout;

    public void Dispose()
    {
        try { linked.Cancel(); }
        finally
        {
            linked.Dispose();
            timer.Dispose();
        }
    }
}

internal sealed class ViaDocsResponseBody : IViaDocsBody
{
    private readonly HttpResponseMessage response;
    private readonly Stream stream;
    private readonly ViaDocsDeadline budget;
    private readonly TimeProvider clock;
    private readonly int idleSeconds;
    private readonly long limit;
    private readonly long? expected;
    private readonly CancellationTokenRegistration closeOnDeadline;
    private long consumed;
    private bool finished;
    private int disposed;
    private int responseClosed;
    private CloseFailure responseFailure;

    private enum CloseFailure { None, Operational, Unexpected }

    internal ViaDocsResponseBody(HttpResponseMessage response, Stream stream, ViaDocsDeadline budget,
        TimeProvider clock, int idleSeconds, long limit, long? expected)
    {
        this.response = response;
        this.stream = stream;
        this.budget = budget;
        this.clock = clock;
        this.idleSeconds = idleSeconds;
        this.limit = limit;
        this.expected = expected;
        closeOnDeadline = budget.Token.UnsafeRegister(static state => ((ViaDocsResponseBody)state!).CloseResponse(), this);
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (finished)
            return 0;
        ObjectDisposedException.ThrowIf(disposed != 0, this);
        if (buffer.IsEmpty)
            return 0;
        using var idle = new CancellationTokenSource(TimeSpan.FromSeconds(idleSeconds), clock);
        using var read = CancellationTokenSource.CreateLinkedTokenSource(budget.Token, cancellationToken, idle.Token);
        try
        {
            read.Token.ThrowIfCancellationRequested();
            var allowed = Math.Min(limit, expected ?? limit) - consumed;
            // A one-byte probe distinguishes EOF from a body that exceeded its advertised boundary.
            var destination = allowed == 0 ? new byte[1].AsMemory()
                : buffer[..(int)Math.Min(Math.Min(buffer.Length, 64 * 1024), allowed)];
            var count = await stream.ReadAsync(destination, read.Token);
            read.Token.ThrowIfCancellationRequested();
            if (count == 0 && consumed > 0 && (expected is null || expected == consumed))
            {
                await DisposeAsync();
                finished = true;
                return 0;
            }
            if (count == 0 || allowed == 0)
                throw new ViaDocsBodyException(count == 0 || expected.HasValue
                    ? ViaDocsFailureKind.Contract : ViaDocsFailureKind.Limit);
            consumed += count;
            return count;
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or HttpRequestException ||
            (exception is ObjectDisposedException && read.IsCancellationRequested))
        {
            var kind = exception is ViaDocsBodyException safe ? safe.Kind
                : cancellationToken.IsCancellationRequested || budget.Caller.IsCancellationRequested
                ? ViaDocsFailureKind.Cancellation
                : read.IsCancellationRequested ? ViaDocsFailureKind.Timeout
                : exception is HttpIOException { HttpRequestError: HttpRequestError.ResponseEnded }
                    ? ViaDocsFailureKind.Contract : ViaDocsFailureKind.Unavailable;
            // Disposal cancels the owned budget. Determine the read failure before changing that state.
            await DisposeCoreAsync();
            throw new ViaDocsBodyException(kind);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (await DisposeCoreAsync())
            throw new ViaDocsBodyException(ViaDocsFailureKind.Unavailable);
    }

    private async ValueTask<bool> DisposeCoreAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return false;
        closeOnDeadline.Dispose();
        var streamFailed = false;
        try
        {
            await stream.DisposeAsync();
        }
        catch (Exception exception) when (IsOperationalCloseFailure(exception))
        {
            streamFailed = true;
        }
        finally
        {
            CloseResponse();
            budget.Dispose();
        }
        if (responseFailure == CloseFailure.Unexpected)
            throw new InvalidOperationException("Unexpected failure while closing a VIA.Docs response.");
        return streamFailed || responseFailure == CloseFailure.Operational;
    }

    private void CloseResponse()
    {
        if (Interlocked.Exchange(ref responseClosed, 1) != 0)
            return;
        try
        {
            response.Dispose();
        }
        catch (Exception exception)
        {
            // A cancellation callback has no awaiter. The owner observes the recorded failure on disposal.
            responseFailure = IsOperationalCloseFailure(exception) ? CloseFailure.Operational : CloseFailure.Unexpected;
        }
    }

    private static bool IsOperationalCloseFailure(Exception exception) =>
        exception is IOException or HttpRequestException or OperationCanceledException or ObjectDisposedException;
}

internal sealed class ViaDocsUploadException(ViaDocsFailureKind kind) : IOException("VIA.Docs upload stream failed.")
{
    internal ViaDocsFailureKind Kind { get; } = kind;
}

internal sealed class ViaDocsMultipart : HttpContent
{
    private readonly MultipartFormDataContent parts = new();
    private int started;
    private int completed;
    internal bool Completed => Volatile.Read(ref completed) != 0;

    internal ViaDocsMultipart() => Headers.ContentType = parts.Headers.ContentType;

    internal void Add(HttpContent content, string name, string fileName) => parts.Add(content, name, fileName);

    protected override bool TryComputeLength(out long length)
    {
        var declared = parts.Headers.ContentLength;
        length = declared.GetValueOrDefault();
        return declared.HasValue;
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref started, 1) != 0)
            throw new ViaDocsUploadException(ViaDocsFailureKind.Contract);
        await parts.CopyToAsync(stream, context, cancellationToken);
        Volatile.Write(ref completed, 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            parts.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class ViaDocsUploadContent : HttpContent
{
    internal const int MaximumBytes = 25 * 1024 * 1024;
    private readonly Stream source;
    private readonly long? length;
    private readonly int idleSeconds;
    private readonly TimeProvider clock;
    private readonly CancellationToken deadline;
    private int started;
    internal long BytesSent { get; private set; }

    internal ViaDocsUploadContent(Stream source, long? length, int idleSeconds, TimeProvider clock, CancellationToken deadline)
    {
        this.source = source;
        this.length = length;
        this.idleSeconds = idleSeconds;
        this.clock = clock;
        this.deadline = deadline;
        Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
    }

    protected override bool TryComputeLength(out long size)
    {
        size = length.GetValueOrDefault();
        return length.HasValue;
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref started, 1) != 0)
            throw new ViaDocsUploadException(ViaDocsFailureKind.Contract);
        const int chunk = 32 * 1024;
        var buffer = ArrayPool<byte>.Shared.Rent(chunk);
        try
        {
            while (true)
            {
                using var idle = new CancellationTokenSource(TimeSpan.FromSeconds(idleSeconds), clock);
                using var transfer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline, idle.Token);
                try
                {
                    var remaining = (length ?? MaximumBytes) - BytesSent;
                    var read = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(chunk, remaining + 1)), transfer.Token);
                    transfer.Token.ThrowIfCancellationRequested();
                    if (read == 0)
                    {
                        if (BytesSent == 0 || (length.HasValue && length != BytesSent))
                            throw new ViaDocsUploadException(ViaDocsFailureKind.Contract);
                        return;
                    }
                    if (read > remaining)
                        throw new ViaDocsUploadException(length.HasValue ? ViaDocsFailureKind.Contract : ViaDocsFailureKind.Limit);
                    await stream.WriteAsync(buffer.AsMemory(0, read), transfer.Token);
                    transfer.Token.ThrowIfCancellationRequested();
                    BytesSent += read;
                }
                catch (OperationCanceledException) when (idle.IsCancellationRequested && !deadline.IsCancellationRequested)
                {
                    throw new ViaDocsUploadException(ViaDocsFailureKind.Timeout);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }
}
