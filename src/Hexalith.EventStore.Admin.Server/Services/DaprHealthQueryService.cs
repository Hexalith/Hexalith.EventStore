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
        bool stateStoreProbeFailed = IsStateStoreProbeFailed(inventory, _options.StateStoreName);

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
            inventory.LocalProbeAvailable);

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
            InventorySourceStatus: inventory.RemoteMetadataStatus);
    }

    private static bool IsStateStoreProbeFailed(DaprCanonicalInventory inventory, string configuredStateStoreName) {
        // The probe runs inside GetCanonicalDaprInventoryAsync and writes Source = LocalAdminProbe
        // on the matching state-store entry. Any non-Healthy status on that row is treated as
        // probe failure for overall-status purposes.
        DaprComponentDetail? row = inventory.Components.FirstOrDefault(c =>
            string.Equals(c.ComponentName, configuredStateStoreName, StringComparison.OrdinalIgnoreCase)
            && c.Category == DaprComponentCategory.StateStore);

        if (row is null) {
            // Configured state store has no row at all — canonical inventory always synthesizes
            // one when neither metadata source listed it, so this branch only fires if the name
            // was filtered out for a category mismatch upstream. Treat as probe failure to honor
            // the truth contract ("absent local evidence cannot be misread as healthy").
            return true;
        }

        return row.Status != HealthStatus.Healthy;
    }

    private static List<DaprComponentHealth> MapToHealthComponents(DaprCanonicalInventory inventory) {
        // After ST3 / probe-last ordering, canonical inventory rows already carry probe Status
        // and Source for state-store components (and remote-source attribution for everything
        // else). /health is now a pure projection.
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<DaprComponentHealth> components = new(inventory.Components.Count);
        foreach (DaprComponentDetail c in inventory.Components) {
            components.Add(new DaprComponentHealth(c.ComponentName, c.ComponentType, c.Status, now, c.Source));
        }

        return components;
    }

    private static HealthStatus ComputeOverallStatus(
        bool stateStoreProbeFailed,
        bool eventStoreFailed,
        RemoteMetadataStatus remoteStatus,
        bool localProbeAvailable) {
        if (stateStoreProbeFailed) {
            return HealthStatus.Unhealthy;
        }

        if (!localProbeAvailable) {
            // Local Admin sidecar unreachable — admin operations cannot be considered healthy.
            return HealthStatus.Unhealthy;
        }

        // Decision D1: only Unreachable signals an operator-meaningful connectivity gap.
        // InvalidPayload and Initializing are diagnostic states surfaced via
        // SystemHealthReport.InventorySourceStatus and per-page banners; they do not double-
        // count against overall health.
        if (remoteStatus == RemoteMetadataStatus.Unreachable) {
            return HealthStatus.Degraded;
        }

        if (eventStoreFailed) {
            return HealthStatus.Degraded;
        }

        return HealthStatus.Healthy;
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
            PagedResult<StreamSummary> page = await _streamQuery
                .GetRecentlyActiveStreamsAsync(tenantId: null, domain: null, count: int.MaxValue, ct)
                .ConfigureAwait(false);
            long sum = 0;
            foreach (StreamSummary s in page.Items) {
                sum += s.EventCount;
            }

            return (sum, SystemHealthMetricStatus.Available);
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
        DaprMetadata metadata = await _daprClient.GetMetadataAsync(ct).ConfigureAwait(false);
        if (metadata?.Components is null) {
            return [];
        }

        return metadata.Components
            .Select(c => new DaprComponentHealth(
                c.Name,
                c.Type,
                HealthStatus.Healthy,
                DateTimeOffset.UtcNow,
                DaprComponentSource.LocalAdminMetadataFallback))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<DaprComponentHealthTimeline> GetComponentHealthHistoryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? componentName,
        CancellationToken ct = default) {
        try {
            // Calculate which day keys to query
            List<string> dayKeys = [];
            for (DateTimeOffset date = from.UtcDateTime.Date; date.Date <= to.UtcDateTime.Date; date = date.AddDays(1)) {
                dayKeys.Add($"admin:health-history:{date:yyyyMMdd}");
            }

            // Read all day partitions in parallel
            Task<DaprComponentHealthTimeline>[] tasks = dayKeys
                .Select(key => _daprClient.GetStateAsync<DaprComponentHealthTimeline>(
                    _options.StateStoreName, key, cancellationToken: ct))
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

            return new DaprComponentHealthTimeline(allEntries.AsReadOnly(), HasData: allEntries.Count > 0, IsTruncated: isTruncated);
        }
        catch (OperationCanceledException) {
            throw;
        }
    }
}
