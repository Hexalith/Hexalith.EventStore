namespace Hexalith.EventStore.Contracts.Deployment;

/// <summary>Public credential-free endpoint; never a provider connection string.</summary>
/// <param name="Id">The required id.</param>
/// <param name="ComponentId">The required component id.</param>
/// <param name="Address">The required address.</param>
public sealed record ProductionProfileEndpoint(
    string Id,
    string ComponentId,
    string Address);
