using System.Security.Cryptography;
using System.Text;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>DAPR state-store backed immediate-mode retry scheduler.</summary>
public sealed class DaprProjectionDeliveryRetryScheduler(
    DaprClient daprClient,
    IOptions<ProjectionOptions> projectionOptions,
    IOptions<ProjectionDispatchOptions>? dispatchOptions = null) : IProjectionDeliveryRetryScheduler {
    private const int BulkReadParallelism = 8;
    private const int LedgerShardCount = 64;
    private const int MaxEtagRetries = 5;
    private const string LegacyLedgerKey = "projection-delivery-retry:ledger:v1";
    private const string LedgerKeyPrefix = "projection-delivery-retry:ledger:v2:";
    private const string LeaseKeyPrefix = "projection-delivery-retry:lease:v2:";
    private const string ProtocolMarkerKey = "projection-delivery-retry:protocol";
    private const string ProtocolMarkerValue = "v2-ready:v1-writers-quiesced";
    private static readonly string[] LedgerKeys = [.. Enumerable
        .Range(0, LedgerShardCount)
        .Select(static shard => $"{LedgerKeyPrefix}{shard:x2}")];
    private readonly SemaphoreSlim _legacyMigrationLock = new(1, 1);
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
    public async Task<ProjectionDeliveryRetryWorkItem?> TryAcquireAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        string leaseOwner,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(workItem);
        ValidateWorkItem(workItem);
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseOwner);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        await MigrateLegacyLedgerAsync(cancellationToken).ConfigureAwait(false);

        if (!await TryAcquireAggregateLeaseAsync(
                workItem,
                leaseOwner,
                nowUtc,
                leaseDuration,
                cancellationToken)
            .ConfigureAwait(false)) {
            return null;
        }

        ProjectionDeliveryRetryWorkItem? claimed = null;
        await MutateAsync(
            workItem.WorkId,
            items => {
                int index = items.FindIndex(item => string.Equals(item.WorkId, workItem.WorkId, StringComparison.Ordinal));
                if (index < 0) {
                    return false;
                }

                ProjectionDeliveryRetryWorkItem current = items[index];
                if (current.Revision != workItem.Revision
                    || (!string.IsNullOrWhiteSpace(current.LeaseOwner)
                        && current.LeaseExpiresUtc > nowUtc
                        && !string.Equals(current.LeaseOwner, leaseOwner, StringComparison.Ordinal))) {
                    return false;
                }

                claimed = current with {
                    Revision = current.Revision + 1,
                    LeaseOwner = leaseOwner,
                    LeaseExpiresUtc = nowUtc + leaseDuration,
                };
                items[index] = claimed;
                return true;
            },
            cancellationToken).ConfigureAwait(false);
        if (claimed is null) {
            await ReleaseAggregateLeaseAsync(workItem, leaseOwner, cancellationToken).ConfigureAwait(false);
        }

        return claimed;
    }

    /// <inheritdoc/>
    public async Task<bool> TryUpdateAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(workItem);
        ValidateClaimedWorkItem(workItem);
        await MigrateLegacyLedgerAsync(cancellationToken).ConfigureAwait(false);
        bool updated = false;
        await MutateAsync(
            workItem.WorkId,
            items => {
                int index = items.FindIndex(item => string.Equals(item.WorkId, workItem.WorkId, StringComparison.Ordinal));
                if (index < 0) {
                    return false;
                }

                ProjectionDeliveryRetryWorkItem current = items[index];
                if (current.Revision != workItem.Revision
                    || !string.Equals(current.LeaseOwner, workItem.LeaseOwner, StringComparison.Ordinal)) {
                    return false;
                }

                items[index] = workItem with {
                    Revision = current.Revision + 1,
                    LeaseOwner = null,
                    LeaseExpiresUtc = null,
                };
                updated = true;
                return true;
            },
            cancellationToken).ConfigureAwait(false);
        await ReleaseAggregateLeaseAsync(workItem, workItem.LeaseOwner!, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <inheritdoc/>
    public async Task<bool> TryDeleteAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(workItem);
        ValidateClaimedWorkItem(workItem);
        await MigrateLegacyLedgerAsync(cancellationToken).ConfigureAwait(false);
        bool deleted = false;
        await MutateAsync(
            workItem.WorkId,
            items => {
                int index = items.FindIndex(item => string.Equals(item.WorkId, workItem.WorkId, StringComparison.Ordinal));
                if (index < 0
                    || items[index].Revision != workItem.Revision
                    || !string.Equals(items[index].LeaseOwner, workItem.LeaseOwner, StringComparison.Ordinal)) {
                    return false;
                }

                items.RemoveAt(index);
                deleted = true;
                return true;
            },
            cancellationToken).ConfigureAwait(false);
        await ReleaseAggregateLeaseAsync(workItem, workItem.LeaseOwner!, cancellationToken).ConfigureAwait(false);
        return deleted;
    }

    /// <inheritdoc/>
    public Task UpdateAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(workItem);
        ValidateClaimedWorkItem(workItem);
        return UpdateClaimedCoreAsync(workItem, cancellationToken);
    }

    private async Task UpdateClaimedCoreAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        CancellationToken cancellationToken) {
        if (!await TryUpdateAsync(workItem, cancellationToken).ConfigureAwait(false)) {
            throw new InvalidOperationException("Projection delivery retry update lost its revision or lease.");
        }
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string workId, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(workId);
        return Task.FromException(new InvalidOperationException(
            "Projection delivery retry deletion requires a claimed revision. Use TryDeleteAsync."));
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
            .Where(item => IsValidPersistedWorkItem(item)
                && item.NextDueUtc <= dueUtc
                && item.PendingRoutes.Count > 0
                && (string.IsNullOrWhiteSpace(item.LeaseOwner) || item.LeaseExpiresUtc <= dueUtc))
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

    private static string GetLeaseKey(ProjectionDeliveryRetryWorkItem workItem) {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{workItem.TenantId}\u001f{workItem.Domain}\u001f{workItem.AggregateId}"));
        return $"{LeaseKeyPrefix}{Convert.ToHexStringLower(hash)}";
    }

    private async Task<bool> TryAcquireAggregateLeaseAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        string leaseOwner,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken) {
        string storeName = projectionOptions.Value.CheckpointStateStoreName;
        string leaseKey = GetLeaseKey(workItem);
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            (ProjectionDeliveryRetryLease? lease, string etag) = await daprClient
                .GetStateAndETagAsync<ProjectionDeliveryRetryLease>(
                    storeName,
                    leaseKey,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (lease is not null
                && lease.ExpiresUtc > nowUtc
                && !string.Equals(lease.Owner, leaseOwner, StringComparison.Ordinal)) {
                return false;
            }

            if (await daprClient.TrySaveStateAsync(
                    storeName,
                    leaseKey,
                    new ProjectionDeliveryRetryLease(leaseOwner, nowUtc + leaseDuration),
                    etag,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false)) {
                return true;
            }
        }

        return false;
    }

    private async Task ReleaseAggregateLeaseAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        string leaseOwner,
        CancellationToken cancellationToken) {
        string storeName = projectionOptions.Value.CheckpointStateStoreName;
        string leaseKey = GetLeaseKey(workItem);
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            (ProjectionDeliveryRetryLease? lease, string etag) = await daprClient
                .GetStateAndETagAsync<ProjectionDeliveryRetryLease>(
                    storeName,
                    leaseKey,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (lease is null || !string.Equals(lease.Owner, leaseOwner, StringComparison.Ordinal)) {
                return;
            }

            if (await daprClient.TrySaveStateAsync(
                    storeName,
                    leaseKey,
                    lease with { ExpiresUtc = DateTimeOffset.MinValue },
                    etag,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false)) {
                return;
            }
        }
    }

    private async Task MigrateLegacyLedgerAsync(CancellationToken cancellationToken) {
        if (Volatile.Read(ref _legacyMigrationCompleted) != 0) {
            return;
        }

        await _legacyMigrationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if (Volatile.Read(ref _legacyMigrationCompleted) != 0) {
                return;
            }

            ProjectionDispatchOptions migrationOptions = dispatchOptions?.Value ?? new ProjectionDispatchOptions();
            migrationOptions.Validate();
            if (!migrationOptions.EnableLegacyRetryLedgerMigration) {
                Volatile.Write(ref _legacyMigrationCompleted, 1);
                return;
            }

            string stateStoreName = projectionOptions.Value.CheckpointStateStoreName;
            string? persistedMarker = await daprClient
                .GetStateAsync<string>(
                    stateStoreName,
                    ProtocolMarkerKey,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (string.Equals(persistedMarker, ProtocolMarkerValue, StringComparison.Ordinal)) {
                Volatile.Write(ref _legacyMigrationCompleted, 1);
                return;
            }

            if (!string.IsNullOrWhiteSpace(persistedMarker)) {
                throw new InvalidOperationException("The projection delivery retry protocol marker is not recognized.");
            }

            (ProjectionDeliveryRetryLedger? legacy, string legacyEtag) = await daprClient
                .GetStateAndETagAsync<ProjectionDeliveryRetryLedger>(
                    stateStoreName,
                    LegacyLedgerKey,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            foreach (ProjectionDeliveryRetryWorkItem workItem in (legacy?.Items ?? [])
                .Where(IsValidPersistedWorkItem)
                .OrderBy(static item => item.WorkId, StringComparer.Ordinal)) {
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
                await daprClient.ExecuteStateTransactionAsync(
                    stateStoreName,
                    [new StateTransactionRequest(LegacyLedgerKey, null, StateOperationType.Delete, legacyEtag)],
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            await daprClient.SaveStateAsync(
                stateStoreName,
                ProtocolMarkerKey,
                ProtocolMarkerValue,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _legacyMigrationCompleted, 1);
        }
        finally {
            _ = _legacyMigrationLock.Release();
        }
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
        ArgumentNullException.ThrowIfNull(workItem.PendingRoutes);
        ArgumentNullException.ThrowIfNull(workItem.TerminalRoutes);
        ArgumentNullException.ThrowIfNull(workItem.ReservationFencingTokens);
        if (workItem.PendingRoutes.Count == 0 && workItem.TerminalRoutes.Count == 0) {
            throw new ArgumentException("Retry work must retain at least one pending or terminal route.", nameof(workItem));
        }

        string[] allRoutes = [.. workItem.PendingRoutes.Concat(workItem.TerminalRoutes)];
        if (allRoutes.Any(string.IsNullOrWhiteSpace)
            || allRoutes.Distinct(StringComparer.Ordinal).Count() != allRoutes.Length
            || workItem.ReservationFencingTokens.Any(pair =>
                string.IsNullOrWhiteSpace(pair.Key)
                || pair.Value <= 0
                || !workItem.PendingRoutes.Contains(pair.Key, StringComparer.Ordinal))) {
            throw new ArgumentException(
                "Retry routes and reservation fencing tokens must be unique, positive, and pending-route scoped.",
                nameof(workItem));
        }
    }

    private static void ValidateClaimedWorkItem(ProjectionDeliveryRetryWorkItem workItem) {
        ValidateWorkItem(workItem);
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.LeaseOwner);
        if (workItem.Revision <= 0 || workItem.LeaseExpiresUtc is null) {
            throw new ArgumentException("Retry work must carry a valid claimed revision and lease.", nameof(workItem));
        }
    }

    private static bool IsValidPersistedWorkItem(ProjectionDeliveryRetryWorkItem? workItem) {
        try {
            ValidateWorkItem(workItem!);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NullReferenceException) {
            return false;
        }
    }
}
