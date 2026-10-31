namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Exact required host and its independently observed executable, authentication and topology identities.</summary>
/// <param name="HostId">Unique physical host, including independently enrolled replicas and generated API hosts.</param>
/// <param name="AppId">Exact Dapr application identifier.</param>
/// <param name="InventoryDigest">Digest of the compiled declarations and executable targets.</param>
/// <param name="TopologyDigest">Digest of loaded components, scopes, sidecar arguments and ACL operations.</param>
/// <param name="AuthenticationFingerprint">Shared authentication-profile fingerprint.</param>
public sealed record RouteCatalogHostIdentity(string HostId, string AppId, string InventoryDigest,
    string TopologyDigest, string AuthenticationFingerprint)
{
    /// <summary>Gets the independently observed executable owner digest required for equivalent physical replicas.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ExecutableDigest { get; init; }
}
