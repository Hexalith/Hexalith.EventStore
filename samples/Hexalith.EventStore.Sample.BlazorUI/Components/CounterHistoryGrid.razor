@using Microsoft.FluentUI.AspNetCore.Components
@using Hexalith.EventStore.Client.Gateway
@using Hexalith.EventStore.Sample.Counter.Queries
@using Hexalith.EventStore.Sample.BlazorUI.Services
@inject EventStoreSignalRClient SignalRClient
@inject EventStoreProjectionQueryClient QueryClient
@implements IAsyncDisposable

<FluentCard Style="padding: 24px; margin-top: 16px;">
    <h3>Counter History</h3>
    @if (_error is not null)
    {
        <Microsoft.FluentUI.AspNetCore.Components.FluentMessageBar Title="Refresh Error" Intent="MessageBarIntent.Warning" Style="margin-bottom: 12px;">
            @_error
        </Microsoft.FluentUI.AspNetCore.Components.FluentMessageBar>
    }
    @if (_isLoading && _history.Count == 0)
    {
        <Microsoft.FluentUI.AspNetCore.Components.FluentProgressBar />
    }
    else
    {
        <Microsoft.FluentUI.AspNetCore.Components.FluentDataGrid Items="@_history.AsQueryable()" Style="width: 100%;">
            <PropertyColumn Property="@(e => e.Timestamp)" Title="Time" Format="HH:mm:ss.fff" />
            <PropertyColumn Property="@(e => e.Count)" Title="Count" />
            <PropertyColumn Property="@(e => e.ETag)" Title="ETag (prefix)" />
        </Microsoft.FluentUI.AspNetCore.Components.FluentDataGrid>
        @if (_isLoading)
        {
            <Microsoft.FluentUI.AspNetCore.Components.FluentProgressBar Style="margin-top: 8px;" />
        }
    }
</FluentCard>

@code {
    [Parameter] public string TenantId { get; set; } = "tenant-a";
    [Parameter] public string ProjectionType { get; set; } = "counter";

    private readonly List<CounterHistoryEntry> _history = [];
    private CounterStatusResult? _lastResult;
    private bool _isLoading = true;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        await SignalRClient.SubscribeAsync(ProjectionType, TenantId, OnProjectionChanged);
        await LoadDataAsync();
        _isLoading = false;
    }

    private void OnProjectionChanged()
    {
        _ = InvokeAsync(async () =>
        {
            _isLoading = true;
            StateHasChanged();

            await LoadDataAsync();

            _isLoading = false;
            StateHasChanged();
        });
    }

    private async Task LoadDataAsync()
    {
        try
        {
            EventStoreQueryResult queryResult = await QueryClient.GetAsync(
                TenantId,
                GetCounterStatusQuery.Domain,
                "counter-1",
                GetCounterStatusQuery.QueryType,
                GetCounterStatusQuery.ProjectionType,
                "counter-1",
                _lastResult?.ETag);
            CounterStatusResult result = CounterStatusResult.FromQueryResult(queryResult, _lastResult);
            _lastResult = result;
            _history.Insert(0, new CounterHistoryEntry(
                DateTime.Now,
                result.Count,
                result.ETag?[..Math.Min(8, result.ETag.Length)] ?? "n/a"));
            _error = null;

            // Keep last 20 entries to prevent unbounded growth
            if (_history.Count > 20)
            {
                _history.RemoveAt(_history.Count - 1);
            }
        }
        catch (EventStoreGatewayException ex) when (ex.StatusCode == 404)
        {
            _lastResult = CounterStatusResult.Empty;
            _history.Insert(0, new CounterHistoryEntry(DateTime.Now, 0, "n/a"));
            _error = null;

            // Keep last 20 entries to prevent unbounded growth
            if (_history.Count > 20)
            {
                _history.RemoveAt(_history.Count - 1);
            }
        }
        catch (Exception ex)
        {
            _error = $"History refresh failed: {ex.Message}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        await SignalRClient.UnsubscribeAsync(ProjectionType, TenantId, OnProjectionChanged);
    }

    private sealed record CounterHistoryEntry(DateTime Timestamp, int Count, string ETag);
}
