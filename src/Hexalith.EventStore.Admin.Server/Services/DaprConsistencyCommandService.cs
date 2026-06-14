using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

using Dapr.Client;

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
/// <remarks>
/// DW12 decision record: sequence-continuity and metadata-consistency diagnostics depend
/// on public EventStore query contracts (<see cref="IStreamQueryService"/>), not on the
/// physical DAPR actor-state layout. Probing reconstructed <c>{tenant}:{domain}:{aggregate}:events:{seq}</c>
/// or <c>:metadata</c> keys would break actor isolation and produced false positives for
/// actor-backed streams. Checks may become Inconclusive when supported contracts cannot
/// cover the required range, but they must never infer correctness from private storage keys.
/// </remarks>
public sealed class DaprConsistencyCommandService : IConsistencyCommandService {
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> ActiveScans = new(StringComparer.Ordinal);

    private const int AnomalyCap = 500;
    private const string CheckKeyPrefix = "admin:consistency:";
    private const int IndexCap = 100;
    private const string IndexKey = "admin:consistency:index";
    private const int MaxETagRetries = 3;
    private const int TimeoutMinutes = 30;
    private const int TtlDays = 30;
    private const int TimelinePageSize = 1_000;
    private const int MaxTimelinePages = 32;

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
        ILogger<DaprConsistencyCommandService> logger) {
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
        CancellationToken ct = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkId);
        string key = $"{CheckKeyPrefix}{checkId}";

        try {
            ConsistencyCheckResult? existing = await _daprClient
                .GetStateAsync<ConsistencyCheckResult>(_options.StateStoreName, key, cancellationToken: ct)
                .ConfigureAwait(false);

            if (existing is null) {
                return new AdminOperationResult(false, checkId, "Consistency check not found.", "NotFound");
            }

            if (existing.Status is not (ConsistencyCheckStatus.Pending or ConsistencyCheckStatus.Running)) {
                return new AdminOperationResult(false, checkId, $"Cannot cancel a check with status '{existing.Status}'.", "InvalidOperation");
            }

            ConsistencyCheckResult cancelled = existing with {
                Status = ConsistencyCheckStatus.Cancelled,
                CompletedAtUtc = DateTimeOffset.UtcNow,
            };

            await SaveCheckResultAsync(key, cancelled, ct).ConfigureAwait(false);
            if (ActiveScans.TryGetValue(checkId, out CancellationTokenSource? activeScan)) {
                await activeScan.CancelAsync().ConfigureAwait(false);
            }

            _logger.LogInformation("Consistency check '{CheckId}' cancelled.", checkId);
            return new AdminOperationResult(true, checkId, "Consistency check cancelled.", null);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to cancel consistency check '{CheckId}'.", checkId);
            return new AdminOperationResult(false, checkId, "Failed to cancel consistency check.", "InternalError");
        }
    }

    /// <inheritdoc/>
    public async Task<AdminOperationResult> TriggerCheckAsync(
        string? tenantId,
        string? domain,
        IReadOnlyList<ConsistencyCheckType> checkTypes,
        CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(checkTypes);
        if (checkTypes.Count == 0) {
            return new AdminOperationResult(false, string.Empty, "At least one consistency check type must be selected.", "InvalidOperation");
        }

        string checkId = UniqueIdHelper.GenerateSortableUniqueStringId();

        try {
            // Concurrency guard: check for active check on same tenant
            if (await HasActiveCheckForTenantAsync(tenantId, ct).ConfigureAwait(false)) {
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
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to trigger consistency check.");
            return new AdminOperationResult(false, checkId, "Failed to trigger consistency check.", "InternalError");
        }
    }

    private async Task AppendToIndexAsync(string checkId, CancellationToken ct) {
        for (int attempt = 0; attempt < MaxETagRetries; attempt++) {
            try {
                (List<string>? existing, string etag) = await _daprClient
                    .GetStateAndETagAsync<List<string>>(_options.StateStoreName, IndexKey, cancellationToken: ct)
                    .ConfigureAwait(false);

                List<string> index = existing ?? [];
                index.Insert(0, checkId);

                // Cap at IndexCap entries — drop oldest
                while (index.Count > IndexCap) {
                    index.RemoveAt(index.Count - 1);
                }

                bool saved = await _daprClient
                    .TrySaveStateAsync(_options.StateStoreName, IndexKey, index, etag, cancellationToken: ct)
                    .ConfigureAwait(false);

                if (saved) {
                    return;
                }

                _logger.LogDebug("ETag mismatch on consistency index update, retry {Attempt}.", attempt + 1);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) when (attempt < MaxETagRetries - 1) {
                _logger.LogDebug(ex, "Retry {Attempt} for consistency index update.", attempt + 1);
            }
        }

        _logger.LogWarning("Failed to update consistency index after {MaxRetries} retries.", MaxETagRetries);
    }

    private async Task<bool> HasActiveCheckForTenantAsync(string? tenantId, CancellationToken ct) {
        try {
            List<string>? index = await _daprClient
                .GetStateAsync<List<string>>(_options.StateStoreName, IndexKey, cancellationToken: ct)
                .ConfigureAwait(false);

            if (index is null or { Count: 0 }) {
                return false;
            }

            foreach (string existingCheckId in index) {
                string key = $"{CheckKeyPrefix}{existingCheckId}";
                ConsistencyCheckResult? check = await _daprClient
                    .GetStateAsync<ConsistencyCheckResult>(_options.StateStoreName, key, cancellationToken: ct)
                    .ConfigureAwait(false);

                if (check is null) {
                    continue;
                }

                if (check.Status is ConsistencyCheckStatus.Pending or ConsistencyCheckStatus.Running
                    && check.TenantId == tenantId) {
                    // Also check for timeout
                    if (check.Status == ConsistencyCheckStatus.Running && DateTimeOffset.UtcNow > check.TimeoutUtc) {
                        continue; // Timed out — not active
                    }

                    return true;
                }
            }

            return false;
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to check for active consistency checks. Allowing trigger.");
            return false;
        }
    }

    private async Task RunBackgroundScanAsync(
        string checkId,
        string? tenantId,
        string? domain,
        IReadOnlyList<ConsistencyCheckType> checkTypes) {
        string key = $"{CheckKeyPrefix}{checkId}";
        using CancellationTokenSource scanCts = new();
        _ = ActiveScans.TryAdd(checkId, scanCts);
        CancellationToken scanToken = scanCts.Token;
        try {
            // Update status to Running
            ConsistencyCheckResult? current = await _daprClient
                .GetStateAsync<ConsistencyCheckResult>(_options.StateStoreName, key)
                .ConfigureAwait(false);

            if (current is null || current.Status == ConsistencyCheckStatus.Cancelled) {
                return;
            }

            current = current with { Status = ConsistencyCheckStatus.Running };
            await SaveCheckResultAsync(key, current, scanToken).ConfigureAwait(false);

            // Sanity check: verify state store is accessible
            PagedResult<StreamSummary> sanityCheck = await _streamQueryService
                .GetRecentlyActiveStreamsAsync(tenantId, domain, count: 1, ct: scanToken)
                .ConfigureAwait(false);

            if (sanityCheck is null) {
                await FailCheckAsync(key, current, "State store read returned null — verify DAPR configuration.").ConfigureAwait(false);
                return;
            }

            // Discover all aggregate streams
            PagedResult<StreamSummary> streams = await _streamQueryService
                .GetRecentlyActiveStreamsAsync(tenantId, domain, count: 10000, ct: scanToken)
                .ConfigureAwait(false);

            bool projectionCheckRequested = checkTypes.Contains(ConsistencyCheckType.ProjectionPositions);
            IReadOnlyList<ConsistencyCheckType> effectiveCheckTypes = checkTypes
                .Where(t => t != ConsistencyCheckType.ProjectionPositions)
                .ToList();

            List<ConsistencyAnomaly> anomalies = [];
            int totalAnomaliesFound = 0;
            int streamsChecked = 0;

            if (projectionCheckRequested) {
                List<ConsistencyAnomaly> projectionAnomalies = await CheckProjectionPositionsAsync(tenantId, domain).ConfigureAwait(false);
                totalAnomaliesFound += projectionAnomalies.Count;
                AddAnomaliesWithinCap(anomalies, projectionAnomalies);
            }

            foreach (StreamSummary stream in streams.Items) {
                scanToken.ThrowIfCancellationRequested();

                // Check if cancelled
                ConsistencyCheckResult? latestState = await _daprClient
                    .GetStateAsync<ConsistencyCheckResult>(_options.StateStoreName, key, cancellationToken: scanToken)
                    .ConfigureAwait(false);

                if (latestState?.Status == ConsistencyCheckStatus.Cancelled) {
                    await scanCts.CancelAsync().ConfigureAwait(false);
                    return;
                }

                ConsistencyStreamCheckOutcome outcome = await CheckStreamAsync(
                    stream, effectiveCheckTypes, scanToken).ConfigureAwait(false);

                totalAnomaliesFound += outcome.Anomalies.Count;
                AddAnomaliesWithinCap(anomalies, outcome.Anomalies);

                streamsChecked++;

                // Periodic progress update (every 50 streams)
                if (streamsChecked % 50 == 0) {
                    current = current with {
                        StreamsChecked = streamsChecked,
                        AnomaliesFound = totalAnomaliesFound,
                    };
                    await SaveCheckResultAsync(key, current, scanToken).ConfigureAwait(false);
                }
            }

            // Sort anomalies by severity (Critical > Error > Warning) and truncate
            anomalies = [.. anomalies
                .OrderByDescending(a => a.Severity)
                .Take(AnomalyCap)];

            bool truncated = totalAnomaliesFound > AnomalyCap;

            ConsistencyCheckResult completed = current with {
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
        catch (OperationCanceledException) when (scanToken.IsCancellationRequested) {
            _logger.LogInformation("Consistency check '{CheckId}' scan cancelled.", checkId);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Consistency check '{CheckId}' failed.", checkId);
            try {
                ConsistencyCheckResult? current = await _daprClient
                    .GetStateAsync<ConsistencyCheckResult>(_options.StateStoreName, key)
                    .ConfigureAwait(false);

                if (current is not null) {
                    await FailCheckAsync(key, current, ex.Message).ConfigureAwait(false);
                }
            }
            catch (Exception inner) {
                _logger.LogError(inner, "Failed to update check '{CheckId}' to Failed status.", checkId);
            }
        }
        finally {
            _ = ActiveScans.TryRemove(checkId, out _);
        }
    }

    internal async Task<ConsistencyStreamCheckOutcome> CheckStreamAsync(
        StreamSummary stream,
        IReadOnlyList<ConsistencyCheckType> checkTypes,
        CancellationToken ct) {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(checkTypes);

        List<ConsistencyAnomaly> anomalies = [];
        SequenceRange? evaluatedRange = null;
        long evaluatedEventCount = 0;

        bool streamReadCompleted = false;
        if (checkTypes.Contains(ConsistencyCheckType.SequenceContinuity)) {
            try {
                ContinuityResult continuity = await CheckSequenceContinuityAsync(stream, anomalies, ct).ConfigureAwait(false);
                evaluatedRange = continuity.EvaluatedRange;
                evaluatedEventCount = continuity.EvaluatedEventCount;
                streamReadCompleted = continuity.StreamReadCompleted;
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                _logger.LogDebug(ex, "Error checking {CheckType} for stream {Tenant}:{Domain}:{Aggregate}.",
                    ConsistencyCheckType.SequenceContinuity, stream.TenantId, stream.Domain, stream.AggregateId);
            }
        }

        foreach (ConsistencyCheckType checkType in checkTypes.Where(t => t != ConsistencyCheckType.SequenceContinuity)) {
            try {
                switch (checkType) {
                    case ConsistencyCheckType.SnapshotIntegrity:
                        await CheckSnapshotIntegrityAsync(stream, anomalies, ct).ConfigureAwait(false);
                        break;
                    case ConsistencyCheckType.MetadataConsistency:
                        CheckMetadataConsistency(stream, anomalies, streamReadCompleted);
                        break;
                }
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                _logger.LogDebug(ex, "Error checking {CheckType} for stream {Tenant}:{Domain}:{Aggregate}.",
                    checkType, stream.TenantId, stream.Domain, stream.AggregateId);
            }
        }

        return new ConsistencyStreamCheckOutcome(anomalies, evaluatedRange, evaluatedEventCount);
    }

    private readonly record struct ContinuityResult(SequenceRange? EvaluatedRange, long EvaluatedEventCount, bool StreamReadCompleted);

    private async Task<List<ConsistencyAnomaly>> CheckProjectionPositionsAsync(string? tenantId, string? domain) {
        List<ConsistencyAnomaly> anomalies = [];
        string scope = tenantId ?? "all";
        string projectionIndexKey = $"admin:projections:{scope}";
        string storageOverviewKey = $"admin:storage-overview:{scope}";

        try {
            List<ProjectionStatus>? projections = await _daprClient
                .GetStateAsync<List<ProjectionStatus>>(_options.StateStoreName, projectionIndexKey)
                .ConfigureAwait(false);

            StorageOverview? overview = await _daprClient
                .GetStateAsync<StorageOverview>(_options.StateStoreName, storageOverviewKey)
                .ConfigureAwait(false);

            if (projections is null && tenantId is not null) {
                projections = await _daprClient
                    .GetStateAsync<List<ProjectionStatus>>(_options.StateStoreName, "admin:projections:all")
                    .ConfigureAwait(false);
                projections = projections?
                    .Where(p => p.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase)
                        || p.TenantId.Equals("all", StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.TenantId.Equals("all", StringComparison.OrdinalIgnoreCase)
                        ? CopyProjectionForTenant(p, tenantId)
                        : p)
                    .ToList();
            }

            if (projections is null) {
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
            foreach (ProjectionStatus projection in projections) {
                if (projection.Lag > 1000) {
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

                if (totalEvents is long eventCount && projection.LastProcessedPosition > eventCount) {
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

            if (domain is not null) {
                anomalies.Add(new ConsistencyAnomaly(
                    UniqueIdHelper.GenerateSortableUniqueStringId(),
                    ConsistencyCheckType.ProjectionPositions,
                    AnomalySeverity.Warning,
                    scope,
                    domain,
                    "all",
                    "Projection diagnostic limitation: domain-scoped projection positions are not granular.",
                    "Operational warning (check limitation), not an event-loss anomaly. The projection index is tenant-scoped and does not partition positions by domain. Re-run without the domain filter or extend the projection index contract to include domain partitioning if granular validation is required.",
                    null,
                    null));
            }
        }
        catch (Exception ex) {
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

    private async Task<ContinuityResult> CheckSequenceContinuityAsync(
        StreamSummary stream,
        List<ConsistencyAnomaly> anomalies,
        CancellationToken ct) {
        if (stream.LastEventSequence <= 0) {
            return new ContinuityResult(null, 0, true);
        }

        long expectedMax = stream.LastEventSequence;
        SequenceRange evaluatedRange = new(1, expectedMax);
        List<long> observed = [];
        bool partial = false;
        long? cursor = null;

        for (int page = 0; page < MaxTimelinePages; page++) {
            PagedResult<TimelineEntry> result;
            try {
                result = await _streamQueryService.GetStreamTimelineAsync(
                    stream.TenantId,
                    stream.Domain,
                    stream.AggregateId,
                    fromSequence: cursor,
                    toSequence: expectedMax,
                    count: TimelinePageSize,
                    ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested) {
                _logger.LogDebug(
                    ex,
                    "Sequence-continuity timeline read timed out for {Tenant}:{Domain}:{Aggregate}.",
                    stream.TenantId, stream.Domain, stream.AggregateId);
                AddTimelineReadFailureAnomaly(
                    anomalies,
                    stream,
                    expectedMax,
                    "Sequence continuity check timed out while reading the supported stream timeline.",
                    "Timeout");
                return new ContinuityResult(evaluatedRange, 0, false);
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                _logger.LogDebug(
                    ex,
                    "Sequence-continuity timeline read failed for {Tenant}:{Domain}:{Aggregate}.",
                    stream.TenantId, stream.Domain, stream.AggregateId);
                AddTimelineReadFailureAnomaly(
                    anomalies,
                    stream,
                    expectedMax,
                    GetTimelineReadFailureDescription(ex),
                    GetTimelineReadFailureCategory(ex));
                return new ContinuityResult(evaluatedRange, 0, false);
            }

            foreach (TimelineEntry entry in result.Items) {
                if (entry.EntryType != TimelineEntryType.Event) {
                    continue;
                }

                if (entry.SequenceNumber > 0 && entry.SequenceNumber <= expectedMax) {
                    observed.Add(entry.SequenceNumber);
                }
            }

            long observedCoverage = observed.Count;
            long expectedCoverage = Math.Min(expectedMax, Math.Max(result.TotalCount, 0));
            if (string.IsNullOrEmpty(result.ContinuationToken)
                && expectedCoverage > observedCoverage) {
                partial = true;
                break;
            }

            if (string.IsNullOrEmpty(result.ContinuationToken)) {
                break;
            }

            if (!long.TryParse(result.ContinuationToken, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out long nextCursor)
                || (cursor.HasValue && nextCursor <= cursor.Value)) {
                partial = true;
                break;
            }

            cursor = nextCursor;

            if (page == MaxTimelinePages - 1) {
                partial = true;
            }
        }

        if (partial) {
            anomalies.Add(new ConsistencyAnomaly(
                UniqueIdHelper.GenerateSortableUniqueStringId(),
                ConsistencyCheckType.SequenceContinuity,
                AnomalySeverity.Warning,
                stream.TenantId,
                stream.Domain,
                stream.AggregateId,
                "Sequence continuity check Inconclusive: supported timeline did not expose enough information to prove coverage.",
                $"Evaluated up to {observed.Count} events for expected range 1..{expectedMax} before the paging safety guard tripped.",
                ExpectedSequence: expectedMax,
                ActualSequence: observed.Count));
            return new ContinuityResult(evaluatedRange, observed.Distinct().LongCount(), false);
        }

        // Order normalize, detect duplicates separately from missing.
        observed.Sort();
        HashSet<long> distinct = [];
        foreach (long seq in observed) {
            if (!distinct.Add(seq)) {
                AddAnomalyWithinCap(anomalies, new ConsistencyAnomaly(
                        UniqueIdHelper.GenerateSortableUniqueStringId(),
                        ConsistencyCheckType.SequenceContinuity,
                        AnomalySeverity.Error,
                        stream.TenantId,
                        stream.Domain,
                        stream.AggregateId,
                        $"Duplicate sequence number {seq} reported by supported timeline contract.",
                        null,
                        ExpectedSequence: null,
                        ActualSequence: seq));
            }
        }

        long nextExpected = 1;
        foreach (long sequence in distinct.Order()) {
            if (sequence > nextExpected) {
                AddMissingSequenceRange(anomalies, stream, nextExpected, sequence - 1);
            }

            if (sequence >= nextExpected) {
                nextExpected = sequence + 1;
            }
        }

        if (nextExpected <= expectedMax) {
            AddMissingSequenceRange(anomalies, stream, nextExpected, expectedMax);
        }

        return new ContinuityResult(evaluatedRange, distinct.Count, true);
    }

    private static void AddAnomaliesWithinCap(List<ConsistencyAnomaly> target, IEnumerable<ConsistencyAnomaly> source) {
        foreach (ConsistencyAnomaly anomaly in source) {
            AddAnomalyWithinCap(target, anomaly);
            if (target.Count >= AnomalyCap) {
                return;
            }
        }
    }

    private static void AddAnomalyWithinCap(List<ConsistencyAnomaly> target, ConsistencyAnomaly anomaly) {
        if (target.Count < AnomalyCap) {
            target.Add(anomaly);
        }
    }

    private static void AddTimelineReadFailureAnomaly(
        List<ConsistencyAnomaly> anomalies,
        StreamSummary stream,
        long expectedMax,
        string description,
        string category) => AddAnomalyWithinCap(anomalies, new ConsistencyAnomaly(
            UniqueIdHelper.GenerateSortableUniqueStringId(),
            ConsistencyCheckType.SequenceContinuity,
            category is "AuthorizationFailure" ? AnomalySeverity.Error : AnomalySeverity.Warning,
            stream.TenantId,
            stream.Domain,
            stream.AggregateId,
            description,
            $"Category: {category}. Raw DAPR state-store keys are not used as a fallback.",
            ExpectedSequence: expectedMax,
            ActualSequence: null));

    private static string GetTimelineReadFailureCategory(Exception exception)
        => exception switch {
            AdminUpstreamProblemException ex when ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "AuthorizationFailure",
            AdminUpstreamProblemException ex when ex.StatusCode == HttpStatusCode.NotFound => "StreamNotFound",
            AdminUpstreamProblemException => "QueryProblem",
            HttpRequestException ex when ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "AuthorizationFailure",
            HttpRequestException ex when ex.StatusCode == HttpStatusCode.NotFound => "StreamNotFound",
            HttpRequestException => "QueryException",
            TimeoutException => "Timeout",
            _ => "QueryException",
        };

    private static string GetTimelineReadFailureDescription(Exception exception)
        => GetTimelineReadFailureCategory(exception) switch {
            "AuthorizationFailure" => "Sequence continuity check failed: supported stream read was not authorized.",
            "StreamNotFound" => "Sequence continuity check Inconclusive: supported stream read reported the stream was not found.",
            "Timeout" => "Sequence continuity check timed out while reading the supported stream timeline.",
            "QueryProblem" => "Sequence continuity check Inconclusive: supported stream read returned a query problem.",
            _ => "Sequence continuity check Inconclusive: supported stream read failed.",
        };

    private static void AddMissingSequenceRange(
        List<ConsistencyAnomaly> anomalies,
        StreamSummary stream,
        long firstMissing,
        long lastMissing) {
        for (long sequence = firstMissing; sequence <= lastMissing && anomalies.Count < AnomalyCap; sequence++) {
            AddAnomalyWithinCap(anomalies, new ConsistencyAnomaly(
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

    private static ProjectionStatus CopyProjectionForTenant(ProjectionStatus projection, string tenantId)
        => new(
            projection.Name,
            tenantId,
            projection.Status,
            projection.Lag,
            projection.Throughput,
            projection.ErrorCount,
            projection.LastProcessedPosition,
            projection.LastProcessedUtc);

    private async Task CheckSnapshotIntegrityAsync(StreamSummary stream, List<ConsistencyAnomaly> anomalies, CancellationToken ct) {
        // Check: no snapshot for aggregate with > 100 events
        if (!stream.HasSnapshot && stream.EventCount > 100) {
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

        if (!stream.HasSnapshot) {
            return;
        }

        string snapshotKey = $"{stream.TenantId}:{stream.Domain}:{stream.AggregateId}:snapshot";
        try {
            object? snapshot = await _daprClient
                .GetStateAsync<object>(_options.StateStoreName, snapshotKey, cancellationToken: ct)
                .ConfigureAwait(false);
            if (TryExtractLong(snapshot, "sequenceNumber", "SequenceNumber", "sequence", "Sequence", "lastSequence", "LastSequence", "position", "Position") is long snapshotSequence
                && snapshotSequence > stream.LastEventSequence) {
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
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogDebug(ex, "Failed to read snapshot key '{Key}'.", snapshotKey);
        }
    }

    /// <summary>
    /// Metadata consistency uses <see cref="StreamSummary"/> evidence (EventCount, LastEventSequence)
    /// as the supported signal. No raw <c>{tenant}:{domain}:{aggregateId}:metadata</c> probing — the
    /// EventStore exposes no public metadata read contract, and reconstructing actor-state keys would
    /// break actor isolation and produce false positives for actor-backed streams. When EventCount and
    /// LastEventSequence agree, the result is Verified (no anomaly).
    /// </summary>
    private static void CheckMetadataConsistency(StreamSummary stream, List<ConsistencyAnomaly> anomalies, bool supportedStreamReadCompleted) {
        if (stream.EventCount <= 0 && stream.LastEventSequence <= 0) {
            return;
        }

        if (!supportedStreamReadCompleted) {
            AddAnomalyWithinCap(anomalies, new ConsistencyAnomaly(
                UniqueIdHelper.GenerateSortableUniqueStringId(),
                ConsistencyCheckType.MetadataConsistency,
                AnomalySeverity.Warning,
                stream.TenantId,
                stream.Domain,
                stream.AggregateId,
                "Metadata consistency check Inconclusive: no supported stream read proof was available.",
                "The EventStore exposes no public metadata read contract; metadata verification requires coherent StreamSummary evidence plus a completed supported stream read.",
                ExpectedSequence: stream.LastEventSequence,
                ActualSequence: stream.EventCount));
            return;
        }

        if (stream.EventCount != stream.LastEventSequence) {
            AddAnomalyWithinCap(anomalies, new ConsistencyAnomaly(
                UniqueIdHelper.GenerateSortableUniqueStringId(),
                ConsistencyCheckType.MetadataConsistency,
                AnomalySeverity.Error,
                stream.TenantId,
                stream.Domain,
                stream.AggregateId,
                "Metadata count does not match the latest event sequence.",
                "Detected via the supported StreamSummary metadata signal (EventCount vs LastEventSequence).",
                ExpectedSequence: stream.LastEventSequence,
                ActualSequence: stream.EventCount));
        }
    }

    private static long? TryExtractLong(object? source, params string[] candidateNames) {
        if (source is null) {
            return null;
        }

        if (source is JsonElement json) {
            foreach (string name in candidateNames) {
                if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty(name, out JsonElement property)) {
                    if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out long number)) {
                        return number;
                    }

                    if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out long parsed)) {
                        return parsed;
                    }
                }
            }

            return null;
        }

        Type type = source.GetType();
        foreach (string name in candidateNames) {
            System.Reflection.PropertyInfo? property = type.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (property is null) {
                continue;
            }

            object? value = property.GetValue(source);
            if (value is null) {
                continue;
            }

            if (value is long longValue) {
                return longValue;
            }

            if (value is int intValue) {
                return intValue;
            }

            if (long.TryParse(value.ToString(), out long parsedValue)) {
                return parsedValue;
            }
        }

        return null;
    }

    private async Task FailCheckAsync(string key, ConsistencyCheckResult current, string errorMessage) {
        ConsistencyCheckResult failed = current with {
            Status = ConsistencyCheckStatus.Failed,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            ErrorMessage = errorMessage,
        };
        await SaveCheckResultAsync(key, failed, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task SaveCheckResultAsync(string key, ConsistencyCheckResult result, CancellationToken ct) {
        Dictionary<string, string> metadata = new() {
            ["ttlInSeconds"] = (TtlDays * 24 * 60 * 60).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        await _daprClient
            .SaveStateAsync(_options.StateStoreName, key, result, metadata: metadata, cancellationToken: ct)
            .ConfigureAwait(false);
    }
}
