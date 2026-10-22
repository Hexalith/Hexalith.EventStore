@namespace Hexalith.EventStore.Admin.UI.Components
@using Hexalith.EventStore.Admin.Abstractions.Models.Projections
@using Hexalith.EventStore.Admin.UI.Components.Shared

<StatusBadge DisplayConfig="@GetDisplayConfig(Status)"
             AriaLabelPrefix="Projection status" />

@code {
    [Parameter, EditorRequired]
    public ProjectionStatusType Status { get; set; }

    private static StatusBadge.StatusDisplayConfig GetDisplayConfig(ProjectionStatusType status) => status switch
    {
        ProjectionStatusType.Running => new("Running", "\u2714", "var(--hexalith-status-success)"),
        ProjectionStatusType.Paused => new("Paused", "\u23F8", "var(--hexalith-status-warning)"),
        ProjectionStatusType.Error => new("Error", "\u2716", "var(--hexalith-status-error)"),
        ProjectionStatusType.Rebuilding => new("Rebuilding", "\u21BB", "var(--hexalith-status-inflight)"),
        _ => new(status.ToString(), "\u2022", "var(--hexalith-status-neutral)"),
    };
}
