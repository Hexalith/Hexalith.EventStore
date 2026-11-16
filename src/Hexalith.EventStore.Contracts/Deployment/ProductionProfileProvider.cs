namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Pinned infrastructure provider; roles do not imply separate deployments.</summary>
/// <param name="Id">The required id.</param>
/// <param name="Provider">The required provider.</param>
/// <param name="Version">The required version.</param>
/// <param name="ImmutableDigest">The required immutable digest.</param>
/// <param name="Topology">The required topology.</param>
/// <param name="Roles">The required roles.</param>
public sealed record ProductionProfileProvider(
    string Id,
    string Provider,
    string Version,
    string ImmutableDigest,
    string Topology,
    IReadOnlyList<string> Roles);
