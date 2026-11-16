namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Component version in the claimed protection qualification tuple.</summary>
/// <param name="Id">The required id.</param>
/// <param name="Version">The required version.</param>
public sealed record ProductionProfileComponentVersion(
    string Id,
    string Version);
