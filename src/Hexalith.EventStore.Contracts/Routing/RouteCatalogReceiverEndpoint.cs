using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Actual route, method and authorization metadata; admitted endpoints are finite concrete operations only.</summary>
/// <param name="Pattern">Actual route pattern.</param>
/// <param name="Methods">Actual allowed HTTP verbs.</param>
/// <param name="Operation">Canonical authenticated operation, if enrolled.</param>
/// <param name="Policies">Actual authorization policy names.</param>
/// <param name="Anonymous">Whether the endpoint allows anonymous execution.</param>
/// <param name="IsPattern">Whether the route contains parameter, catch-all or constraint syntax.</param>
/// <param name="Admitted">Whether the installed fence permits this exact endpoint over Dapr service invocation.</param>
public sealed record RouteCatalogReceiverEndpoint(string Pattern, IReadOnlyList<string> Methods, string? Operation,
    IReadOnlyList<string> Policies, bool Anonymous, bool IsPattern, bool Admitted)
{
    /// <summary>Gets the actual typed domain owner and scope extraction code, when explicitly enrolled.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RouteCatalogDomainAuxiliaryTransport? DomainAuxiliary { get; init; }
    /// <summary>Gets an actual SDK subscription callback, without granting workload or public message eligibility.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RouteCatalogSidecarTransport? Sidecar { get; init; }
    /// <summary>Gets independently captured SDK controller/code/verb identity, never message eligibility.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RouteCatalogHttpTransport? Http { get; init; }
    /// <summary>Gets the independently observed existing native SDK actor transport, when explicitly qualified.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RouteCatalogNativeActorTransport? NativeActor { get; init; }
    /// <summary>Gets the closed SDK discovery or signed notification callback binding.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RouteCatalogDaprCallbackTransport? DaprCallback { get; init; }
    /// <summary>Gets concrete owner ACL paths independently matched against this actual compiled framework action.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<RouteCatalogHttpGrant>? TechnicalGrants { get; init; }
}
