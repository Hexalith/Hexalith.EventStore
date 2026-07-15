using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Reports whether named routes own a rebuild and whether every required route is durable.</summary>
internal sealed record NamedProjectionRebuildResult(
    bool Owned,
    bool Succeeded,
    IReadOnlyList<ProjectionDispatchOutcome> Outcomes);
