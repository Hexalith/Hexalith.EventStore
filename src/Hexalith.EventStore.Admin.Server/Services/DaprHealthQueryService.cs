using System.Net.Http.Headers;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// DAPR-backed implementation of <see cref="IHealthQueryService"/>.
/// Health checks work independently of EventStore — uses DAPR metadata and state store probes directly.
/// </summary>
public sealed class DaprHealthQueryService : IHealthQueryService {
    private readonly IAdminAuthContext _authContext;
    private readonly DaprClient _daprClient;
    private readonly IDaprInfrastructureQueryService _infrastructure;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DaprHealthQueryService> _logger;
    private readonly AdminServerOptions _options;
    private readonly IStreamQueryService _streamQuery;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprHealthQueryService"/> class.
    /// </summary>
    /// <param name="daprClient">The DAPR client.</param>
    /// <param name="httpClientFactory">The HTTP client factory for DAPR service invocation.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="authContext">The admin auth context for JWT forwarding.</param>
    /// <param name="streamQuery">The stream query service that backs the dashboard's TotalEventCount metric (bounded source: <c>admin:stream-activity:all</c>).</param>
    /// <param name="infrastructure">The shared canonical DAPR inventory provider.</param>
    /// <param name="logger">The logger.</param>
    public DaprHealthQueryService(
        DaprClient daprClient,
        IHttpClientFactory httpClientFactory,
        IOptions<AdminServerOptions> options,
        IAdminAuthContext authContext,
        IStreamQueryService streamQuery,
        IDaprInfrastructureQueryService infrastructure,
        ILogger<DaprHealthQueryService> logger) {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(authContext);
        ArgumentNullException.ThrowIfNull(streamQuery);
        ArgumentNullException.ThrowIfNull(infrastructure);
        ArgumentNullException.ThrowIfNull(logger);
        _daprClient = daprClient;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _authContext = authContext;
        _streamQuery = streamQuery;
        _infrastructure = infrastructure;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SystemHealthReport> GetSystemHealthAsync(CancellationToken ct = default) {
        // Build the canonical inventory once. The infra service swallows dependency failures
        // and exposes them as RemoteMetadataStatus values, so we never throw out of /health
        // for downstream issues — only real cancellation propagates. The canonical inventory
        // is also the sole source of state-store probe evidence: it ensures the configured
        // state store is probed (synthesizing a row when neither metadata source listed it)
        // and writes the probe result with Source = LocalAdminProbe.
        DaprCanonicalInventory inventory = await _infrastructure
            .GetCanonicalDaprInventoryAsync(ct)
            .ConfigureAwait(false);

        // Derive state-store evidence from the canonical inventory. AC1 requires that when
        // Redis is down, the matching component row is Unhealthy and overall status is
        // Unhealthy. The probe ran inside GetCanonicalDaprInventoryAsync; here we only read.
        bool stateStoreProbeFailed = IsStateStoreProbeFailed(inventory, _options.StateStoreName, _logger);

        List<DaprComponentHealth> components = MapToHealthComponents(inventory);

        // 2. EventStore reachability (short timeout - degraded, not unhealthy)
        bool eventStoreFailed = false;
        try {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            using HttpRequestMessage request = _daprClient.CreateInvokeMethodRequest(
                HttpMethod.Get, _options.EventStoreAppId, "health");

            string? token = _authContext.GetToken();
            if (token is not null) {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            HttpClient httpClient = _httpClientFactory.CreateClient();
            using HttpResponseMessage httpResponse = await httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
            _ = httpResponse.EnsureSuccessStatusCode();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            eventStoreFailed = true;
            _logger.LogWarning("EventStore health check timed out — marking as Degraded.");
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            eventStoreFailed = true;
            _logger.LogWarning(ex, "EventStore health check failed — marking as Degraded.");
        }

        HealthStatus overallStatus = ComputeOverallStatus(
            stateStoreProbeFailed,
            eventStoreFailed,
            inventory.RemoteMetadataStatus,
            inventory.LocalSidecarMetadataAvailable,
            _logger);

        ObservabilityLinks links = new(_options.TraceUrl, _options.MetricsUrl, _options.LogsUrl);

        // ADR-3 (Truthful Metrics): TotalEventCount is derived from the bounded
        // admin:stream-activity:all index; EventsPerSecond and ErrorPercentage have no
        // injectable clock / rolling-window or rejection-rate source wired in this build,
        // so they are reported with explicit Unavailable status. The numeric value stays
        // 0 only as a wire-format fallback — UI must consult the *Status fields and render
        // an unavailable indicator instead of the zero.
        (long totalEventCount, SystemHealthMetricStatus totalEventCountStatus) =
            await TryComputeTotalEventCountAsync(ct).ConfigureAwait(false);

        return new SystemHealthReport(
            overallStatus,
            TotalEventCount: totalEventCount,
            EventsPerSecond: 0,
            ErrorPercentage: 0,
            components,
            links,
            TotalEventCountStatus: totalEventCountStatus,
            EventsPerSecondStatus: SystemHealthMetricStatus.Unavailable,
            ErrorPercentageStatus: SystemHealthMetricStatus.Unavailable,
            InventorySourceStatus: inventory.RemoteMetadataStatus,
            LocalSidecarMetadataStatus: inventory.LocalSidecarMetadataAvailable
                ? RemoteMetadataStatus.Available
                : RemoteMetadataStatus.Unreachable);
    }

    private static bool IsStateStoreProbeFailed(DaprCanonicalInventory inventory, string configuredStateStoreName, ILogger logger) {
        if (string.IsNullOrWhiteSpace(configuredStateStoreName)) {
            // Without a configured state-store name we cannot identify the probe row; absent
            // probe evidence cannot be presented as healthy, so treat as probe failure. Log a
            // single warning per call so operators chasing an "Unhealthy" alert see the
            // configuration cause instead of mistaking it for a Redis outage. The throttling is
            // implicit: /health is called per request, but the log is a structured warning that
            // can be filtered by message template.
            logger.LogWarning(
                "AdminServerOptions.StateStoreName is empty/whitespace; the configured state-store probe is disabled and /health will report Unhealthy until the option is set. This is a configuration error, not a Redis outage.");
            return true;
        }

        // The probe runs inside GetCanonicalDaprInventoryAsync and writes the probe result onto
        // the matching state-store entry. Inventory provenance and health evidence are split:
        // Source can remain RemoteEventStoreMetadata while HealthEvidenceSource proves the
        // Status came from LocalAdminProbe.
        DaprComponentDetail[] probeRows = inventory.Components
            .Where(c =>
                string.Equals(c.ComponentName, configuredStateStoreName, StringComparison.OrdinalIgnoreCase)
                && c.Category == DaprComponentCategory.StateStore
                && c.HealthEvidenceSource == DaprComponentSource.LocalAdminProbe)
            .OrderBy(c => c.ComponentType, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (probeRows.Length == 0) {
            // No probe-attributed row for the configured state-store name. This means either
            // the synth was not inserted (pre-condition: name was missing/empty/whitespace —
            // already handled above) or the probe never overwrote a remote-attributed row.
            // Either way we have no probe evidence; treat as probe failure to honour the
            // truth contract ("absent local evidence cannot be misread as healthy").
            return true;
        }

        // Only Status == Unhealthy counts as probe failure. The probe writes Healthy on
        // success, Unhealthy on exception/timeout; Degraded would never be probe-written and
        // collapsing it into "failed" would misclassify any future-emitted state.
        return probeRows.Any(r => r.Status == HealthStatus.Unhealthy);
    }

    private static List<DaprComponentHealth> MapToHealthComponents(DaprCanonicalInventory inventory) {
        // After ST3 / probe-last ordering, canonical inventory rows already carry probe Status,
        // Source, and the timestamp at which the evidence was captured (probe time for state
        // stores, remote-payload-parse time for remote-attributed rows, fallback insertion time
        // otherwise). /health is now a pure projection — preserving c.LastCheckUtc keeps the
        // operator's "when was this last checked?" triage signal intact instead of stamping the
        // request rendering time over real probe latency.
        //
        // Round 5 P4: collapse rows that share ComponentName so the /health grid does not show
        // two rows for the same component when its types differ across sources (e.g., the synth
        // `state.unknown` placeholder and a real `state.redis` row coexisting because Stage 3's
        // skip predicate only inspects category/name and a future change could leave the synth
        // behind). Prefer the row with the most authoritative probe evidence:
        //   1. LocalAdminProbe Source (probe attestation present)
        //   2. RemoteEventStoreMetadata Source (canonical inventory present)
        //   3. LocalAdminMetadataFallback (degraded inventory)
        //   4. Unavailable (truly no source attribution)
        // Within a tier, prefer the row whose ComponentType is *not* `state.unknown` so a real
        // type wins over the synth placeholder.
        List<DaprComponentHealth> components = new(inventory.Components.Count);
        foreach (IGrouping<string, DaprComponentDetail> group in inventory.Components.GroupBy(c => c.ComponentName, StringComparer.OrdinalIgnoreCase)) {
            DaprComponentDetail winner = group
                .OrderBy(c => SourcePreferenceRank(c.HealthEvidenceSource))
                .ThenBy(c => SourcePreferenceRank(c.Source))
                .ThenBy(c => string.Equals(c.ComponentType, "state.unknown", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenByDescending(c => c.LastCheckUtc)
                .First();
            components.Add(new DaprComponentHealth(
                winner.ComponentName,
                winner.ComponentType,
                winner.Status,
                winner.LastCheckUtc,
                winner.Source,
                winner.HealthEvidenceSource));
        }

        return components;
    }

    private static int SourcePreferenceRank(DaprComponentSource source) => source switch {
        DaprComponentSource.LocalAdminProbe => 0,
        DaprComponentSource.RemoteEventStoreMetadata => 1,
        DaprComponentSource.LocalAdminMetadataFallback => 2,
        _ => 3,
    };

    /// <summary>
    /// Aggregates dependency evidence into a single <see cref="HealthStatus"/>.
    /// Round 5 P8: this method intentionally implements first-failure-wins ordering — the
    /// returned status reflects the *highest-severity* dependency outcome, but the cause is
    /// not embedded in the return value. Operators looking for multi-cause diagnosis should
    /// inspect <see cref="SystemHealthReport.InventorySourceStatus"/>,
    /// <see cref="SystemHealthReport.LocalSidecarMetadataStatus"/>, and the per-component
    /// <see cref="DaprComponentHealth"/> rows directly. The /health UI surfaces these via
    /// dedicated banners so a two-cause failure (e.g., Unhealthy state-store + Unreachable
    /// remote metadata) renders all causes side-by-side rather than collapsing into a single
    /// reason string. Spec status vocabulary table documents the same first-failure-wins rule.
    /// </summary>
    private static HealthStatus ComputeOverallStatus(
        bool stateStoreProbeFailed,
        bool eventStoreFailed,
        RemoteMetadataStatus remoteStatus,
        bool localSidecarMetadataAvailable,
        ILogger logger) {
        // First-failure-wins: state-store probe failure dominates because Admin operations
        // require a usable state store. Multi-cause callers must also consult inventory/local
        // sidecar status fields for full diagnosis.
        if (stateStoreProbeFailed) {
            return HealthStatus.Unhealthy;
        }

        if (!localSidecarMetadataAvailable) {
            // Local Admin sidecar unreachable or returned an empty payload — admin operations
            // cannot be considered healthy. (The per-cause breakdown lives in
            // SystemHealthReport.LocalSidecarMetadataStatus.)
            return HealthStatus.Unhealthy;
        }

        // Round 5 P7: explicit switch with a default arm so a future RemoteMetadataStatus
        // member added without updating this method does not silently fall through to Healthy.
        // The truth contract forbids "unknown evidence presented as healthy", and the rest of
        // this rewrite goes to lengths to prevent unknown-default traps; this method must too.
        HealthStatus remoteContribution = remoteStatus switch {
            RemoteMetadataStatus.NotConfigured => HealthStatus.Healthy,
            RemoteMetadataStatus.Available => HealthStatus.Healthy,
            RemoteMetadataStatus.Unreachable => HealthStatus.Degraded,
            RemoteMetadataStatus.InvalidPayload => HealthStatus.Degraded,
            _ => LogUnknownAndDegrade(remoteStatus, logger),
        };

        if (remoteContribution == HealthStatus.Degraded) {
            return HealthStatus.Degraded;
        }

        if (eventStoreFailed) {
            return HealthStatus.Degraded;
        }

        return HealthStatus.Healthy;
    }

    private static HealthStatus LogUnknownAndDegrade(RemoteMetadataStatus status, ILogger logger) {
        logger.LogWarning(
            "Unrecognised RemoteMetadataStatus '{Status}' encountered in ComputeOverallStatus; treating as Degraded so unknown evidence does not render as healthy.",
            status);
        return HealthStatus.Degraded;
    }

    /// <summary>
    /// Computes <see cref="SystemHealthReport.TotalEventCount"/> by summing
    /// <see cref="StreamSummary.EventCount"/> across the bounded
    /// <c>admin:stream-activity:all</c> index. The index is bounded by design and is
    /// already used by every Admin UI page that lists streams, so this does not scan
    /// unbounded storage on a health request.
    /// </summary>
    /// <returns>
    /// A tuple of (sum, status). Status is <see cref="SystemHealthMetricStatus.Unavailable"/>
    /// when the index read fails so the UI can render an explicit unavailable indicator
    /// rather than a misleading zero.
    /// </returns>
    private async Task<(long Sum, SystemHealthMetricStatus Status)> TryComputeTotalEventCountAsync(CancellationToken ct) {
        try {
            using var metricCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            metricCts.CancelAfter(TimeSpan.FromSeconds(3));
            PagedResult<StreamSummary> page = await _streamQuery
                .GetRecentlyActiveStreamsAsync(tenantId: null, domain: null, count: int.MaxValue, metricCts.Token)
                .ConfigureAwait(false);
            long sum = 0;
            foreach (StreamSummary s in page.Items) {
                sum += s.EventCount;
            }

            return (sum, SystemHealthMetricStatus.Available);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested) {
            _logger.LogWarning(ex, "Timed out computing TotalEventCount from stream activity index — reporting Unavailable.");
            return (0, SystemHealthMetricStatus.Unavailable);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to compute TotalEventCount from stream activity index — reporting Unavailable.");
            return (0, SystemHealthMetricStatus.Unavailable);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DaprComponentHealth>> GetDaprComponentStatusAsync(CancellationToken ct = default) {
        // Route through the canonical inventory so this API contract surface returns the same
        // truth as /health and /dapr — every row carries probe-derived Status and Source
        // attribution rather than the pre-refactor "everything from local sidecar metadata is
        // Healthy + LocalAdminMetadataFallback" answer.
        DaprCanonicalInventory inventory = await _infrastructure
            .GetCanonicalDaprInventoryAsync(ct)
            .ConfigureAwait(false);

        return MapToHealthComponents(inventory);
    }

    /// <inheritdoc/>
    public async Task<DaprComponentHealthTimeline> GetComponentHealthHistoryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? componentName,
        CancellationToken ct = default) {
        try {
            using var historyCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            historyCts.CancelAfter(TimeSpan.FromSeconds(3));

            // Calculate which day keys to query
            List<string> dayKeys = [];
            for (DateTimeOffset date = from.UtcDateTime.Date; date.Date <= to.UtcDateTime.Date; date = date.AddDays(1)) {
                dayKeys.Add($"admin:health-history:{date:yyyyMMdd}");
            }

            // Read all day partitions in parallel
            Task<DaprComponentHealthTimeline>[] tasks = dayKeys
                .Select(key => _daprClient.GetStateAsync<DaprComponentHealthTimeline>(
                    _options.StateStoreName, key, cancellationToken: historyCts.Token))
                .ToArray();

            DaprComponentHealthTimeline[] results = await Task.WhenAll(tasks).ConfigureAwait(false);

            // Merge and filter
            var allEntries = results
                .Where(t => t?.Entries is not null)
                .SelectMany(t => t!.Entries)
                .Where(e => e.CapturedAtUtc >= from && e.CapturedAtUtc <= to)
                .Where(e => componentName is null
                    || e.ComponentName.Equals(componentName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.CapturedAtUtc)
                .ToList();

            // Apply entry cap to prevent excessive memory/payload on large queries
            bool isTruncated = allEntries.Count > _options.MaxHealthHistoryEntriesPerQuery;
            if (isTruncated) {
                allEntries = allEntries
                    .OrderByDescending(e => e.CapturedAtUtc)
                    .Take(_options.MaxHealthHistoryEntriesPerQuery)
                    .OrderBy(e => e.CapturedAtUtc)
                    .ToList();
            }

            return new DaprComponentHealthTimeline(
                allEntries.AsReadOnly(),
                HasData: allEntries.Count > 0,
                IsTruncated: isTruncated,
                HistoryStatus: SystemHealthMetricStatus.Available,
                StatusMessage: allEntries.Count == 0
                    ? results.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t?.StatusMessage))?.StatusMessage
                    : null);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested) {
            _logger.LogWarning(ex, "Timed out reading DAPR component health history — reporting history source unavailable.");
            return new DaprComponentHealthTimeline(
                [],
                HasData: false,
                IsTruncated: false,
                HistoryStatus: SystemHealthMetricStatus.Unavailable,
                StatusMessage: "Health history storage is unavailable; live health and DAPR inventory remain authoritative for the current sample.");
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to read DAPR component health history — reporting history source unavailable.");
            return new DaprComponentHealthTimeline(
                [],
                HasData: false,
                IsTruncated: false,
                HistoryStatus: SystemHealthMetricStatus.Unavailable,
                StatusMessage: "Health history storage is unavailable; live health and DAPR inventory remain authoritative for the current sample.");
        }
    }
}
