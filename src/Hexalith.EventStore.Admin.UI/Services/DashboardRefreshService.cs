using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// Scoped service that polls Admin.Server for health and stream data at a fixed 30-second interval.
/// SignalR signals can trigger an immediate refresh via <see cref="TriggerImmediateRefreshAsync"/>.
/// </summary>
public sealed class DashboardRefreshService : IAsyncDisposable, IDisposable
{
    private readonly AdminStreamApiClient _apiClient;
    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger<DashboardRefreshService> _logger;
    private readonly PeriodicTimer _timer;
    private bool _isRefreshing;
    private Task? _timerLoop;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardRefreshService"/> class.
    /// </summary>
    /// <param name="apiClient">The Admin API client.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public DashboardRefreshService(
        AdminStreamApiClient apiClient,
        ILogger<DashboardRefreshService> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Raised when fresh data is available from the API.
    /// </summary>
    public event Action<DashboardData>? OnDataChanged;

    /// <summary>
    /// Raised when a refresh attempt fails.
    /// </summary>
    public event Action<Exception>? OnError;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        _timer.Dispose();
        if (_timerLoop is not null)
        {
            try
            {
                await _timerLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts.Dispose();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cts.Cancel();
        _timer.Dispose();
        _cts.Dispose();
    }

    /// <summary>
    /// Starts the polling timer loop.
    /// </summary>
    public void Start()
    {
        if (_timerLoop is not null)
        {
            return;
        }

        _timerLoop = RunTimerLoopAsync();
    }

    /// <summary>
    /// Triggers an immediate refresh, bypassing the polling interval.
    /// Called by SignalR signal handlers to provide near-real-time updates.
    /// </summary>
    public async Task TriggerImmediateRefreshAsync()
    {
        await RefreshAsync().ConfigureAwait(false);
    }

    private async Task RefreshAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            SystemHealthReport? health = await _apiClient
                .GetSystemHealthAsync(_cts.Token)
                .ConfigureAwait(false);
            PagedResult<StreamSummary>? streams = null;
            if (health is not null && health.TotalEventCount > 0)
            {
                streams = await _apiClient
                    .GetRecentlyActiveStreamsAsync(null, null, 100, _cts.Token)
                    .ConfigureAwait(false);
            }

            OnDataChanged?.Invoke(new DashboardData(health, streams));
        }
        catch (OperationCanceledException)
        {
            // Service is disposing — do not fire error event
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dashboard refresh failed");
            OnError?.Invoke(ex);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task RunTimerLoopAsync()
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(_cts.Token).ConfigureAwait(false))
            {
                await RefreshAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on dispose
        }
    }
}

/// <summary>
/// Holds the latest data from a dashboard refresh cycle.
/// </summary>
/// <param name="Health">The system health report, or null if unavailable.</param>
/// <param name="Streams">The recently active streams, or null if not fetched.</param>
public record DashboardData(SystemHealthReport? Health, PagedResult<StreamSummary>? Streams);
