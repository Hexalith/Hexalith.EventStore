@namespace Hexalith.EventStore.Admin.UI.Components.Shared

<div style="display: flex; align-items: center; gap: 12px; font-size: 13px;">
    <FluentBadge Appearance="@_badgeAppearance"
                 Color="@_badgeColor"
                 aria-label="@($"Connection status: {ConnectionStatus}")"
                 Content="@ConnectionStatus.ToString()" />
    <span style="color: var(--colorNeutralForeground3);">@HealthSummary</span>
</div>

@code {
    public enum ConnectionStatusType
    {
        Unknown,
        Connected,
        Disconnected,
    }

    [Parameter]
    public ConnectionStatusType ConnectionStatus { get; set; } = ConnectionStatusType.Unknown;

    [Parameter]
    public string HealthSummary { get; set; } = "\u2014";

    private BadgeAppearance _badgeAppearance => ConnectionStatus switch
    {
        ConnectionStatusType.Connected => BadgeAppearance.Filled,
        ConnectionStatusType.Disconnected => BadgeAppearance.Ghost,
        _ => BadgeAppearance.Filled,
    };

    private BadgeColor _badgeColor => ConnectionStatus switch
    {
        ConnectionStatusType.Connected => BadgeColor.Success,
        ConnectionStatusType.Disconnected => BadgeColor.Danger,
        _ => BadgeColor.Subtle,
    };
}
