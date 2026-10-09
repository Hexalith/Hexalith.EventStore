namespace Hexalith.EventStore.DomainService;

/// <summary>
/// Identifies one explicitly declared public route for the domain-service startup inventory.
/// </summary>
/// <param name="Route">The exact, literal route pattern declared by the host.</param>
internal sealed record EventStorePublicEndpointMetadata(string Route);
