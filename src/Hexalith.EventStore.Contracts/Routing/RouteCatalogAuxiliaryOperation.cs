using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Inventory of an existing replay, reconstruction or metadata call; never a message declaration.</summary>
/// <param name="AppId">Exact receiver.</param>
/// <param name="Method">Existing canonical receiver method.</param>
/// <param name="Operation">Existing credential operation.</param>
/// <param name="Domain">Exact owning domain.</param>
public sealed record RouteCatalogAuxiliaryOperation(string AppId, string Method, string Operation, string Domain)
{
    /// <summary>Gets the actual typed domain owner and scope extraction code, when explicitly enrolled.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RouteCatalogDomainAuxiliaryTransport? DomainAuxiliary { get; init; }
    /// <summary>Gets an actual SDK subscription callback, without granting workload or public message eligibility.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RouteCatalogSidecarTransport? Sidecar { get; init; }
    /// <summary>Gets the closed existing SDK HTTP transport binding; absent for legacy auxiliary calls.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RouteCatalogHttpTransport? Http { get; init; }
    /// <summary>Gets the independently observed existing native SDK actor transport, when explicitly qualified.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RouteCatalogNativeActorTransport? NativeActor { get; init; }
}
