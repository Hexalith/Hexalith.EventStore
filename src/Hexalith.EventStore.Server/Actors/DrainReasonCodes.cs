namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Stable bounded reason codes for drain activity tags and structured logs. Both production
/// code and tests must reference these symbols rather than string literals so that a future
/// rename keeps both halves consistent.
/// </summary>
internal static class DrainReasonCodes
{
    public const string EventCountMismatch = "drain_event_count_mismatch";
    public const string MissingEvent = "drain_missing_event";
    public const string PublishFailed = "drain_publish_failed";
    public const string Unknown = "unknown";
}
