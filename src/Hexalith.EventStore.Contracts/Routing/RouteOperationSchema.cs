using System.Text.Json;
using System.Text.Json.Schema;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Exports actual serializer contracts without instantiating payloads or retaining payload values.</summary>
public static class RouteOperationSchema
{
    /// <summary>Creates and seals an owner's request/result shapes under its actual serialization settings.</summary>
    public static RouteOperationContract Create(RouteContractDeclaration route, Type request, Type result,
        string ingress, string eligibility, JsonSerializerOptions serializer, JsonSchemaExporterOptions? schemaOptions = null)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(serializer);
        var options = new JsonSerializerOptions(serializer);
        options.MakeReadOnly(populateMissingResolver: true);
        var operation = new RouteOperationContract(route.RouteId, route.ContractVersion, ingress, eligibility,
            JsonSerializer.SerializeToElement(options.GetJsonSchemaAsNode(request, schemaOptions)),
            JsonSerializer.SerializeToElement(options.GetJsonSchemaAsNode(result, schemaOptions)), string.Empty,
            route.Kind, route.Domain, route.MessageType ?? route.ProjectionType);
        return operation with { Digest = RouteCatalogCodec.ComputeOperationContractDigest(operation) };
    }
}
