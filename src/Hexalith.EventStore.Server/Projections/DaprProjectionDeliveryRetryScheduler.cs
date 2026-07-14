using System.Security.Cryptography;
using System.Text;

using Dapr.Client;

using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>DAPR state-store backed immediate-mode retry scheduler.</summary>
public sealed class DaprProjectionDeliveryRetryScheduler(
    DaprClient daprClient,
    IOptions<ProjectionOptions> projectionOptions) : IProjectionDeliveryRetryScheduler {
    private const int BulkReadParallelism = 8;
    private const int LedgerShardCount = 64;
    private const int MaxEtagRetries = 5;
    private const string LegacyLedgerKey = "projection-delivery-retry:ledger:v1";
    private const string LedgerKeyPrefix = "projection-delivery-retry:ledger:v2:";
    private static readonly string[] LedgerKeys = [.. Enumerable
        .Range(0, LedgerShardCount)
        .Select(static shard => $"{LedgerKeyPrefix}{shard:x2}")];
    private int _legacyMigrationCompleted;

    /// <inheritdoc/>
    public async Task<ProjectionDeliveryRetryWorkItem> ScheduleAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        CancellationToken cancellationToken = default) {
        ValidateWorkItem(workItem);
        await MigrateLegacyLedgerAsync(cancellationToken).ConfigureAwait(false);
        ProjectionDeliveryRetryWorkItem? persisted = null;
        await MutateAsync(
            workItem.WorkId,
            items => {
                ProjectionDeliveryRetryWorkItem? existing = items.FirstOrDefault(
                    item => string.Equals(item.WorkId, workItem.WorkId, StringComparison.Ordinal));
                persisted = existing ?? workItem;
                if (existing is null) {
                    items.Add(workItem);
                }

                return existing is null;
            },
            cancellationToken).ConfigureAwait(false);
        return persisted ?? workItem;
    }

    /// <inheritdoc/>
    public Task UpdateAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        CancellationToken cancellationToken = default) {
        ValidateWorkItem(workItem);
        return UpdateCoreAsync(workItem, cancellationToken);
    }

    private async Task UpdateCoreAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        CancellationToken cancellationToken) {
        await MigrateLegacyLedgerAsync(cancellationToken).ConfigureAwait(false);
        await MutateAsync(
            workItem.WorkId,
            items => {
                int index = items.FindIndex(item => string.Equals(item.WorkId, workItem.WorkId, StringComparison.Ordinal));
                if (index < 0) {
                    // Update-only: never resurrect a work item that a concurrent live delivery has
                    // already converged and deleted. Insertion is ScheduleAsync's responsibility.
                    return false;
                }

                items[index] = workItem;
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string workId, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(workId);
        await MigrateLegacyLedgerAsync(cancellationToken).ConfigureAwait(false);
        await MutateAsync(
            workId,
            items => items.RemoveAll(item => string.Equals(item.WorkId, workId, StringComparison.Ordinal)) > 0,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProjectionDeliveryRetryWorkItem>> GetDueAsync(
        DateTimeOffset dueUtc,
        int maximumCount,
        CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        await MigrateLegacyLedgerAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<BulkStateItem<ProjectionDeliveryRetryLedger>> shards = await daprClient
            .GetBulkStateAsync<ProjectionDeliveryRetryLedger>(
                projectionOptions.Value.CheckpointStateStoreName,
                LedgerKeys,
                BulkReadParallelism,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return [.. shards
            .SelectMany(static shard => shard.Value?.Items ?? [])
            .Where(item => item.NextDueUtc <= dueUtc && item.PendingRoutes.Count > 0)
            .OrderBy(static item => item.NextDueUtc)
            .ThenBy(static item => item.WorkId, StringComparer.Ordinal)
            .Take(maximumCount)];
    }

    private async Task MutateAsync(
        string workId,
        Func<List<ProjectionDeliveryRetryWorkItem>, bool> mutation,
        CancellationToken cancellationToken) {
        string stateStoreName = projectionOptions.Value.CheckpointStateStoreName;
        string ledgerKey = GetLedgerKey(workId);
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            cancellationToken.ThrowIfCancellationRequested();
            (ProjectionDeliveryRetryLedger? ledger, string etag) = await daprClient
                .GetStateAndETagAsync<ProjectionDeliveryRetryLedger>(
                    stateStoreName,
                    ledgerKey,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            List<ProjectionDeliveryRetryWorkItem> items = [.. ledger?.Items ?? []];
            if (!mutation(items)) {
                return;
            }

            bool saved = await daprClient
                .TrySaveStateAsync(
                    stateStoreName,
                    ledgerKey,
                    new ProjectionDeliveryRetryLedger(items),
                    etag,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (saved) {
                return;
            }
        }

        throw new InvalidOperationException("Projection delivery retry ledger update exhausted optimistic-concurrency attempts.");
    }

    private static string GetLedgerKey(string workId) {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(workId));
        int shard = hash[0] % LedgerShardCount;
        return LedgerKeys[shard];
    }

    private async Task MigrateLegacyLedgerAsync(CancellationToken cancellationToken) {
        if (Volatile.Read(ref _legacyMigrationCompleted) != 0) {
            return;
        }

        string stateStoreName = projectionOptions.Value.CheckpointStateStoreName;
        ProjectionDeliveryRetryLedger? legacy = await daprClient
            .GetStateAsync<ProjectionDeliveryRetryLedger>(
                stateStoreName,
                LegacyLedgerKey,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        foreach (ProjectionDeliveryRetryWorkItem workItem in (legacy?.Items ?? [])
            .OrderBy(static item => item.WorkId, StringComparer.Ordinal)) {
            ValidateWorkItem(workItem);
            await MutateAsync(
                workItem.WorkId,
                items => {
                    if (items.Any(item => string.Equals(item.WorkId, workItem.WorkId, StringComparison.Ordinal))) {
                        return false;
                    }

                    items.Add(workItem);
                    return true;
                },
                cancellationToken).ConfigureAwait(false);
        }

        if (legacy is not null) {
            await daprClient
                .DeleteStateAsync(stateStoreName, LegacyLedgerKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        Volatile.Write(ref _legacyMigrationCompleted, 1);
    }

    private static void ValidateWorkItem(ProjectionDeliveryRetryWorkItem workItem) {
        ArgumentNullException.ThrowIfNull(workItem);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.WorkId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.TenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.Domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.AggregateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.AppId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.ServiceVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(workItem.HeadSequence);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.HeadMessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.DispatchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.CatalogFingerprint);
        ArgumentOutOfRangeException.ThrowIfNegative(workItem.Attempt);
        if (workItem.PendingRoutes.Count == 0 && workItem.TerminalRoutes.Count == 0) {
            throw new ArgumentException("Retry work must retain at least one pending or terminal route.", nameof(workItem));
        }
    }
}
