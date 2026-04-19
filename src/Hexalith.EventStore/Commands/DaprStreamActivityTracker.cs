using Dapr.Client;

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
