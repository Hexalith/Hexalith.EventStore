namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Indicates the outcome of a remote EventStore DAPR sidecar metadata query.
/// </summary>
public enum RemoteMetadataStatus {
    /// <summary>No remote endpoint configured; only local sidecar queried.</summary>
    NotConfigured,

    /// <summary>Remote endpoint configured and successfully queried.</summary>
    Available,

    /// <summary>Remote endpoint configured but query failed (exception caught).</summary>
    Unreachable,
}
