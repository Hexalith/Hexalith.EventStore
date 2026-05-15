namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>
/// Stable public reason codes for stream replay/read and projection rebuild failures.
/// </summary>
public static class StreamReplayReasonCodes {
    /// <summary>Requested sequence range is malformed.</summary>
    public const string InvalidRange = "invalid-range";

    /// <summary>Continuation token is malformed or cannot be validated.</summary>
    public const string InvalidContinuation = "invalid-continuation";

    /// <summary>Continuation token is bound to a different request shape.</summary>
    public const string TokenRequestMismatch = "token-request-mismatch";

    /// <summary>Caller is not authorized for the requested tenant.</summary>
    public const string UnauthorizedTenant = "unauthorized-tenant";

    /// <summary>Caller is not authorized for the requested replay scope.</summary>
    public const string ForbiddenReplayScope = "forbidden-replay-scope";

    /// <summary>The requested stream does not exist.</summary>
    public const string MissingStream = "missing-stream";

    /// <summary>An expected event is missing from the stream.</summary>
    public const string MissingEvent = "missing-event";

    /// <summary>A persisted event is corrupt or unreadable.</summary>
    public const string CorruptEvent = "corrupt-event";

    /// <summary>Protected payload material is unavailable to the replay path.</summary>
    public const string ProtectedPayloadUnavailable = "protected-payload-unavailable";

    /// <summary>The domain projection apply path rejected the page.</summary>
    public const string ProjectionApplyRejected = "projection-apply-rejected";

    /// <summary>A checkpoint write conflicted with a concurrent writer.</summary>
    public const string CheckpointConflict = "checkpoint-conflict";

    /// <summary>A checkpoint write was stale.</summary>
    public const string StaleCheckpoint = "stale-checkpoint";

    /// <summary>A checkpoint read observed drift from available stream progress.</summary>
    public const string CheckpointDrift = "checkpoint-drift";

    /// <summary>The checkpoint store is unavailable.</summary>
    public const string CheckpointUnavailable = "checkpoint-unavailable";

    /// <summary>Background polling and operator rebuild would conflict.</summary>
    public const string PollerRebuildConflict = "poller-rebuild-conflict";

    /// <summary>The rebuild operation was not found.</summary>
    public const string RebuildOperationNotFound = "rebuild-operation-not-found";

    /// <summary>The rebuild operation was canceled.</summary>
    public const string RebuildCanceled = "rebuild-canceled";

    /// <summary>The rebuild operation is paused.</summary>
    public const string RebuildPaused = "rebuild-paused";

    /// <summary>The domain projection apply path failed.</summary>
    public const string DomainFailure = "domain-failure";

    /// <summary>A transient retryable failure occurred.</summary>
    public const string RetryableTransientFailure = "retryable-transient-failure";

    /// <summary>The EventStore service is unavailable.</summary>
    public const string ServiceUnavailable = "service-unavailable";
}
