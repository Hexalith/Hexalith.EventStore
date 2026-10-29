namespace Hexalith.EventStore.Contracts.Routing;

/// <summary>Exact required host and its independently observed executable, authentication and topology identities.</summary>
/// <param name="HostId">Unique logical host, including generated API hosts.</param>
/// <param name="AppId">Exact Dapr application identifier.</param>
/// <param name="InventoryDigest">Digest of the compiled declarations and executable targets.</param>
/// <param name="TopologyDigest">Digest of loaded components, scopes, sidecar arguments and ACL operations.</param>
/// <param name="AuthenticationFingerprint">Shared authentication-profile fingerprint.</param>
public sealed record RouteCatalogHostIdentity(string HostId, string AppId, string InventoryDigest,
    string TopologyDigest, string AuthenticationFingerprint);
