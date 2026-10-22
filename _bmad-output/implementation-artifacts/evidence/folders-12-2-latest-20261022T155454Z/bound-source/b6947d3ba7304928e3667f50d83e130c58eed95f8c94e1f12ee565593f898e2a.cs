namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>Inspects the native guarded binding for one exact selected owner through the existing bound-owner workload route.</summary>
/// <param name="Selection">The exact content-free generation selected by the owner query, never a grant.</param>
public sealed record InspectProjectionCurrentQuery(ProjectionOwnerSelection Selection)
{
    /// <summary>The exact additive technical inspection discriminator, requiring explicit workload enrollment.</summary>
    public const string QueryType = "Hexalith.EventStore.Queries.InspectProjectionCurrent.v1";
}
