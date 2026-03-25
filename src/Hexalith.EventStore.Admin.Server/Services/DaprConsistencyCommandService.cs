using Dapr.Client;

using System.Text.Json;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;
using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// DAPR-backed implementation of <see cref="IConsistencyCommandService"/>.
/// Triggers and cancels consistency checks using the state store for coordination.
/// </summary>
public sealed class DaprConsistencyCommandService : IConsistencyCommandService
{
    private const int AnomalyCap = 500;
    private const string CheckKeyPrefix = "admin:consistency:";
    private const int IndexCap = 100;
    private const string IndexKey = "admin:consistency:index";
    private const int MaxETagRetries = 3;
    private const int TimeoutMinutes = 30;
    private const int TtlDays = 30;

    private readonly DaprClient _daprClient;
    private readonly ILogger<DaprConsistencyCommandService> _logger;
    private readonly AdminServerOptions _options;
    private readonly IStreamQueryService _streamQueryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprConsistencyCommandService"/> class.
    /// </summary>
    /// <param name="daprClient">The DAPR client.</param>
    /// <param name="streamQueryService">The stream query service for aggregate discovery.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="logger">The logger.</param>
    public DaprConsistencyCommandService(
        DaprClient daprClient,
        IStreamQueryService streamQueryService,
        IOptions<AdminServerOptions> options,
        ILogger<DaprConsistencyCommandService> logger)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(streamQueryService);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _daprClient = daprClient;
        _streamQueryService = streamQueryService;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<AdminOperationResult> CancelCheckAsync(
        string checkId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkId);
        string key = $"{CheckKeyPrefix}{checkId}";

        try
        {
            ConsistencyCheckResult? existing = await _daprClient
                .GetStateAsync<ConsistencyCheckResult>(_options.StateStoreName, key, cancellationToken: ct)
                .ConfigureAwait(false);

            if (existing is null)
            {
                return new AdminOperationResult(false, checkId, "Consistency check not found.", "NotFound");
            }

            if (existing.Status is not (ConsistencyCheckStatus.Pending or ConsistencyCheckStatus.Running))
            {
                return new AdminOperationResult(false, checkId, $"Cannot cancel a check with status '{existing.Status}'.", "InvalidOperation");
            }

            ConsistencyCheckResult cancelled = existing with
            {
                Status = ConsistencyCheckStatus.Cancelled,
                CompletedAtUtc = DateTimeOffset.UtcNow,
            };

            await SaveCheckResultAsync(key, cancelled, ct).ConfigureAwait(false);

            _logger.LogInformation("Consistency check '{CheckId}' cancelled.", checkId);
            return new AdminOperationResult(true, checkId, "Consistency check cancelled.", null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel consistency check '{CheckId}'.", checkId);
            return new AdminOperationResult(false, checkId, "Failed to cancel consistency check.", "InternalError");
        }
    }

