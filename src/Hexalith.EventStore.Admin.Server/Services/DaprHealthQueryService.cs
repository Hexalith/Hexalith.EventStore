using System.Net.Http.Headers;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// DAPR-backed implementation of <see cref="IHealthQueryService"/>.
/// Health checks work independently of EventStore — uses DAPR metadata and state store probes directly.
/// </summary>
public sealed class DaprHealthQueryService : IHealthQueryService
{
    private readonly IAdminAuthContext _authContext;
    private readonly DaprClient _daprClient;
    private readonly ILogger<DaprHealthQueryService> _logger;
    private readonly AdminServerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprHealthQueryService"/> class.
    /// </summary>
    /// <param name="daprClient">The DAPR client.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="authContext">The admin auth context for JWT forwarding.</param>
    /// <param name="logger">The logger.</param>
    public DaprHealthQueryService(
        DaprClient daprClient,
        IOptions<AdminServerOptions> options,
        IAdminAuthContext authContext,
        ILogger<DaprHealthQueryService> logger)
    {
        ArgumentNullException.ThrowIfNull(daprClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(authContext);
        ArgumentNullException.ThrowIfNull(logger);
        _daprClient = daprClient;
        _options = options.Value;
        _authContext = authContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SystemHealthReport> GetSystemHealthAsync(CancellationToken ct = default)
    {
        HealthStatus overallStatus = HealthStatus.Healthy;
        List<DaprComponentHealth> components = [];

        // 1. DAPR sidecar health via metadata
        try
        {
            DaprMetadata metadata = await _daprClient.GetMetadataAsync(ct).ConfigureAwait(false);
            if (metadata?.Components is not null)
            {
                foreach (DaprComponentsMetadata component in metadata.Components)
                {
                    components.Add(new DaprComponentHealth(
                        component.Name,
                        component.Type,
                        HealthStatus.Healthy,
                        DateTimeOffset.UtcNow));
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DAPR sidecar metadata unavailable.");
            overallStatus = HealthStatus.Unhealthy;
        }

        // 2. State store connectivity probe
        try
        {
            _ = await _daprClient
                .GetStateAsync<string>(_options.StateStoreName, "admin:health-check", cancellationToken: ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "State store connectivity probe failed.");
            overallStatus = HealthStatus.Unhealthy;
        }

        // 3. EventStore reachability (short timeout - degraded, not unhealthy)
        try
        {
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            using HttpRequestMessage request = _daprClient.CreateInvokeMethodRequest(
                HttpMethod.Get, _options.EventStoreAppId, "health");

            string? token = _authContext.GetToken();
            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            _ = await _daprClient.InvokeMethodAsync<string>(request, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // EventStore timeout — degraded but not unhealthy
            if (overallStatus == HealthStatus.Healthy)
            {
                overallStatus = HealthStatus.Degraded;
            }

            _logger.LogWarning("EventStore health check timed out — marking as Degraded.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (overallStatus == HealthStatus.Healthy)
            {
                overallStatus = HealthStatus.Degraded;
            }

            _logger.LogWarning(ex, "EventStore health check failed — marking as Degraded.");
        }

        ObservabilityLinks links = new(_options.TraceUrl, _options.MetricsUrl, _options.LogsUrl);

        return new SystemHealthReport(
            overallStatus,
            TotalEventCount: 0,
            EventsPerSecond: 0,
            ErrorPercentage: 0,
            components,
            links);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DaprComponentHealth>> GetDaprComponentStatusAsync(CancellationToken ct = default)
    {
        DaprMetadata metadata = await _daprClient.GetMetadataAsync(ct).ConfigureAwait(false);
        if (metadata?.Components is null)
        {
            return [];
        }

        return metadata.Components
            .Select(c => new DaprComponentHealth(
                c.Name,
                c.Type,
                HealthStatus.Healthy,
                DateTimeOffset.UtcNow))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<DaprComponentHealthTimeline> GetComponentHealthHistoryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? componentName,
        CancellationToken ct = default)
    {
        try
        {
            // Calculate which day keys to query
            List<string> dayKeys = [];
            for (DateTimeOffset date = from.UtcDateTime.Date; date.Date <= to.UtcDateTime.Date; date = date.AddDays(1))
            {
                dayKeys.Add($"admin:health-history:{date:yyyyMMdd}");
            }

            // Read all day partitions in parallel
            Task<DaprComponentHealthTimeline>[] tasks = dayKeys
                .Select(key => _daprClient.GetStateAsync<DaprComponentHealthTimeline>(
                    _options.StateStoreName, key, cancellationToken: ct))
                .ToArray();

            DaprComponentHealthTimeline[] results = await Task.WhenAll(tasks).ConfigureAwait(false);

            // Merge and filter
            List<DaprHealthHistoryEntry> allEntries = results
                .Where(t => t?.Entries is not null)
                .SelectMany(t => t!.Entries)
                .Where(e => e.CapturedAtUtc >= from && e.CapturedAtUtc <= to)
                .Where(e => componentName is null
                    || e.ComponentName.Equals(componentName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.CapturedAtUtc)
                .ToList();

            // Apply entry cap to prevent excessive memory/payload on large queries
            bool isTruncated = allEntries.Count > _options.MaxHealthHistoryEntriesPerQuery;
            if (isTruncated)
            {
                allEntries = allEntries
                    .OrderByDescending(e => e.CapturedAtUtc)
                    .Take(_options.MaxHealthHistoryEntriesPerQuery)
                    .OrderBy(e => e.CapturedAtUtc)
                    .ToList();
            }

            return new DaprComponentHealthTimeline(allEntries.AsReadOnly(), HasData: allEntries.Count > 0, IsTruncated: isTruncated);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }
}
