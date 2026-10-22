using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.Client.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.DomainService;

// Each durable shard is bounded. Registration precedes promotion, so a crash
// after promotion cannot leave a committed operation undiscoverable by the sweep.
/// <summary>One bounded shard of durable private scratch cleanup work.</summary>
/// <param name="Operations">The operations awaiting terminal scratch reconciliation.</param>
internal sealed record DomainSharedProjectionScratchCleanupQueue(
    DomainSharedProjectionRebuildIdentity[] Operations);

/// <summary>Registers and reconciles terminal shared rebuild scratch work.</summary>
internal static class DomainSharedProjectionScratchCleanup
{
    private const int ShardCount = 64;
    private const int MaxOperationsPerShard = 100;

    private static string Key(DomainSharedProjectionRebuildIdentity identity)
    {
        byte first = SHA256.HashData(Encoding.UTF8.GetBytes(
            DomainSharedProjectionRebuildSessionKey.Compute(identity)))[0];
        return "shared-rebuild-scratch-cleanup-v1:" + (first % ShardCount).ToString("D2", CultureInfo.InvariantCulture);
    }

    private static DomainSharedProjectionRebuildIdentity[] ReadOperations(
        DomainSharedProjectionScratchCleanupQueue? queue)
    {
        if (queue is null) return [];
        DomainSharedProjectionRebuildIdentity[] operations = queue.Operations
            ?? throw new InvalidOperationException("Shared rebuild cleanup work index is corrupt.");
        if (operations.Length > MaxOperationsPerShard || operations.Any(static item => item is null)
            || operations.Distinct().Count() != operations.Length)
            throw new InvalidOperationException("Shared rebuild cleanup work index is corrupt.");
        return operations;
    }

    /// <summary>Durably registers private scratch for eventual terminal cleanup.</summary>
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
                throw new InvalidOperationException("Shared rebuild cleanup work index is full.");
            var next = new DomainSharedProjectionScratchCleanupQueue([.. existing, identity]);
            if (await store.TrySaveAsync(storeName, key, next, entry.ETag ?? string.Empty, cancellationToken)
                .ConfigureAwait(false)) return;
        }

        throw new InvalidOperationException("Shared rebuild cleanup work registration conflicted.");
    }

    /// <summary>Retries terminal cleanup from bounded durable work shards.</summary>
    internal static async Task RunPassAsync(IServiceProvider services, ProjectionDispatchOptions options,
        CancellationToken cancellationToken, Action<Exception>? onItemFailure = null)
    {
        IReadModelStore store = services.GetRequiredService<IReadModelStore>();
        foreach (IAsyncDomainSharedProjectionPartitionedRebuildHandler handler in services
            .GetServices<IAsyncDomainProjectionHandler>()
            .OfType<IAsyncDomainSharedProjectionPartitionedRebuildHandler>()
            .Where(static handler => handler.EpochEnabled))
        {
            for (int shard = 0; shard < ShardCount; shard++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string key = "shared-rebuild-scratch-cleanup-v1:" + shard.ToString("D2", CultureInfo.InvariantCulture);
                ReadModelEntry<DomainSharedProjectionScratchCleanupQueue> entry = await store
                    .GetAsync<DomainSharedProjectionScratchCleanupQueue>(handler.RebuildStoreName, key, cancellationToken)
                    .ConfigureAwait(false);
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
                        if (session?.Phase is not (DomainSharedProjectionRebuildPhase.Committed
                            or DomainSharedProjectionRebuildPhase.Aborted)) continue;
                        await DomainSharedProjectionRebuildDispatcher.ReconcilePartitionScratchAsync(
                            services, store, handler, identity, options, cancellationToken).ConfigureAwait(false);
                        await RemoveAsync(store, handler.RebuildStoreName, identity, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        // The durable entry remains available for the next pass or a new replica.
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

        throw new InvalidOperationException("Shared rebuild cleanup work removal conflicted.");
    }
}

/// <summary>Resumes durable shared-projection private scratch cleanup after response or restart.</summary>
public sealed class DomainSharedProjectionScratchCleanupService(
    IServiceScopeFactory scopes,
    IOptions<ProjectionDispatchOptions> options,
    ILogger<DomainSharedProjectionScratchCleanupService> logger) : BackgroundService
{
    /// <summary>Runs one bounded scan, primarily for deterministic recovery verification.</summary>
    public async Task RunPassAsync(CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = scopes.CreateScope();
        await DomainSharedProjectionScratchCleanup.RunPassAsync(scope.ServiceProvider, options.Value,
            cancellationToken, exception => logger.LogWarning(exception,
                "Shared projection scratch cleanup item will be retried.")).ConfigureAwait(false);
        await DomainSharedProjectionBeginCleanup.RunPassAsync(scope.ServiceProvider,
            cancellationToken, exception => logger.LogWarning(exception,
                "Shared projection Begin cleanup item will be retried.")).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            do
            {
                try { await RunPassAsync(stoppingToken).ConfigureAwait(false); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception) { logger.LogWarning(exception, "Shared projection scratch cleanup pass failed."); }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
