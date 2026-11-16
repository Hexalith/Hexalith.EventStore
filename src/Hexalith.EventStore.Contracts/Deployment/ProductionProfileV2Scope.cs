namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Actual instance scope; absent identifiers are explicitly null only when inapplicable.</summary>
/// <param name="Kind">One of environment, cluster, node or namespace.</param>
/// <param name="EnvironmentId">The actual containing environment.</param>
/// <param name="ClusterId">The cluster for cluster, node and namespace scope; otherwise null.</param>
/// <param name="NodeId">The actual node for node scope; otherwise null.</param>
/// <param name="Namespace">The actual namespace for namespace scope; otherwise null.</param>
public sealed record ProductionProfileV2Scope(
    string Kind,
    string EnvironmentId,
    string? ClusterId,
    string? NodeId,
    string? Namespace);
