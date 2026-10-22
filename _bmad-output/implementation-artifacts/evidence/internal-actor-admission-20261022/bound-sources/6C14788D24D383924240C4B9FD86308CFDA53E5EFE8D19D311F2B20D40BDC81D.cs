using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Reports whether named routes own a rebuild and whether every required route is durable.</summary>
internal sealed record NamedProjectionRebuildResult(
    bool Owned,
    bool Succeeded,
    IReadOnlyList<ProjectionDispatchOutcome> Outcomes,
    IReadOnlyList<string> LifecycleProjectionTypes) {
    /// <summary>Gets a value indicating whether the durable result is known terminal.</summary>
    public bool IsTerminalFailure => Owned
        && !Succeeded
        && Outcomes.Count > 0
        && Outcomes.Any(static outcome => outcome.Status == ProjectionDispatchStatus.Failed);
}
