using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Client.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.DomainService;

/// <summary>Durable, bounded work for reclaiming private paged Begin inventory after a seal or displacement.</summary>
internal static class DomainSharedProjectionBeginCleanup
{
    private const int ShardCount = 64;
    private const int MaxOperationsPerShard = 100;
    private const string KeyPrefix = "shared-begin-cleanup-v1:";

    private static string Key(DomainSharedProjectionRebuildIdentity identity)
    {
        byte first = SHA256.HashData(Encoding.UTF8.GetBytes(
            DomainSharedProjectionRebuildSessionKey.Compute(identity)))[0];
        return KeyPrefix + (first % ShardCount).ToString("D2", CultureInfo.InvariantCulture);
    }

    private static DomainSharedProjectionRebuildIdentity[] ReadOperations(
        DomainSharedProjectionScratchCleanupQueue? queue)
    {
        if (queue is null) return [];
        DomainSharedProjectionRebuildIdentity[] operations = queue.Operations
            ?? throw new InvalidOperationException("Begin cleanup work index is corrupt.");
        if (operations.Length > MaxOperationsPerShard || operations.Any(static item => item is null)
            || operations.Distinct().Count() != operations.Length)
            throw new InvalidOperationException("Begin cleanup work index is corrupt.");
        return operations;
    }

    internal static async Task RegisterAsync(IReadModelStore store, string storeName,
        DomainSharedProjectionRebuildIdentity identity, CancellationToken cancellationToken)
    {
        string key = Key(identity);
        for (int attempt = 0; attempt < 20; attempt++)
        {
            ReadModelEntry<DomainSharedProjectionScratchCleanupQueue> entry = await store
                .GetAsync<DomainSharedProjectionScratchCleanupQueue>(storeName, key, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildIdentity[] existing = ReadOperations(entry.Value);
            if (existing.Contains(identity)) return;
            if (existing.Length >= MaxOperationsPerShard)
                throw new InvalidOperationException("Begin cleanup work index is full.");
            var next = new DomainSharedProjectionScratchCleanupQueue([.. existing, identity]);
            if (await store.TrySaveAsync(storeName, key, next, entry.ETag ?? string.Empty, cancellationToken)
                .ConfigureAwait(false)) return;
        }

        throw new InvalidOperationException("Begin cleanup work registration conflicted.");
    }

    internal static async Task RunPassAsync(IServiceProvider services, CancellationToken cancellationToken,
        Action<Exception>? onItemFailure = null)
    {
        IReadModelStore store = services.GetRequiredService<IReadModelStore>();
        IReadModelBatchStore batches = services.GetRequiredService<IReadModelBatchStore>();
        foreach (IAsyncDomainSharedProjectionRebuildHandler handler in services
            .GetServices<IAsyncDomainProjectionHandler>()
            .OfType<IAsyncDomainSharedProjectionRebuildHandler>()
            .Where(static handler => handler is IAsyncDomainSharedProjectionEpochHandler { EpochEnabled: true }))
        {
            for (int shard = 0; shard < ShardCount; shard++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string key = KeyPrefix + shard.ToString("D2", CultureInfo.InvariantCulture);
                ReadModelEntry<DomainSharedProjectionScratchCleanupQueue> entry = await store
                    .GetAsync<DomainSharedProjectionScratchCleanupQueue>(handler.RebuildStoreName, key,
                        cancellationToken).ConfigureAwait(false);
                foreach (DomainSharedProjectionRebuildIdentity identity in ReadOperations(entry.Value))
                {
                    if (identity.Domain != handler.Domain || identity.ProjectionType != handler.ProjectionType)
                        continue;
                    try
                    {
                        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
                        DomainSharedProjectionRebuildSessionState? session = (await store
                            .GetAsync<DomainSharedProjectionRebuildSessionState>(handler.RebuildStoreName,
                                sessionKey, cancellationToken).ConfigureAwait(false)).Value;
                        DomainSharedProjectionRebuildIdentity? lease = (await store
                            .GetAsync<DomainSharedProjectionRebuildIdentity>(handler.RebuildStoreName,
                                DomainSharedProjectionRebuildDispatcher.BeginLeaseKey(identity),
                                cancellationToken).ConfigureAwait(false)).Value;
                        if (session is null && lease == identity) continue;
                        await DomainSharedProjectionRebuildDispatcher.ReclaimBeginPagesAsync(
                            store, batches, handler, identity, cancellationToken).ConfigureAwait(false);
                        await RemoveAsync(store, handler.RebuildStoreName, identity, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        onItemFailure?.Invoke(exception);
                    }
                }
            }
        }
    }

    private static async Task RemoveAsync(IReadModelStore store, string storeName,
        DomainSharedProjectionRebuildIdentity identity, CancellationToken cancellationToken)
    {
        string key = Key(identity);
        for (int attempt = 0; attempt < 20; attempt++)
        {
            ReadModelEntry<DomainSharedProjectionScratchCleanupQueue> entry = await store
                .GetAsync<DomainSharedProjectionScratchCleanupQueue>(storeName, key, cancellationToken)
                .ConfigureAwait(false);
            DomainSharedProjectionRebuildIdentity[] existing = ReadOperations(entry.Value);
            if (!existing.Contains(identity)) return;
            var next = new DomainSharedProjectionScratchCleanupQueue([.. existing.Where(item => item != identity)]);
            if (await store.TrySaveAsync(storeName, key, next, entry.ETag ?? string.Empty, cancellationToken)
                .ConfigureAwait(false)) return;
        }

        throw new InvalidOperationException("Begin cleanup work removal conflicted.");
    }
}