    /// <inheritdoc/>
    public async Task<AdminOperationResult> TriggerCheckAsync(
        string? tenantId,
        string? domain,
        IReadOnlyList<ConsistencyCheckType> checkTypes,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(checkTypes);
        if (checkTypes.Count == 0)
        {
            return new AdminOperationResult(false, string.Empty, "At least one consistency check type must be selected.", "InvalidOperation");
        }

        string checkId = UniqueIdHelper.GenerateSortableUniqueStringId();

        try
        {
            // Concurrency guard: check for active check on same tenant
            if (await HasActiveCheckForTenantAsync(tenantId, ct).ConfigureAwait(false))
            {
                return new AdminOperationResult(false, checkId, "A check is already active for this tenant.", "Conflict");
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset timeoutUtc = now + TimeSpan.FromMinutes(TimeoutMinutes);

            ConsistencyCheckResult initialResult = new(
                checkId,
                ConsistencyCheckStatus.Pending,
                tenantId,
                domain,
                checkTypes,
                StartedAtUtc: now,
                CompletedAtUtc: null,
                TimeoutUtc: timeoutUtc,
                StreamsChecked: 0,
                AnomaliesFound: 0,
                Anomalies: [],
                Truncated: false,
                ErrorMessage: null);

            string key = $"{CheckKeyPrefix}{checkId}";
            await SaveCheckResultAsync(key, initialResult, ct).ConfigureAwait(false);
            await AppendToIndexAsync(checkId, ct).ConfigureAwait(false);

            // Fire background scan
            _ = Task.Run(() => RunBackgroundScanAsync(checkId, tenantId, domain, checkTypes), CancellationToken.None);

            _logger.LogInformation("Consistency check '{CheckId}' triggered for tenant '{TenantId}', domain '{Domain}'.", checkId, tenantId ?? "all", domain ?? "all");
            return new AdminOperationResult(true, checkId, "Consistency check started.", null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger consistency check.");
            return new AdminOperationResult(false, checkId, "Failed to trigger consistency check.", "InternalError");
        }
    }

    private async Task AppendToIndexAsync(string checkId, CancellationToken ct)
    {
        for (int attempt = 0; attempt < MaxETagRetries; attempt++)
        {
            try
            {
                (List<string>? existing, string etag) = await _daprClient
                    .GetStateAndETagAsync<List<string>>(_options.StateStoreName, IndexKey, cancellationToken: ct)
                    .ConfigureAwait(false);

                List<string> index = existing ?? [];
                index.Insert(0, checkId);

                // Cap at IndexCap entries — drop oldest
                while (index.Count > IndexCap)
                {
                    index.RemoveAt(index.Count - 1);
                }

                bool saved = await _daprClient
                    .TrySaveStateAsync(_options.StateStoreName, IndexKey, index, etag, cancellationToken: ct)
                    .ConfigureAwait(false);

                if (saved)
                {
                    return;
                }

                _logger.LogDebug("ETag mismatch on consistency index update, retry {Attempt}.", attempt + 1);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < MaxETagRetries - 1)
            {
                _logger.LogDebug(ex, "Retry {Attempt} for consistency index update.", attempt + 1);
            }
        }

        _logger.LogWarning("Failed to update consistency index after {MaxRetries} retries.", MaxETagRetries);
    }

    private async Task<bool> HasActiveCheckForTenantAsync(string? tenantId, CancellationToken ct)
    {
        try
        {
            List<string>? index = await _daprClient
                .GetStateAsync<List<string>>(_options.StateStoreName, IndexKey, cancellationToken: ct)
                .ConfigureAwait(false);

            if (index is null or { Count: 0 })
            {
                return false;
            }

            foreach (string existingCheckId in index)
            {
                string key = $"{CheckKeyPrefix}{existingCheckId}";
                ConsistencyCheckResult? check = await _daprClient
                    .GetStateAsync<ConsistencyCheckResult>(_options.StateStoreName, key, cancellationToken: ct)
                    .ConfigureAwait(false);

                if (check is null)
                {
                    continue;
                }

                if (check.Status is ConsistencyCheckStatus.Pending or ConsistencyCheckStatus.Running
                    && check.TenantId == tenantId)
                {
                    // Also check for timeout
                    if (check.Status == ConsistencyCheckStatus.Running && DateTimeOffset.UtcNow > check.TimeoutUtc)
                    {
                        continue; // Timed out — not active
                    }

                    return true;
                }
            }

            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for active consistency checks. Allowing trigger.");
            return false;
        }
    }

    private async Task RunBackgroundScanAsync(
        string checkId,
        string? tenantId,
        string? domain,
        IReadOnlyList<ConsistencyCheckType> checkTypes)
    {
        string key = $"{CheckKeyPrefix}{checkId}";
        try
        {
            // Update status to Running
            ConsistencyCheckResult? current = await _daprClient
                .GetStateAsync<ConsistencyCheckResult>(_options.StateStoreName, key)
                .ConfigureAwait(false);

            if (current is null || current.Status == ConsistencyCheckStatus.Cancelled)
            {
                return;
            }

            current = current with { Status = ConsistencyCheckStatus.Running };
            await SaveCheckResultAsync(key, current, CancellationToken.None).ConfigureAwait(false);

            // Sanity check: verify state store is accessible
            PagedResult<StreamSummary> sanityCheck = await _streamQueryService
                .GetRecentlyActiveStreamsAsync(tenantId, domain, count: 1)
                .ConfigureAwait(false);

            if (sanityCheck is null)
            {
                await FailCheckAsync(key, current, "State store read returned null — verify DAPR configuration.").ConfigureAwait(false);
                return;
            }

            // Discover all aggregate streams
            PagedResult<StreamSummary> streams = await _streamQueryService
                .GetRecentlyActiveStreamsAsync(tenantId, domain, count: 10000)
                .ConfigureAwait(false);

            bool projectionCheckRequested = checkTypes.Contains(ConsistencyCheckType.ProjectionPositions);
            IReadOnlyList<ConsistencyCheckType> effectiveCheckTypes = checkTypes
                .Where(t => t != ConsistencyCheckType.ProjectionPositions)
                .ToList();

            List<ConsistencyAnomaly> anomalies = [];
            int totalAnomaliesFound = 0;
            int streamsChecked = 0;

            if (projectionCheckRequested)
            {
                List<ConsistencyAnomaly> projectionAnomalies = await CheckProjectionPositionsAsync(tenantId, domain).ConfigureAwait(false);
                totalAnomaliesFound += projectionAnomalies.Count;
                anomalies.AddRange(projectionAnomalies);
            }

            foreach (StreamSummary stream in streams.Items)
            {
                // Check if cancelled
                ConsistencyCheckResult? latestState = await _daprClient
                    .GetStateAsync<ConsistencyCheckResult>(_options.StateStoreName, key)
                    .ConfigureAwait(false);

                if (latestState?.Status == ConsistencyCheckStatus.Cancelled)
                {
                    return;
                }

                List<ConsistencyAnomaly> streamAnomalies = await CheckStreamAsync(
                    stream, effectiveCheckTypes).ConfigureAwait(false);

                totalAnomaliesFound += streamAnomalies.Count;

                if (anomalies.Count < AnomalyCap)
                {
                    anomalies.AddRange(streamAnomalies);
                }

                streamsChecked++;

                // Periodic progress update (every 50 streams)
                if (streamsChecked % 50 == 0)
                {
                    current = current with
                    {
                        StreamsChecked = streamsChecked,
                        AnomaliesFound = totalAnomaliesFound,
                    };
                    await SaveCheckResultAsync(key, current, CancellationToken.None).ConfigureAwait(false);
                }
            }

            // Sort anomalies by severity (Critical > Error > Warning) and truncate
            anomalies = [.. anomalies
                .OrderByDescending(a => a.Severity)
                .Take(AnomalyCap)];

            bool truncated = totalAnomaliesFound > AnomalyCap;

            ConsistencyCheckResult completed = current with
            {
                Status = ConsistencyCheckStatus.Completed,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                StreamsChecked = streamsChecked,
                AnomaliesFound = totalAnomaliesFound,
                Anomalies = anomalies,
                Truncated = truncated,
            };

            await SaveCheckResultAsync(key, completed, CancellationToken.None).ConfigureAwait(false);
            _logger.LogInformation(
                "Consistency check '{CheckId}' completed. Streams: {Streams}, Anomalies: {Anomalies}.",
                checkId, streamsChecked, totalAnomaliesFound);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Consistency check '{CheckId}' failed.", checkId);
            try
            {
                ConsistencyCheckResult? current = await _daprClient
                    .GetStateAsync<ConsistencyCheckResult>(_options.StateStoreName, key)
                    .ConfigureAwait(false);

                if (current is not null)
                {
                    await FailCheckAsync(key, current, ex.Message).ConfigureAwait(false);
                }
            }
            catch (Exception inner)
            {
                _logger.LogError(inner, "Failed to update check '{CheckId}' to Failed status.", checkId);
            }
        }
    }

    private async Task<List<ConsistencyAnomaly>> CheckStreamAsync(
        StreamSummary stream,
        IReadOnlyList<ConsistencyCheckType> checkTypes)
    {
        List<ConsistencyAnomaly> anomalies = [];

        foreach (ConsistencyCheckType checkType in checkTypes)
        {
            try
            {
                switch (checkType)
                {
                    case ConsistencyCheckType.SequenceContinuity:
                        await CheckSequenceContinuityAsync(stream, anomalies).ConfigureAwait(false);
                        break;
                    case ConsistencyCheckType.SnapshotIntegrity:
                        await CheckSnapshotIntegrityAsync(stream, anomalies).ConfigureAwait(false);
                        break;
                    case ConsistencyCheckType.MetadataConsistency:
                        await CheckMetadataConsistencyAsync(stream, anomalies).ConfigureAwait(false);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error checking {CheckType} for stream {Tenant}:{Domain}:{Aggregate}.",
                    checkType, stream.TenantId, stream.Domain, stream.AggregateId);
            }
        }

        return anomalies;
    }

    private async Task<List<ConsistencyAnomaly>> CheckProjectionPositionsAsync(string? tenantId, string? domain)
    {
        List<ConsistencyAnomaly> anomalies = [];
        string scope = tenantId ?? "all";
        string projectionIndexKey = $"admin:projections:{scope}";
        string storageOverviewKey = $"admin:storage-overview:{scope}";

        try
        {
            List<ProjectionStatus>? projections = await _daprClient
                .GetStateAsync<List<ProjectionStatus>>(_options.StateStoreName, projectionIndexKey)
                .ConfigureAwait(false);

            StorageOverview? overview = await _daprClient
                .GetStateAsync<StorageOverview>(_options.StateStoreName, storageOverviewKey)
                .ConfigureAwait(false);

            if (projections is null)
            {
                anomalies.Add(new ConsistencyAnomaly(
                    UniqueIdHelper.GenerateSortableUniqueStringId(),
                    ConsistencyCheckType.ProjectionPositions,
                    AnomalySeverity.Warning,
                    scope,
                    domain ?? "all",
                    "all",
                    "Projection status index is unavailable.",
                    "Could not load admin:projections index to validate projection positions.",
                    null,
                    null));
                return anomalies;
            }

            long? totalEvents = overview?.TotalEventCount;
            foreach (ProjectionStatus projection in projections)
            {
                if (projection.Lag > 1000)
                {
                    anomalies.Add(new ConsistencyAnomaly(
                        UniqueIdHelper.GenerateSortableUniqueStringId(),
                        ConsistencyCheckType.ProjectionPositions,
                        AnomalySeverity.Warning,
                        projection.TenantId,
                        domain ?? "all",
                        $"projection:{projection.Name}",
                        $"Projection lag is high ({projection.Lag}).",
                        "Lag above 1000 indicates potential projection staleness.",
                        1000,
                        projection.Lag));
                }

                if (totalEvents is long eventCount && projection.LastProcessedPosition > eventCount)
                {
                    anomalies.Add(new ConsistencyAnomaly(
                        UniqueIdHelper.GenerateSortableUniqueStringId(),
                        ConsistencyCheckType.ProjectionPositions,
                        AnomalySeverity.Error,
                        projection.TenantId,
                        domain ?? "all",
                        $"projection:{projection.Name}",
                        "Projection position is ahead of available event count.",
                        null,
                        eventCount,
                        projection.LastProcessedPosition));
                }
            }

            if (domain is not null)
            {
                anomalies.Add(new ConsistencyAnomaly(
                    UniqueIdHelper.GenerateSortableUniqueStringId(),
                    ConsistencyCheckType.ProjectionPositions,
                    AnomalySeverity.Warning,
                    scope,
                    domain,
                    "all",
                    "Domain-specific projection position validation is not granular.",
                    "Projection indexes are tenant-scoped and do not include domain-level position partitioning.",
                    null,
                    null));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to validate projection positions for scope '{Scope}'.", scope);
            anomalies.Add(new ConsistencyAnomaly(
                UniqueIdHelper.GenerateSortableUniqueStringId(),
                ConsistencyCheckType.ProjectionPositions,
                AnomalySeverity.Warning,
                scope,
                domain ?? "all",
                "all",
                "Projection position verification failed.",
                ex.Message,
                null,
                null));
        }

        return anomalies;
    }

    private async Task CheckSequenceContinuityAsync(StreamSummary stream, List<ConsistencyAnomaly> anomalies)
    {
        if (stream.LastEventSequence <= 0)
        {
            return;
        }

        for (long sequence = 1; sequence <= stream.LastEventSequence; sequence++)
        {
            string eventKey = $"{stream.TenantId}:{stream.Domain}:{stream.AggregateId}:events:{sequence}";
            try
            {
                string? evt = await _daprClient
                    .GetStateAsync<string>(_options.StateStoreName, eventKey)
                    .ConfigureAwait(false);

                if (evt is null)
                {
                    anomalies.Add(new ConsistencyAnomaly(
                        UniqueIdHelper.GenerateSortableUniqueStringId(),
                        ConsistencyCheckType.SequenceContinuity,
                        sequence == 1 ? AnomalySeverity.Critical : AnomalySeverity.Error,
                        stream.TenantId,
                        stream.Domain,
                        stream.AggregateId,
                        $"Missing event at sequence {sequence}.",
                        null,
                        ExpectedSequence: sequence,
                        ActualSequence: null));
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to read event key '{Key}'.", eventKey);
            }
        }

        if (stream.EventCount != stream.LastEventSequence)
        {
            anomalies.Add(new ConsistencyAnomaly(
                UniqueIdHelper.GenerateSortableUniqueStringId(),
                ConsistencyCheckType.SequenceContinuity,
                AnomalySeverity.Error,
                stream.TenantId,
                stream.Domain,
                stream.AggregateId,
                "Event metadata mismatch between EventCount and LastEventSequence.",
                null,
                stream.LastEventSequence,
                stream.EventCount));
        }
    }

    private async Task CheckSnapshotIntegrityAsync(StreamSummary stream, List<ConsistencyAnomaly> anomalies)
    {
        // Check: no snapshot for aggregate with > 100 events
        if (!stream.HasSnapshot && stream.EventCount > 100)
        {
            anomalies.Add(new ConsistencyAnomaly(
                UniqueIdHelper.GenerateSortableUniqueStringId(),
                ConsistencyCheckType.SnapshotIntegrity,
                AnomalySeverity.Warning,
                stream.TenantId,
                stream.Domain,
                stream.AggregateId,
                $"No snapshot exists for aggregate with {stream.EventCount} events.",
                null,
                ExpectedSequence: null,
                ActualSequence: null));
        }

        if (!stream.HasSnapshot)
        {
            return;
        }

        string snapshotKey = $"{stream.TenantId}:{stream.Domain}:{stream.AggregateId}:snapshot";
        try
        {
            object? snapshot = await _daprClient
                .GetStateAsync<object>(_options.StateStoreName, snapshotKey)
                .ConfigureAwait(false);
            if (TryExtractLong(snapshot, "sequenceNumber", "SequenceNumber", "sequence", "Sequence", "lastSequence", "LastSequence", "position", "Position") is long snapshotSequence
                && snapshotSequence > stream.LastEventSequence)
            {
                anomalies.Add(new ConsistencyAnomaly(
                    UniqueIdHelper.GenerateSortableUniqueStringId(),
                    ConsistencyCheckType.SnapshotIntegrity,
                    AnomalySeverity.Error,
                    stream.TenantId,
                    stream.Domain,
                    stream.AggregateId,
                    "Snapshot sequence is ahead of latest event sequence.",
                    null,
                    stream.LastEventSequence,
                    snapshotSequence));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read snapshot key '{Key}'.", snapshotKey);
        }
    }

    private async Task CheckMetadataConsistencyAsync(StreamSummary stream, List<ConsistencyAnomaly> anomalies)
    {
        string metadataKey = $"{stream.TenantId}:{stream.Domain}:{stream.AggregateId}:metadata";
        try
        {
            object? metadata = await _daprClient
                .GetStateAsync<object>(_options.StateStoreName, metadataKey)
                .ConfigureAwait(false);

            if (metadata is null && stream.EventCount > 0)
            {
                anomalies.Add(new ConsistencyAnomaly(
                    UniqueIdHelper.GenerateSortableUniqueStringId(),
                    ConsistencyCheckType.MetadataConsistency,
                    AnomalySeverity.Error,
                    stream.TenantId,
                    stream.Domain,
                    stream.AggregateId,
                    "Aggregate metadata is missing.",
                    null,
                    ExpectedSequence: stream.EventCount,
                    ActualSequence: null));
            }

            long? metadataCount = TryExtractLong(metadata, "eventCount", "EventCount", "lastSequence", "LastSequence", "sequence", "Sequence");
            if (metadataCount is not null && metadataCount.Value != stream.EventCount)
            {
                anomalies.Add(new ConsistencyAnomaly(
                    UniqueIdHelper.GenerateSortableUniqueStringId(),
                    ConsistencyCheckType.MetadataConsistency,
                    AnomalySeverity.Error,
                    stream.TenantId,
                    stream.Domain,
                    stream.AggregateId,
                    "Metadata count does not match actual event count.",
                    null,
                    stream.EventCount,
                    metadataCount.Value));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read metadata key '{Key}'.", metadataKey);
        }
    }

    private static long? TryExtractLong(object? source, params string[] candidateNames)
    {
        if (source is null)
        {
            return null;
        }

        if (source is JsonElement json)
        {
            foreach (string name in candidateNames)
            {
                if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty(name, out JsonElement property))
                {
                    if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out long number))
                    {
                        return number;
                    }

                    if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out long parsed))
                    {
                        return parsed;
                    }
                }
            }

            return null;
        }

        Type type = source.GetType();
        foreach (string name in candidateNames)
        {
            System.Reflection.PropertyInfo? property = type.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (property is null)
            {
                continue;
            }

            object? value = property.GetValue(source);
            if (value is null)
            {
                continue;
            }

            if (value is long longValue)
            {
                return longValue;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            if (long.TryParse(value.ToString(), out long parsedValue))
            {
                return parsedValue;
            }
        }

        return null;
    }

    private async Task FailCheckAsync(string key, ConsistencyCheckResult current, string errorMessage)
    {
        ConsistencyCheckResult failed = current with
        {
            Status = ConsistencyCheckStatus.Failed,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            ErrorMessage = errorMessage,
        };
        await SaveCheckResultAsync(key, failed, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task SaveCheckResultAsync(string key, ConsistencyCheckResult result, CancellationToken ct)
    {
        Dictionary<string, string> metadata = new()
        {
            ["ttlInSeconds"] = (TtlDays * 24 * 60 * 60).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        await _daprClient
            .SaveStateAsync(_options.StateStoreName, key, result, metadata: metadata, cancellationToken: ct)
            .ConfigureAwait(false);
    }
}
