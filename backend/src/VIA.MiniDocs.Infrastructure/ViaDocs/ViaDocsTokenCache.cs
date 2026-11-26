using Microsoft.Extensions.Options;
using VIA.MiniDocs.Application;

namespace VIA.MiniDocs.Infrastructure.ViaDocs;

internal sealed class ViaDocsTokenCache(IOptions<ViaDocsOptions> options, TimeProvider clock)
{
    internal readonly record struct Key(Uri Root, Guid Workspace, Guid Application, string Name);
    private sealed class Slot(ViaDocsCredentialReference reference, long generation)
    {
        internal ViaDocsCredentialReference Reference { get; } = reference;
        internal long Generation { get; } = generation;
        internal ViaDocsSession? Session { get; set; }
        internal TaskCompletionSource<ViaDocsResult<ViaDocsSession>>? Flight { get; set; }
    }

    private readonly Dictionary<Key, Slot> slots = new();
    private readonly object gate = new();
    private int active;

    internal Task<ViaDocsResult<ViaDocsSession>> GetAsync(Key key, ViaDocsCredentialReference reference, long generation,
        Func<Task<ViaDocsResult<ViaDocsSession>>> login, CancellationToken cancellationToken)
    {
        TaskCompletionSource<ViaDocsResult<ViaDocsSession>> flight;
        Slot slot;
        lock (gate)
        {
            if (cancellationToken.IsCancellationRequested)
                return Failed(ViaDocsFailureKind.Cancellation);

            foreach (var expired in slots.Where(pair => pair.Value.Flight is null &&
                (pair.Value.Session is null || pair.Value.Session.RenewAt <= clock.GetUtcNow())).ToArray())
                slots.Remove(expired.Key);

            if (slots.TryGetValue(key, out var existing))
            {
                if (generation < existing.Generation ||
                    (generation == existing.Generation && reference != existing.Reference))
                    return Failed(ViaDocsFailureKind.Authentication);
                if (generation == existing.Generation && existing.Session is { } token)
                    return Task.FromResult<ViaDocsResult<ViaDocsSession>>(new ViaDocsResult<ViaDocsSession>.Success(token));
                if (generation == existing.Generation && existing.Flight is { } pending)
                    return pending.Task.WaitAsync(cancellationToken);
                // Detach the old generation even if capacity prevents a replacement.
                slots.Remove(key);
            }

            if (active >= options.Value.ConcurrentLogins || slots.Count >= options.Value.CacheEntries)
                return Failed(ViaDocsFailureKind.Limit);

            slot = new Slot(reference, generation);
            flight = new(TaskCreationOptions.RunContinuationsAsynchronously);
            slot.Flight = flight;
            slots.Add(key, slot);
            active++;
        }

        _ = FinishAsync(key, slot, flight, login);
        return flight.Task.WaitAsync(cancellationToken);
    }

    private async Task FinishAsync(Key key, Slot slot, TaskCompletionSource<ViaDocsResult<ViaDocsSession>> flight,
        Func<Task<ViaDocsResult<ViaDocsSession>>> login)
    {
        ViaDocsResult<ViaDocsSession> result;
        try
        {
            result = await login();
        }
        catch (Exception)
        {
            lock (gate)
            {
                active--;
                if (slots.GetValueOrDefault(key) == slot)
                    slots.Remove(key);
                flight.SetException(new InvalidOperationException("Unexpected failure during shared VIA.Docs login."));
                // The original waiters may already have cancelled.
                _ = flight.Task.Exception;
            }
            return;
        }

        lock (gate)
        {
            active--;
            slot.Flight = null;
            if (slots.GetValueOrDefault(key) != slot)
                result = new ViaDocsResult<ViaDocsSession>.Failure(new(ViaDocsFailureKind.Authentication));
            else if (result is ViaDocsResult<ViaDocsSession>.Success success)
                slot.Session = success.Value;
            else
                slots.Remove(key);
            flight.SetResult(result);
        }
    }

    internal void Invalidate(Key key, ViaDocsSession rejected)
    {
        lock (gate)
        {
            if (slots.GetValueOrDefault(key) is { } slot &&
                ReferenceEquals(slot.Session, rejected))
                slots.Remove(key);
        }
    }

    private static Task<ViaDocsResult<ViaDocsSession>> Failed(ViaDocsFailureKind kind) =>
        Task.FromResult<ViaDocsResult<ViaDocsSession>>(new ViaDocsResult<ViaDocsSession>.Failure(new(kind)));
}

internal sealed class ViaDocsSession(Guid id, string name, string jwt, DateTimeOffset renewAt)
{
    internal Guid Id { get; } = id;
    internal string Name { get; } = name;
    internal string Jwt { get; } = jwt;
    internal DateTimeOffset RenewAt { get; } = renewAt;
    public override string ToString() => "[redacted]";
}
