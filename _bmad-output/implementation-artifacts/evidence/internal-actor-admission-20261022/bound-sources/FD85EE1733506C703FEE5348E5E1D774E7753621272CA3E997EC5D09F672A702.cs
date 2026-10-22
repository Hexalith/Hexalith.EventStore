using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Hexalith.EventStore.Client.Streams;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>Bounds discovery independently of contextual reads and fences repeated abandoned logical operations.</summary>
internal static class SharedProjectionDiscoveryReadAdmission
{
    private static readonly SemaphoreSlim Slots = new(16, 16);
    private static readonly ConditionalWeakTable<IReadModelStore, ConcurrentDictionary<(string Store, string Key), byte>> Pending = new();

    /// <summary>Rejects an already outstanding provider/key instead of invoking it again; results are never cached.</summary>
    internal static async Task<T> ReadAsync<T>(IReadModelStore provider, string store, string key,
        Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pending = Pending.GetOrCreateValue(provider);
        var identity = (store, key);
        if (!pending.TryAdd(identity, 0)) throw new AuthoritativeStreamReadAdmissionException();
        bool admitted = false;
        bool transferred = false;
        try
        {
            admitted = await Slots.WaitAsync(0, cancellationToken).ConfigureAwait(false);
            if (!admitted) throw new AuthoritativeStreamReadAdmissionException();
            using var deadline = new AuthoritativeStreamReadDeadline(TimeSpan.FromMilliseconds(100),
                TimeProvider.System, cancellationToken, TimeProvider.System.GetTimestamp());
            return await deadline.ReadAsync(read, operationStarted: operation =>
            {
                transferred = true;
                _ = operation.ContinueWith(completed =>
                {
                    pending.TryRemove(identity, out _);
                    Slots.Release();
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }).ConfigureAwait(false);
        }
        finally
        {
            // Actual provider completion alone releases a transferred slot/key, even after timeout or cancellation.
            if (!transferred)
            {
                pending.TryRemove(identity, out _);
                if (admitted) Slots.Release();
            }
        }
    }
}
