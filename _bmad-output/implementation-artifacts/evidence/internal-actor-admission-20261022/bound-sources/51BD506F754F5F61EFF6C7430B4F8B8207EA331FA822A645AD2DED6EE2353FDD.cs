using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Server.Commands;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Commands;

/// <summary>
/// DAPR state store implementation of stream activity tracking.
/// Persists a single global admin stream activity index so the admin UI
/// Streams, Events, and Activity Feed pages show real data.
/// </summary>
public sealed class DaprStreamActivityTracker(
    DaprClient daprClient,
    IOptions<CommandStatusOptions> options,
    ILogger<DaprStreamActivityTracker> logger) : IStreamActivityTracker {
    private const string _activityIndexKey = "admin:stream-activity:all";
    private const string _storageHotStreamsPrefix = "admin:storage-hot-streams:";
    private const string _storageOverviewPrefix = "admin:storage-overview:";
    private const string _storageStreamInfoPrefix = "admin:storage-stream-info:";
    private const string _storageStreamCountPrefix = "admin:storage-stream-count:";
    private const int _maxEntries = 1000;
    private const int _maxEtagRetries = 3;
    private readonly string _stateStoreName = options.Value.StateStoreName;

    /// <inheritdoc/>
    public async Task TrackAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long newEventsAppended,
        DateTimeOffset timestamp,
        CancellationToken ct = default) {
        if (newEventsAppended <= 0) {
            return;
        }

        try {
            ArgumentNullException.ThrowIfNull(tenantId);

            bool saved = await TryUpsertActivityIndexAsync(
                tenantId, domain, aggregateId, newEventsAppended, timestamp, ct).ConfigureAwait(false);
            if (!saved) {
                logger.LogWarning(
                    "Failed to track stream activity after {MaxRetries} optimistic-concurrency attempts: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}",
                    _maxEtagRetries,
                    tenantId,
                    domain,
                    aggregateId);
                return;
            }

            (StreamStorageInfo? storageInfo, bool isNewStream) = await UpsertStreamStorageInfoAsync(
                tenantId,
                domain,
                aggregateId,
                newEventsAppended,
                ct).ConfigureAwait(false);
            if (storageInfo is not null) {
                await SaveStorageIndexesAsync(storageInfo, newEventsAppended, isNewStream, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            logger.LogError(
                ex,
                "Failed to track stream activity: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}, ExceptionType={ExceptionType}",
                tenantId,
                domain,
                aggregateId,
                ex.GetType().Name);
        }
    }

    private static bool MatchesIdentity(StreamSummary existing, string tenantId, string domain, string aggregateId)
        => existing.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase)
            && existing.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase)
            && existing.AggregateId.Equals(aggregateId, StringComparison.OrdinalIgnoreCase);

    private static List<StreamSummary> UpsertAndTrim(
        List<StreamSummary> entries,
        string tenantId,
        string domain,
        string aggregateId,
        long newEventsAppended,
        DateTimeOffset timestamp) {
        int index = entries.FindIndex(e => MatchesIdentity(e, tenantId, domain, aggregateId));
        if (index >= 0) {
            StreamSummary existing = entries[index];
            entries[index] = existing with {
                EventCount = existing.EventCount + newEventsAppended,
                LastEventSequence = existing.LastEventSequence + newEventsAppended,
                LastActivityUtc = timestamp,
                // TODO: Preserve non-Active StreamStatus when tombstoning is implemented
                StreamStatus = StreamStatus.Active,
            };
        }
        else {
            entries.Add(new StreamSummary(
                tenantId,
                domain,
                aggregateId,
                LastEventSequence: newEventsAppended,
                LastActivityUtc: timestamp,
                EventCount: newEventsAppended,
                HasSnapshot: false,
                StreamStatus: StreamStatus.Active));
        }

        return entries
            .OrderByDescending(e => e.LastActivityUtc)
            .Take(_maxEntries)
            .ToList();
    }

    private static string ToAggregateTypeName(string domain)
        => string.IsNullOrWhiteSpace(domain)
            ? "UnknownAggregate"
            : $"{char.ToUpperInvariant(domain[0])}{domain[1..]}Aggregate";

    private static StreamStorageInfo CreateStorageInfo(
        string tenantId,
        string domain,
        string aggregateId,
        long eventCount,
        bool hasSnapshot,
        TimeSpan? snapshotAge)
        => new(
            tenantId,
            domain,
            aggregateId,
            ToAggregateTypeName(domain),
            eventCount,
            SizeBytes: null,
            hasSnapshot,
            snapshotAge);

    private static StorageOverview AddToOverview(
        StorageOverview? existing,
        string tenantId,
        long newEventsAppended,
        bool isNewStream) {
        long totalEvents = (existing?.TotalEventCount ?? 0) + newEventsAppended;
        long totalStreams = (existing?.TotalStreamCount ?? 0) + (isNewStream ? 1 : 0);
        List<TenantStorageInfo> tenants = existing?.TenantBreakdown.ToList() ?? [];
        int tenantIndex = tenants.FindIndex(t => t.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase));
        if (tenantIndex >= 0) {
            TenantStorageInfo tenant = tenants[tenantIndex];
            tenants[tenantIndex] = new TenantStorageInfo(
                tenant.TenantId,
                tenant.EventCount + newEventsAppended,
                tenant.SizeBytes,
                tenant.GrowthRatePerDay);
        }
        else {
            tenants.Add(new TenantStorageInfo(tenantId, newEventsAppended, SizeBytes: null, GrowthRatePerDay: null));
        }

        return new StorageOverview(
            totalEvents,
            existing?.TotalSizeBytes,
            [.. tenants.OrderBy(t => t.TenantId, StringComparer.OrdinalIgnoreCase)],
            totalStreams,
            totalEvents > 0 ? StorageIndexStatus.Populated : StorageIndexStatus.Empty);
    }

    private static bool MatchesStorageIdentity(StreamStorageInfo existing, StreamStorageInfo updated)
        => existing.TenantId.Equals(updated.TenantId, StringComparison.OrdinalIgnoreCase)
            && existing.Domain.Equals(updated.Domain, StringComparison.OrdinalIgnoreCase)
            && existing.AggregateId.Equals(updated.AggregateId, StringComparison.OrdinalIgnoreCase);

    private static List<StreamStorageInfo> UpsertHotStream(IReadOnlyList<StreamStorageInfo>? existing, StreamStorageInfo updated)
        => [.. (existing ?? [])
            .Where(s => !MatchesStorageIdentity(s, updated))
            .Append(updated)
            .OrderByDescending(s => s.EventCount)
            .ThenBy(s => s.TenantId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Domain, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.AggregateId, StringComparer.OrdinalIgnoreCase)
            .Take(100)];

    private static StreamStorageInfo AddToStorageInfo(
        StreamStorageInfo? existing,
        string tenantId,
        string domain,
        string aggregateId,
        long newEventsAppended)
        => existing is null
            ? CreateStorageInfo(tenantId, domain, aggregateId, newEventsAppended, hasSnapshot: false, snapshotAge: null)
            : CreateStorageInfo(
                existing.TenantId,
                existing.Domain,
                existing.AggregateId,
                existing.EventCount + newEventsAppended,
                existing.HasSnapshot,
                existing.SnapshotAge);

    private async Task SaveStorageIndexesAsync(
        StreamStorageInfo storageInfo,
        long newEventsAppended,
        bool isNewStream,
        CancellationToken ct) {
        await SaveStorageScopeAsync("all", storageInfo, newEventsAppended, isNewStream, ct).ConfigureAwait(false);
        await SaveStorageScopeAsync(storageInfo.TenantId, storageInfo, newEventsAppended, isNewStream, ct).ConfigureAwait(false);
    }

    private async Task SaveStorageScopeAsync(
        string scope,
        StreamStorageInfo storageInfo,
        long newEventsAppended,
        bool isNewStream,
        CancellationToken ct) {
        await UpdateStorageOverviewAsync(scope, storageInfo.TenantId, newEventsAppended, isNewStream, ct).ConfigureAwait(false);
        await UpdateStorageStreamCountAsync(scope, isNewStream, ct).ConfigureAwait(false);
        await UpdateHotStreamsAsync(scope, storageInfo, ct).ConfigureAwait(false);
    }

    private async Task<(StreamStorageInfo? Info, bool IsNewStream)> UpsertStreamStorageInfoAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long newEventsAppended,
        CancellationToken ct) {
        string key = $"{_storageStreamInfoPrefix}{tenantId}:{domain}:{aggregateId}";
        for (int attempt = 0; attempt < _maxEtagRetries; attempt++) {
            (StreamStorageInfo? existing, string etag) = await daprClient
                .GetStateAndETagAsync<StreamStorageInfo>(_stateStoreName, key, cancellationToken: ct)
                .ConfigureAwait(false);
            StreamStorageInfo updated = AddToStorageInfo(existing, tenantId, domain, aggregateId, newEventsAppended);
            bool saved = await daprClient
                .TrySaveStateAsync(_stateStoreName, key, updated, etag, cancellationToken: ct)
                .ConfigureAwait(false);
            if (saved) {
                return (updated, existing is null);
            }

            logger.LogDebug(
                "ETag mismatch while updating storage stream info index '{IndexKey}', retry {Attempt}.",
                key,
                attempt + 1);
        }

        logger.LogWarning(
            "Failed to update storage stream info after {MaxRetries} optimistic-concurrency attempts: TenantId={TenantId}, Domain={Domain}, AggregateId={AggregateId}",
            _maxEtagRetries,
            tenantId,
            domain,
            aggregateId);
        return (null, false);
    }

    private async Task UpdateStorageOverviewAsync(
        string scope,
        string tenantId,
        long newEventsAppended,
        bool isNewStream,
        CancellationToken ct) {
        string key = $"{_storageOverviewPrefix}{scope}";
        for (int attempt = 0; attempt < _maxEtagRetries; attempt++) {
            (StorageOverview? existing, string etag) = await daprClient
                .GetStateAndETagAsync<StorageOverview>(_stateStoreName, key, cancellationToken: ct)
                .ConfigureAwait(false);
            StorageOverview updated = AddToOverview(existing, tenantId, newEventsAppended, isNewStream);
            bool saved = await daprClient
                .TrySaveStateAsync(_stateStoreName, key, updated, etag, cancellationToken: ct)
                .ConfigureAwait(false);
            if (saved) {
                return;
            }

            logger.LogDebug(
                "ETag mismatch while updating storage overview index '{IndexKey}', retry {Attempt}.",
                key,
                attempt + 1);
        }

        logger.LogWarning("Failed to update storage overview index '{IndexKey}' after {MaxRetries} optimistic-concurrency attempts.", key, _maxEtagRetries);
    }

    private async Task UpdateStorageStreamCountAsync(string scope, bool isNewStream, CancellationToken ct) {
        if (!isNewStream) {
            return;
        }

        string key = $"{_storageStreamCountPrefix}{scope}";
        for (int attempt = 0; attempt < _maxEtagRetries; attempt++) {
            (long? existing, string etag) = await daprClient
                .GetStateAndETagAsync<long?>(_stateStoreName, key, cancellationToken: ct)
                .ConfigureAwait(false);
            long updated = (existing ?? 0) + 1;
            bool saved = await daprClient
                .TrySaveStateAsync(_stateStoreName, key, updated, etag, cancellationToken: ct)
                .ConfigureAwait(false);
            if (saved) {
                return;
            }

            logger.LogDebug(
                "ETag mismatch while updating storage stream count index '{IndexKey}', retry {Attempt}.",
                key,
                attempt + 1);
        }

        logger.LogWarning("Failed to update storage stream count index '{IndexKey}' after {MaxRetries} optimistic-concurrency attempts.", key, _maxEtagRetries);
    }

    private async Task UpdateHotStreamsAsync(string scope, StreamStorageInfo storageInfo, CancellationToken ct) {
        string key = $"{_storageHotStreamsPrefix}{scope}";
        for (int attempt = 0; attempt < _maxEtagRetries; attempt++) {
            (List<StreamStorageInfo>? existing, string etag) = await daprClient
                .GetStateAndETagAsync<List<StreamStorageInfo>>(_stateStoreName, key, cancellationToken: ct)
                .ConfigureAwait(false);
            List<StreamStorageInfo> updated = UpsertHotStream(existing, storageInfo);
            bool saved = await daprClient
                .TrySaveStateAsync(_stateStoreName, key, updated, etag, cancellationToken: ct)
                .ConfigureAwait(false);
            if (saved) {
                return;
            }

            logger.LogDebug(
                "ETag mismatch while updating storage hot streams index '{IndexKey}', retry {Attempt}.",
                key,
                attempt + 1);
        }

        logger.LogWarning("Failed to update storage hot streams index '{IndexKey}' after {MaxRetries} optimistic-concurrency attempts.", key, _maxEtagRetries);
    }

    private async Task<bool> TryUpsertActivityIndexAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long newEventsAppended,
        DateTimeOffset timestamp,
        CancellationToken ct) {
        for (int attempt = 0; attempt < _maxEtagRetries; attempt++) {
            try {
                (List<StreamSummary>? existing, string etag) = await daprClient
                    .GetStateAndETagAsync<List<StreamSummary>>(_stateStoreName, _activityIndexKey, cancellationToken: ct)
                    .ConfigureAwait(false);

                List<StreamSummary> updated = UpsertAndTrim(
                    existing ?? [], tenantId, domain, aggregateId, newEventsAppended, timestamp);
                bool saved = await daprClient
                    .TrySaveStateAsync(_stateStoreName, _activityIndexKey, updated, etag, cancellationToken: ct)
                    .ConfigureAwait(false);

                if (saved) {
                    return true;
                }

                logger.LogDebug(
                    "ETag mismatch while updating stream activity index '{IndexKey}', retry {Attempt}.",
                    _activityIndexKey,
                    attempt + 1);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) when (attempt < _maxEtagRetries - 1) {
                logger.LogDebug(
                    ex,
                    "Retry {Attempt} while updating stream activity index '{IndexKey}'.",
                    attempt + 1,
                    _activityIndexKey);
            }
        }

        return false;
    }
}
