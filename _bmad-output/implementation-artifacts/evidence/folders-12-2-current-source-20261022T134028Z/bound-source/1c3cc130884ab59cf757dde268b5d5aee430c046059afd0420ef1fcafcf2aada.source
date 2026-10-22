@namespace Hexalith.EventStore.Admin.UI.Components.Shared
@using Hexalith.EventStore.Admin.Abstractions.Models.Health
@using Hexalith.EventStore.Admin.Abstractions.Models.Streams
@using Hexalith.EventStore.Contracts.Commands

<span class="status-badge" style="display: inline-flex; align-items: center; gap: 4px; font-size: 12px; color: @_effectiveColor;"
      aria-label="@($"{AriaLabelPrefix}: {_effectiveLabel}")">
    <span class="status-badge__icon" aria-hidden="true">@_effectiveIcon</span>
    <span class="status-badge__label">@_effectiveLabel</span>
</span>

@code {
    [Parameter]
    public string Status { get; set; } = "Unknown";

    [Parameter]
    public string Severity { get; set; } = "neutral";

    [Parameter]
    public string AriaLabelPrefix { get; set; } = "Command status";

    [Parameter]
    public StatusDisplayConfig? DisplayConfig { get; set; }

    private string _effectiveLabel => DisplayConfig?.Label ?? Status;
    private string _effectiveColor => DisplayConfig?.CssColor ?? SeverityToColor(Severity);
    private string _effectiveIcon => DisplayConfig?.Icon ?? SeverityToIcon(Severity);

    private static string SeverityToColor(string severity) => severity.ToLowerInvariant() switch
    {
        "success" => "var(--hexalith-status-success)",
        "inflight" => "var(--hexalith-status-inflight)",
        "warning" => "var(--hexalith-status-warning)",
        "error" => "var(--hexalith-status-error)",
        _ => "var(--hexalith-status-neutral)",
    };

    private static string SeverityToIcon(string severity) => severity.ToLowerInvariant() switch
    {
        "success" => "\u2714",
        "inflight" => "\u25CB",
        "warning" => "\u26A0",
        "error" => "\u2716",
        _ => "\u2022",
    };

    /// <summary>
    /// Display configuration for a status badge, decoupled from specific enum types.
    /// </summary>
    public record StatusDisplayConfig(string Label, string Icon, string CssColor)
    {
        /// <summary>
        /// Creates a <see cref="StatusDisplayConfig"/> from a <see cref="StreamStatus"/> value.
        /// </summary>
        public static StatusDisplayConfig FromStreamStatus(StreamStatus status) => status switch
        {
            StreamStatus.Active => new("Active", "\u25CF", "var(--hexalith-status-success)"),
            StreamStatus.Idle => new("Idle", "\u25CB", "var(--hexalith-status-neutral)"),
            StreamStatus.Tombstoned => new("Tombstoned", "\u2612", "var(--hexalith-status-error)"),
            _ => new(status.ToString(), "\u2022", "var(--hexalith-status-neutral)"),
        };

        /// <summary>
        /// Creates a <see cref="StatusDisplayConfig"/> from a <see cref="HealthStatus"/> value.
        /// </summary>
        public static StatusDisplayConfig FromHealthStatus(HealthStatus status) => status switch
        {
            HealthStatus.Healthy => new("Healthy", "\u2714", "var(--hexalith-status-success)"),
            HealthStatus.Degraded => new("Degraded", "\u26A0", "var(--hexalith-status-warning)"),
            HealthStatus.Unhealthy => new("Unhealthy", "\u2716", "var(--hexalith-status-error)"),
            _ => new(status.ToString(), "\u2022", "var(--hexalith-status-neutral)"),
        };

        /// <summary>
        /// Creates a <see cref="StatusDisplayConfig"/> from a <see cref="CommandStatus"/> value.
        /// </summary>
        public static StatusDisplayConfig FromCommandStatus(CommandStatus status) => status switch
        {
            CommandStatus.Completed => new("Completed", "\u25CF", "var(--hexalith-status-success)"),
            CommandStatus.Received => new("Received", "\u25CB", "var(--hexalith-status-neutral)"),
            CommandStatus.Processing => new("Processing", "\u25CB", "var(--hexalith-status-inflight)"),
            CommandStatus.EventsStored => new("Events Stored", "\u25CB", "var(--hexalith-status-inflight)"),
            CommandStatus.EventsPublished => new("Events Published", "\u25CB", "var(--hexalith-status-inflight)"),
            CommandStatus.Rejected => new("Rejected", "\u2612", "var(--hexalith-status-error)"),
            CommandStatus.PublishFailed => new("Publish Failed", "\u2612", "var(--hexalith-status-error)"),
            CommandStatus.TimedOut => new("Timed Out", "\u2612", "var(--hexalith-status-warning)"),
            _ => new(status.ToString(), "\u2022", "var(--hexalith-status-neutral)"),
        };

        /// <summary>
        /// Creates a <see cref="StatusDisplayConfig"/> from a <see cref="TimelineEntryType"/> value.
        /// </summary>
        public static StatusDisplayConfig FromTimelineEntryType(TimelineEntryType entryType) => entryType switch
        {
            TimelineEntryType.Command => new("Command", "\u25B6", "var(--hexalith-status-inflight)"),
            TimelineEntryType.Event => new("Event", "\u26A1", "var(--hexalith-status-success)"),
            // Query entries may not be produced by the server yet — filter chip will show zero results
            TimelineEntryType.Query => new("Query", "\u2315", "var(--hexalith-status-neutral)"),
            _ => new(entryType.ToString(), "\u2022", "var(--hexalith-status-neutral)"),
        };
    }
}
