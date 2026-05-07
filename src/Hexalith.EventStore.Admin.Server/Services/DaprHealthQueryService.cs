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
        // for downstream issues — only real cancellation propagates.
        DaprCanonicalInventory inventory = await _infrastructure
            .GetCanonicalDaprInventoryAsync(ct)
            .ConfigureAwait(false);

        // 1. Probe the configured Admin.Server state-store dependency (Redis usability for Admin).
        //    This is the canonical evidence for AC1: when Redis is down, the matching component
        //    row must flip to Unhealthy and overall status must be Unhealthy. We never let the
        //    probe exception bubble up — a failed probe is itself the health evidence.
        (HealthStatus stateStoreStatus, bool stateStoreProbeFailed) =
            await ProbeStateStoreAsync(ct).ConfigureAwait(false);

        // Apply the local Admin probe to the matching canonical component (or synthesize one if
        // the state-store name is not in the canonical set yet — e.g. when both local and remote
        // metadata are unavailable).
        List<DaprComponentHealth> components = MapToHealthComponents(
            inventory,
            stateStoreStatus,
            stateStoreProbeFailed);

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

    private async Task<(HealthStatus Status, bool ProbeFailed)> ProbeStateStoreAsync(CancellationToken ct) {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        try {
            _ = await _daprClient
                .GetStateAsync<string>(_options.StateStoreName, "admin:health-check", cancellationToken: cts.Token)
                .ConfigureAwait(false);
            return (HealthStatus.Healthy, false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
            // Probe timed out without caller cancellation — treat as Unhealthy evidence.
            _logger.LogWarning("State store connectivity probe timed out after 3 seconds.");
            return (HealthStatus.Unhealthy, true);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "State store connectivity probe failed.");
            return (HealthStatus.Unhealthy, true);
        }
    }

    private List<DaprComponentHealth> MapToHealthComponents(
        DaprCanonicalInventory inventory,
        HealthStatus stateStoreStatus,
        bool stateStoreProbeFailed) {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<DaprComponentHealth> components = [];
        bool stateStoreApplied = false;
        string configuredStateStore = _options.StateStoreName;

        foreach (DaprComponentDetail c in inventory.Components) {
            HealthStatus status = c.Status;
            DaprComponentSource source = c.Source;

            // Apply the local Admin.Server state-store probe to the configured state-store
            // component, regardless of which sidecar reported it.
            if (string.Equals(c.ComponentName, configuredStateStore, StringComparison.OrdinalIgnoreCase)
                && c.Category == DaprComponentCategory.StateStore) {
                status = stateStoreStatus;
                source = DaprComponentSource.LocalAdminProbe;
                stateStoreApplied = true;
            }

            components.Add(new DaprComponentHealth(c.ComponentName, c.ComponentType, status, now, source));
        }

        // If the canonical inventory did not include the configured state store, synthesize a
        // row from the local probe so /health never silently omits a failed state-store outage.
        if (!stateStoreApplied) {
            components.Add(new DaprComponentHealth(
                configuredStateStore,
                "state",
                stateStoreStatus,
                now,
                DaprComponentSource.LocalAdminProbe));
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

        if (remoteStatus is RemoteMetadataStatus.Unreachable
            or RemoteMetadataStatus.InvalidPayload
            or RemoteMetadataStatus.Initializing) {
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
