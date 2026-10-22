namespace Hexalith.EventStore.Server.Tests.Projections;

internal sealed class ProjectionRebuildAggregateState {
    public string Id { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int EventCount { get; set; }

    public void Apply(ProjectionStatusChanged @event) {
        Id = @event.AggregateId;
        Status = @event.Status;
        EventCount++;
    }
}
