namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Independently observed receiver fence and the complete actual endpoint inventory.</summary>
/// <param name="FenceVersion">Installed enforcement implementation.</param>
/// <param name="Endpoints">Every actual mapped endpoint, including explicitly denied prefix overlaps.</param>
public sealed record RouteCatalogReceiverSurface(string FenceVersion, IReadOnlyList<RouteCatalogReceiverEndpoint> Endpoints);
