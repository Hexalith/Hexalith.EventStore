namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Exact installed technical receiver identity, covered separately by activation-binding v2.</summary>
/// <param name="AppId">Actual receiving sidecar identity.</param>
/// <param name="FenceVersion">Explicit independently installed composite implementation.</param>
/// <param name="Endpoints">Complete SDK technical action inventory, without executable domain entries.</param>
public sealed record RouteCatalogTechnicalHttpReceiver(string AppId, string FenceVersion, IReadOnlyList<RouteCatalogTechnicalHttpEndpoint> Endpoints)
{
    /// <summary>Gets the receiver owner's actual caller/path/verb witnesses, including technical proxies outside authority scopes.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<RouteCatalogDeclaredInvocation>? Invocations { get; init; }
}
