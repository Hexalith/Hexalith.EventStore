@namespace Hexalith.EventStore.Admin.UI.Components
@using Hexalith.EventStore.Admin.Abstractions.Models.Streams

<div class="timeline-filter-bar" role="toolbar" aria-label="Timeline filters">
    <FluentStack Orientation="Orientation.Horizontal" HorizontalGap="8" VerticalAlignment="VerticalAlignment.Center" Wrap="true">
        <FluentButton Appearance="@(SelectedEntryType is null ? ButtonAppearance.Primary : ButtonAppearance.Outline)"
                      @onclick="() => OnTypeChanged(null)"
                      aria-pressed="@(SelectedEntryType is null ? "true" : "false")">
            All
        </FluentButton>
        <FluentButton Appearance="@(SelectedEntryType == TimelineEntryType.Command ? ButtonAppearance.Primary : ButtonAppearance.Outline)"
                      @onclick="() => OnTypeChanged(TimelineEntryType.Command)"
                      aria-pressed="@(SelectedEntryType == TimelineEntryType.Command ? "true" : "false")">
            Commands
        </FluentButton>
        <FluentButton Appearance="@(SelectedEntryType == TimelineEntryType.Event ? ButtonAppearance.Primary : ButtonAppearance.Outline)"
                      @onclick="() => OnTypeChanged(TimelineEntryType.Event)"
                      aria-pressed="@(SelectedEntryType == TimelineEntryType.Event ? "true" : "false")">
            Events
        </FluentButton>
        <FluentButton Appearance="@(SelectedEntryType == TimelineEntryType.Query ? ButtonAppearance.Primary : ButtonAppearance.Outline)"
                      @onclick="() => OnTypeChanged(TimelineEntryType.Query)"
                      aria-pressed="@(SelectedEntryType == TimelineEntryType.Query ? "true" : "false")">
            Queries
        </FluentButton>

        <FluentTextInput @bind-Value="@_correlationSearch"
                      @bind-Value:after="OnCorrelationChanged"
                      Placeholder="Filter by correlation ID..."
                      aria-label="Filter by correlation ID"
                      Style="min-width: 200px;"
                      TextInputType="TextInputType.Text">
            <StartTemplate>
                <FluentIcon Value="@(new Icons.Regular.Size16.Search())" />
            </StartTemplate>
            <EndTemplate>
                <FluentButton Appearance="ButtonAppearance.Transparent"
                              Disabled="@(string.IsNullOrEmpty(_correlationSearch))"
                              OnClick="ClearCorrelationSearchAsync"
                              aria-label="Clear search"
                              Title="Clear search">
                    <FluentIcon Value="@(new Icons.Regular.Size16.Dismiss())" />
                </FluentButton>
            </EndTemplate>
        </FluentTextInput>

        <div style="margin-left: auto;">
            <FluentButton Appearance="@(CompareMode ? ButtonAppearance.Primary : ButtonAppearance.Outline)"
                          OnClick="ToggleCompareMode"
                          aria-pressed="@(CompareMode ? "true" : "false")"
                          Title="Toggle compare mode to select two events for diff">
                Compare
            </FluentButton>
        </div>
    </FluentStack>
</div>

@code {
    private string? _correlationSearch;

    [Parameter]
    public TimelineEntryType? SelectedEntryType { get; set; }

    [Parameter]
    public EventCallback<TimelineEntryType?> SelectedEntryTypeChanged { get; set; }

    [Parameter]
    public string? CorrelationFilter { get; set; }

    [Parameter]
    public EventCallback<string?> CorrelationFilterChanged { get; set; }

    [Parameter]
    public bool CompareMode { get; set; }

    [Parameter]
    public EventCallback<bool> CompareModeChanged { get; set; }

    protected override void OnParametersSet()
    {
        _correlationSearch = CorrelationFilter;
    }

    private async Task OnTypeChanged(TimelineEntryType? type)
    {
        await SelectedEntryTypeChanged.InvokeAsync(type).ConfigureAwait(false);
    }

    private async Task OnCorrelationChanged()
    {
        string? value = string.IsNullOrWhiteSpace(_correlationSearch) ? null : _correlationSearch.Trim();
        await CorrelationFilterChanged.InvokeAsync(value).ConfigureAwait(false);
    }

    private async Task ClearCorrelationSearchAsync()
    {
        _correlationSearch = string.Empty;
        await OnCorrelationChanged().ConfigureAwait(false);
    }

    private async Task ToggleCompareMode()
    {
        await CompareModeChanged.InvokeAsync(!CompareMode).ConfigureAwait(false);
    }
}
