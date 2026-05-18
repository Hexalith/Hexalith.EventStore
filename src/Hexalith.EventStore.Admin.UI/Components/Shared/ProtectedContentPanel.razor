@namespace Hexalith.EventStore.Admin.UI.Components.Shared
@using Hexalith.EventStore.Admin.Abstractions.Models

@if (Compact)
{
    <span class="protected-content-panel protected-content-panel--compact"
          role="status"
          aria-label="@($"Protected {Content.ContentKind} redacted. Reason code {Content.ReasonCode}. Stage {Content.Stage}.")"
          style="display: inline-flex; align-items: center; gap: 6px; flex-wrap: wrap;">
        <FluentBadge Appearance="BadgeAppearance.Filled" Color="BadgeColor.Warning">Redacted</FluentBadge>
        <span class="monospace">@Content.ContentKind</span>
        <span class="monospace">@Content.ReasonCode</span>
    </span>
}
else
{
    <div class="protected-content-panel"
         role="status"
         aria-label="@($"Protected content redacted. Reason code {Content.ReasonCode}. Stage {Content.Stage}.")"
         style="border: 1px solid var(--neutral-stroke-rest); border-radius: 6px; padding: 12px; background: var(--neutral-layer-2);">
        <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 8px;">
            <FluentBadge Appearance="BadgeAppearance.Filled" Color="BadgeColor.Warning">Redacted</FluentBadge>
            <strong>@Content.Placeholder</strong>
        </div>
        <dl style="display: grid; grid-template-columns: max-content 1fr; gap: 4px 12px; margin: 0;">
            <dt>Content</dt>
            <dd class="monospace">@Content.ContentKind</dd>
            <dt>Reason</dt>
            <dd class="monospace">@Content.ReasonCode</dd>
            <dt>Stage</dt>
            <dd class="monospace">@Content.Stage</dd>
            <dt>Retryable</dt>
            <dd>@(Content.Retryable ? "Yes" : "No")</dd>
            <dt>Permanent</dt>
            <dd>@(Content.Permanent ? "Yes" : "No")</dd>
            @if (Content.SequenceNumber is not null)
            {
                <dt>Sequence</dt>
                <dd class="monospace">@Content.SequenceNumber</dd>
            }
            @if (!string.IsNullOrWhiteSpace(Content.CorrelationId))
            {
                <dt>Correlation</dt>
                <dd class="monospace">@Content.CorrelationId</dd>
            }
            <dt>Next action</dt>
            <dd>@Content.SafeNextAction</dd>
        </dl>
    </div>
}

@code {
    [Parameter, EditorRequired]
    public AdminRedactedContent Content { get; set; } = null!;

    [Parameter]
    public bool Compact { get; set; }
}
