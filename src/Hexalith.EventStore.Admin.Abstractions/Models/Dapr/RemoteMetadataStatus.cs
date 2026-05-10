using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Dapr;

/// <summary>
/// Indicates the outcome of a remote EventStore DAPR sidecar metadata query.
/// </summary>
/// <remarks>
/// <para>Ordinals are pinned explicitly so the wire format is stable across versions
/// and external clients. New members MUST be appended with the next unused ordinal;
/// existing members MUST NOT be reordered or renumbered.</para>
/// <para>Serializes by name via <see cref="JsonStringEnumConverter"/> regardless of
/// caller configuration, so JSON consumers see <c>"Available"</c> rather than <c>1</c>.</para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RemoteMetadataStatus {
    /// <summary>No remote endpoint configured; only local sidecar queried.</summary>
    NotConfigured = 0,

    /// <summary>Remote endpoint configured and successfully queried.</summary>
    Available = 1,

    /// <summary>Remote endpoint configured but query failed (transport, timeout, or non-success status).</summary>
    Unreachable = 2,

    /// <summary>Remote endpoint responded but the body could not be parsed or lacked required shape.</summary>
    InvalidPayload = 3,
}
