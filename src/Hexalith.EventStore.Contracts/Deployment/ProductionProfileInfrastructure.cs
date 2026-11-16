namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Provider, topology and tool pins only; excludes release and environment-current bindings.</summary>
/// <param name="Providers">The required providers.</param>
/// <param name="Tools">The required tools.</param>
/// <param name="Topology">The required topology.</param>
public sealed record ProductionProfileInfrastructure(
    IReadOnlyList<ProductionProfileProvider> Providers,
    IReadOnlyList<ProductionProfileTool> Tools,
    ProductionProfileTopology Topology);
