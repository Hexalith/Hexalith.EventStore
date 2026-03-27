using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Admin.Server.Services;

/// <summary>
/// Background service that periodically captures DAPR component health snapshots
/// and persists them to the DAPR state store for historical analysis.
/// </summary>
public sealed class DaprHealthHistoryCollector : BackgroundService
{
    private readonly ILogger<DaprHealthHistoryCollector> _logger;
    private readonly AdminServerOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprHealthHistoryCollector"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scoped service instances per tick.</param>
    /// <param name="options">The admin server options.</param>
    /// <param name="logger">The logger.</param>
    public DaprHealthHistoryCollector(
        IServiceScopeFactory scopeFactory,
        IOptions<AdminServerOptions> options,
        ILogger<DaprHealthHistoryCollector> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.HealthHistoryEnabled)
        {
            _logger.LogInformation("Health history collection is disabled");
            return;
        }

        // Allow DAPR sidecar to initialize before first probe
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ConfigureAwait(false);

        // Run retention cleanup on startup
        await CleanupExpiredHistoryAsync(stoppingToken).ConfigureAwait(false);

        // Capture first snapshot immediately (don't wait for first timer tick)
        await CaptureSnapshotAsync(stoppingToken).ConfigureAwait(false);

        int intervalSeconds = Math.Max(10, _options.HealthHistoryCaptureIntervalSeconds);
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(intervalSeconds));
        DateTimeOffset lastCleanup = DateTimeOffset.UtcNow;

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await CaptureSnapshotAsync(stoppingToken).ConfigureAwait(false);

                // Daily cleanup check
                if (DateTimeOffset.UtcNow.Date > lastCleanup.Date)
                {
                    await CleanupExpiredHistoryAsync(stoppingToken).ConfigureAwait(false);
                    lastCleanup = DateTimeOffset.UtcNow;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Health history capture failed — will retry next cycle");
            }
        }
    }

    private async Task CaptureSnapshotAsync(CancellationToken ct)
    {
        // IMPORTANT: Create a scope per tick — DaprClient and query services are scoped
        using IServiceScope scope = _scopeFactory.CreateScope();
        IDaprInfrastructureQueryService infraService = scope.ServiceProvider
            .GetRequiredService<IDaprInfrastructureQueryService>();
        DaprClient daprClient = scope.ServiceProvider.GetRequiredService<DaprClient>();

        IReadOnlyList<DaprComponentDetail> components = await infraService
            .GetComponentsAsync(ct)
            .ConfigureAwait(false);

        // Skip write if no components returned (sidecar unreachable) — avoids empty snapshots
        if (components.Count == 0)
        {
            _logger.LogDebug("No DAPR components returned — skipping health history snapshot");
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string dayKey = $"admin:health-history:{now:yyyyMMdd}";

        try
        {
            // Read current day's timeline (may be null if first entry today)
            DaprComponentHealthTimeline? existing = await daprClient
                .GetStateAsync<DaprComponentHealthTimeline>(_options.StateStoreName, dayKey, cancellationToken: ct)
                .ConfigureAwait(false);

            List<DaprHealthHistoryEntry> entries = existing?.Entries?.ToList() ?? [];

            // Append new entries for each component
            foreach (DaprComponentDetail component in components)
            {
                entries.Add(new DaprHealthHistoryEntry(
                    ComponentName: component.ComponentName,
                    ComponentType: component.ComponentType,
                    Status: component.Status,
                    CapturedAtUtc: now));
            }

            DaprComponentHealthTimeline updated = new(entries.AsReadOnly(), HasData: true);

            await daprClient
                .SaveStateAsync(_options.StateStoreName, dayKey, updated, cancellationToken: ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist health history snapshot to state store");
        }
    }

    private async Task CleanupExpiredHistoryAsync(CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        DaprClient daprClient = scope.ServiceProvider.GetRequiredService<DaprClient>();

        int retentionDays = Math.Clamp(_options.HealthHistoryRetentionDays, 1, 30);
        int consecutiveFailures = 0;

        // Delete keys for dates before cutoff, going back up to 30 days max to avoid unbounded cleanup
        for (int i = retentionDays + 1; i <= retentionDays + 30; i++)
        {
            DateTimeOffset date = DateTimeOffset.UtcNow.AddDays(-i);
            string dayKey = $"admin:health-history:{date:yyyyMMdd}";
            try
            {
                await daprClient.DeleteStateAsync(_options.StateStoreName, dayKey, cancellationToken: ct)
                    .ConfigureAwait(false);
                consecutiveFailures = 0;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                if (consecutiveFailures >= 3)
                {
                    _logger.LogWarning(ex, "Repeated cleanup failures ({Count} consecutive) — state store may be unavailable. Key: {Key}", consecutiveFailures, dayKey);
                }
                else
                {
                    _logger.LogDebug(ex, "Failed to delete expired health history key: {Key}", dayKey);
                }
            }
        }
    }
}
