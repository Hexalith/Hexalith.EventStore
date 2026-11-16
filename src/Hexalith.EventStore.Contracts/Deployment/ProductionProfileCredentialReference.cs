namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>OpenBao logical reference only; contains no secret value.</summary>
/// <param name="Id">The required id.</param>
/// <param name="Store">The required store.</param>
/// <param name="LogicalName">The required logical name.</param>
/// <param name="MapKey">The required map key.</param>
/// <param name="Generation">The required generation.</param>
/// <param name="ConsumerAppIds">The required consumer app ids.</param>
/// <param name="ComponentIds">The required component ids.</param>
public sealed record ProductionProfileCredentialReference(
    string Id,
    string Store,
    string LogicalName,
    string MapKey,
    string Generation,
    IReadOnlyList<string> ConsumerAppIds,
    IReadOnlyList<string> ComponentIds);
