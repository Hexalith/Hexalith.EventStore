namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Independently observed receiver fence and the complete actual endpoint inventory.</summary>
/// <param name="FenceVersion">Installed enforcement implementation.</param>
/// <param name="Endpoints">Every actual mapped endpoint, including explicitly denied prefix overlaps.</param>
public sealed record RouteCatalogReceiverSurface(string FenceVersion, IReadOnlyList<RouteCatalogReceiverEndpoint> Endpoints)
{
    /// <summary>Gets the separately opted-in generated human GET inventory; absent for finite service dispatch.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<RouteCatalogGeneratedRestEndpoint>? GeneratedRest { get; init; }

    /// <summary>Gets separately opted-in installed SDK technical actions; absent for unchanged finite-v1 receivers.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<RouteCatalogTechnicalHttpEndpoint>? TechnicalHttp { get; init; }
}
