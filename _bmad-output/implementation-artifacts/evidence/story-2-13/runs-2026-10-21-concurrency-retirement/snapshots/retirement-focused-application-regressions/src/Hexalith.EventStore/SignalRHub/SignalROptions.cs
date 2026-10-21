using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.SignalRHub;

/// <summary>
/// Configuration options for the EventStore SignalR hub.
/// Bound from configuration section <c>EventStore:SignalR</c>.
/// </summary>
public class SignalROptions {
    /// <summary>
    /// Gets a value indicating whether SignalR real-time notifications are enabled.
    /// Default: <c>false</c> (disabled). Must be explicitly enabled in configuration.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the Redis connection string for the SignalR backplane (multi-instance deployments).
    /// If <c>null</c>, falls back to environment variable <c>EVENTSTORE_SIGNALR_REDIS</c>.
    /// If neither is set, the backplane is disabled (single-instance mode).
    /// </summary>
    public string? BackplaneRedisConnectionString { get; init; }

    /// <summary>
    /// Gets the maximum number of SignalR groups a single client connection can join.
    /// Prevents resource exhaustion from flooding. Default: 50.
    /// </summary>
    public int MaxGroupsPerConnection { get; init; } = 50;

    /// <summary>
    /// Gets the maximum number of key/value entries carried in a metadata-rich
    /// projection-changed detail broadcast. Entries beyond this cap are clipped (the
    /// broadcast still fires). Keeps the channel metadata-only and bounded. Default and ceiling: 16.
    /// </summary>
    public int MaxDetailMetadataEntries { get; init; } = ProjectionChangedMetadataLimits.MaximumEntries;

    /// <summary>
    /// Gets the maximum serialized UTF-8 byte size (compact JSON, including punctuation and escaping) in a
    /// metadata-rich projection-changed detail broadcast. Entries that would exceed this cap
    /// are clipped (the broadcast still fires). Keeps the channel metadata-only and bounded.
    /// Default: 2048. Options may lower this ceiling; the empty object requires at least 2 bytes.
    /// </summary>
    public int MaxDetailMetadataBytes { get; init; } = ProjectionChangedMetadataLimits.MaximumSerializedUtf8Bytes;
}
