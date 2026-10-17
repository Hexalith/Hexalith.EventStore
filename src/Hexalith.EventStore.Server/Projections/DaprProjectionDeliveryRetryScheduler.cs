using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
    public async Task<ProjectionDeliveryRetryWorkItem?> ReadAsync(string workId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workId);
        await MigrateLegacyLedgerAsync(cancellationToken).ConfigureAwait(false);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SchedulerShardReads);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        ProjectionDeliveryRetryLedger? ledger = await daprClient.GetStateAsync<ProjectionDeliveryRetryLedger>(
            projectionOptions.Value.CheckpointStateStoreName, GetLedgerKey(workId), cancellationToken: cancellationToken).ConfigureAwait(false);
        RecordLedgerRead(ledger);
        if (ledger is not null && (ledger.Items is null || ledger.Items.Any(static item => !IsValidPersistedWorkItem(item))
            || ledger.Items.Count(item => item.WorkId == workId) > 1))
            throw new InvalidOperationException("delivery_reconciliation_required");
        return ledger?.Items.SingleOrDefault(item => item.WorkId == workId);
    }

    /// <inheritdoc/>
    public async Task<ProjectionDeliveryRetryWorkItem> ScheduleAsync(
        ProjectionDeliveryRetryWorkItem workItem,
        CancellationToken cancellationToken = default) {
        ValidateWorkItem(workItem);
        await MigrateLegacyLedgerAsync(cancellationToken).ConfigureAwait(false);
        ProjectionDeliveryRetryWorkItem? persisted = null;
        _ = await MutateAsync(
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
        bool saved = await MutateAsync(
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
        if (!saved) claimed = null;
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
        bool updated = await MutateAsync(
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
        bool deleted = await MutateAsync(
            workItem.WorkId,
            items => {
                int index = items.FindIndex(item => string.Equals(item.WorkId, workItem.WorkId, StringComparison.Ordinal));
                if (index < 0
                    || items[index].Revision != workItem.Revision
                    || !string.Equals(items[index].LeaseOwner, workItem.LeaseOwner, StringComparison.Ordinal)) {
                    return false;
                }

                items.RemoveAt(index);
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
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SchedulerScans);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SchedulerShardReads, LedgerShardCount);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        IReadOnlyList<BulkStateItem<ProjectionDeliveryRetryLedger>> shards = await daprClient
            .GetBulkStateAsync<ProjectionDeliveryRetryLedger>(
                projectionOptions.Value.CheckpointStateStoreName,
                LedgerKeys,
                BulkReadParallelism,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        foreach (BulkStateItem<ProjectionDeliveryRetryLedger> shard in shards) RecordLedgerRead(shard.Value);
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

    /// <summary>Finds terminal or malformed durable retry evidence across every ledger shard.</summary>
    internal async Task<bool> HasUnresolvedConflictAsync(CancellationToken cancellationToken = default) {
        await MigrateLegacyLedgerAsync(cancellationToken).ConfigureAwait(false);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SchedulerScans);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SchedulerShardReads, LedgerShardCount);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
        IReadOnlyList<BulkStateItem<ProjectionDeliveryRetryLedger>> shards = await daprClient
            .GetBulkStateAsync<ProjectionDeliveryRetryLedger>(
                projectionOptions.Value.CheckpointStateStoreName,
                LedgerKeys,
                BulkReadParallelism,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        foreach (BulkStateItem<ProjectionDeliveryRetryLedger> shard in shards) RecordLedgerRead(shard.Value);
        return shards.Any(static shard => shard.Value is not null
            && (shard.Value.Items is null
                || shard.Value.Items.Any(static item => !IsValidPersistedWorkItem(item)
                    || item.TerminalRoutes.Count > 0)));
    }

    private async Task<bool> MutateAsync(
        string workId,
        Func<List<ProjectionDeliveryRetryWorkItem>, bool> mutation,
        CancellationToken cancellationToken) {
        string stateStoreName = projectionOptions.Value.CheckpointStateStoreName;
        string ledgerKey = GetLedgerKey(workId);
        for (int attempt = 0; attempt < MaxEtagRetries; attempt++) {
            cancellationToken.ThrowIfCancellationRequested();
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SchedulerShardReads);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
            (ProjectionDeliveryRetryLedger? ledger, string etag) = await daprClient
                .GetStateAndETagAsync<ProjectionDeliveryRetryLedger>(
                    stateStoreName,
                    ledgerKey,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            RecordLedgerRead(ledger);
            List<ProjectionDeliveryRetryWorkItem> items = [.. ledger?.Items ?? []];
            if (!mutation(items)) {
                return false;
            }

            var replacement = new ProjectionDeliveryRetryLedger(items);
            long writeBytes = MeasureBody(replacement);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SchedulerCasAttempts);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SchedulerWriteAttempts);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SchedulerWriteBytes, writeBytes);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
            bool saved = await daprClient
                .TrySaveStateAsync(
                    stateStoreName,
                    ledgerKey,
                    replacement,
                    etag,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (saved) {
                return true;
            }
        }

        throw new InvalidOperationException("Projection delivery retry ledger update exhausted optimistic-concurrency attempts.");
    }

    private void RecordLedgerRead(ProjectionDeliveryRetryLedger? ledger)
    {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SchedulerEntries, ledger?.Items?.Count ?? 0);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SchedulerReadBytes,
            ledger is null ? 0 : MeasureBody(ledger));
    }

    private long MeasureBody<T>(T value)
    {
        using var count = new SizeLimitedWriteStream(long.MaxValue);
        JsonSerializer.Serialize(count, value, daprClient.JsonSerializerOptions);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataSizingBytes, count.Length);
        return count.Length;
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
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataReads);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
            (ProjectionDeliveryRetryLease? lease, string etag) = await daprClient
                .GetStateAndETagAsync<ProjectionDeliveryRetryLease>(
                    storeName,
                    leaseKey,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            RecordLeaseBytes(lease, false);
            if (lease is not null
                && lease.ExpiresUtc > nowUtc
                && !string.Equals(lease.Owner, leaseOwner, StringComparison.Ordinal)) {
                return false;
            }

            var proposedLease = new ProjectionDeliveryRetryLease(leaseOwner, nowUtc + leaseDuration);
            RecordLeaseBytes(proposedLease, true);
            if (await daprClient.TrySaveStateAsync(
                    storeName,
                    leaseKey,
                    proposedLease,
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
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataReads);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
            (ProjectionDeliveryRetryLease? lease, string etag) = await daprClient
                .GetStateAndETagAsync<ProjectionDeliveryRetryLease>(
                    storeName,
                    leaseKey,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            RecordLeaseBytes(lease, false);
            if (lease is null || !string.Equals(lease.Owner, leaseOwner, StringComparison.Ordinal)) {
                return;
            }

            ProjectionDeliveryRetryLease released = lease with { ExpiresUtc = DateTimeOffset.MinValue };
            RecordLeaseBytes(released, true);
            if (await daprClient.TrySaveStateAsync(
                    storeName,
                    leaseKey,
                    released,
                    etag,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false)) {
                return;
            }
        }
    }

    private void RecordLeaseBytes(ProjectionDeliveryRetryLease? lease, bool write)
    {
        long bytes = lease is null ? 0 : JsonSerializer.SerializeToUtf8Bytes(lease, daprClient.JsonSerializerOptions).Length;
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataSizingBytes, bytes);
        ProjectionDeliveryCostDiagnostics.Record(write ? ProjectionDeliveryCost.MutationBytes : ProjectionDeliveryCost.MetadataReadBytes, bytes);
        if (write)
        {
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationAttempts);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SchedulerCasAttempts);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
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
            RecordMigrationReadAttempt();
            string? persistedMarker = await daprClient
                .GetStateAsync<string>(
                    stateStoreName,
                    ProtocolMarkerKey,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            RecordMigrationReadBody(persistedMarker);
            if (string.Equals(persistedMarker, ProtocolMarkerValue, StringComparison.Ordinal)) {
                Volatile.Write(ref _legacyMigrationCompleted, 1);
                return;
            }

            if (!string.IsNullOrWhiteSpace(persistedMarker)) {
                throw new InvalidOperationException("The projection delivery retry protocol marker is not recognized.");
            }

            RecordMigrationReadAttempt();
            (ProjectionDeliveryRetryLedger? legacy, string legacyEtag) = await daprClient
                .GetStateAndETagAsync<ProjectionDeliveryRetryLedger>(
                    stateStoreName,
                    LegacyLedgerKey,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            RecordMigrationReadBody(legacy);
            ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SchedulerMigrationEntries, legacy?.Items?.Count ?? 0);
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
                RecordMigrationWrite(0);
                ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.TransactionAttempts);
                await daprClient.ExecuteStateTransactionAsync(
                    stateStoreName,
                    [new StateTransactionRequest(LegacyLedgerKey, null, StateOperationType.Delete, legacyEtag)],
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            RecordMigrationWrite(MeasureBody(ProtocolMarkerValue));
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

    private static void RecordMigrationReadAttempt()
    {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SchedulerMigrationReads);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataReads);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
    }

    private void RecordMigrationReadBody<T>(T value)
    {
        long bytes = value is null ? 0 : MeasureBody(value);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SchedulerMigrationReadBytes, bytes);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MetadataReadBytes, bytes);
    }

    private static void RecordMigrationWrite(long bytes)
    {
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SchedulerMigrationWrites);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.SchedulerMigrationWriteBytes, bytes);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationAttempts);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.MutationBytes, bytes);
        ProjectionDeliveryCostDiagnostics.Record(ProjectionDeliveryCost.StateRpcAttempts);
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
        if (workItem.ReplayRecoveryBindings is { } bindings && bindings.Any(pair => string.IsNullOrWhiteSpace(pair.Key)
            || pair.Value is not { Length: 64 } || !pair.Value.All(static item => item is >= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new ArgumentException("Invalid frozen replay recovery binding.", nameof(workItem));
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
