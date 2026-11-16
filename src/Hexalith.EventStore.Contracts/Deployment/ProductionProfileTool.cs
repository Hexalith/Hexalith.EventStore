namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Separately pinned deployment tool, outside provider currency evaluation.</summary>
/// <param name="Id">The required id.</param>
/// <param name="Version">The required version.</param>
/// <param name="ImmutableDigest">The required immutable digest.</param>
public sealed record ProductionProfileTool(
    string Id,
    string Version,
    string ImmutableDigest);
