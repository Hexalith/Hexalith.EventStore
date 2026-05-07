namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Indicates the outcome of a remote EventStore DAPR sidecar metadata query.
/// </summary>
public enum RemoteMetadataStatus {
    /// <summary>No remote endpoint configured; only local sidecar queried.</summary>
    NotConfigured,

    /// <summary>Remote endpoint configured and successfully queried.</summary>
    Available,

    /// <summary>Remote endpoint configured but query failed (transport, timeout, or non-success status).</summary>
    Unreachable,

    /// <summary>Remote endpoint responded but the body could not be parsed or lacked required shape.</summary>
    InvalidPayload,

    /// <summary>Remote sidecar reachable but its app metadata is still initializing/not yet ready.</summary>
    Initializing,
}
