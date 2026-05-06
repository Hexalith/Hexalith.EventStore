@namespace Hexalith.EventStore.Admin.UI.Components
@using Hexalith.EventStore.Admin.Abstractions.Models.Streams

<div class="command-detail-panel" role="complementary" aria-label="Command detail">
    <FluentStack Orientation="Orientation.Vertical" VerticalGap="16">
        <h3 style="margin: 0;">@Entry.TypeName</h3>

        <div class="command-metadata">
            <FluentStack Orientation="Orientation.Vertical" VerticalGap="8">
                <div><strong>Sequence:</strong> <span class="monospace">@Entry.SequenceNumber</span></div>
                <div><strong>Timestamp:</strong> <span class="monospace">@Entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff")</span></div>
                <div>
                    <strong>Correlation ID:</strong>
                    <span class="monospace" title="@Entry.CorrelationId">@Entry.CorrelationId</span>
                    <FluentButton Appearance="ButtonAppearance.Transparent"
                                  Title="Copy correlation ID"
                                  OnClick="() => OnCopyCorrelation.InvokeAsync(Entry.CorrelationId)"
                                  aria-label="Copy correlation ID">
                        &#x1F4CB;
                    </FluentButton>
                    <FluentButton Appearance="ButtonAppearance.Transparent"
                                  Title="Open trace map"
                                  OnClick="() => OnOpenTraceMap.InvokeAsync(Entry.CorrelationId)"
                                  aria-label="Open trace map">
                        &#x1F50D; Trace
                    </FluentButton>
                </div>
                @if (Entry.UserId is not null)
                {
                    <div><strong>User:</strong> @Entry.UserId</div>
                }
            </FluentStack>
        </div>

        <h4 style="margin: 8px 0 0 0;">Command Lifecycle</h4>
        <CommandPipeline />
    </FluentStack>
</div>

@code {
    [Parameter, EditorRequired]
    public TimelineEntry Entry { get; set; } = default!;

    [Parameter]
    public EventCallback<string> OnCopyCorrelation { get; set; }

    [Parameter]
    public EventCallback<string> OnOpenTraceMap { get; set; }
}
