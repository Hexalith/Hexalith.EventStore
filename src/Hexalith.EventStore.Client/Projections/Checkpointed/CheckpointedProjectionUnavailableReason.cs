namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// Why a checkpointed projection operation could not reach a durable outcome.
/// </summary>
public enum CheckpointedProjectionUnavailableReason {
    /// <summary>The state provider failed.</summary>
    ProviderUnavailable = 1,

    /// <summary>Optimistic concurrency conflicts exhausted the total attempt budget.</summary>
    ConcurrencyExhausted = 2,

    /// <summary>
    /// Authoritative history could not be read, returned a truncated page without progress, or has not yet
    /// reached the requested sequence.
    /// </summary>
    HistoryUnavailable = 3,

    /// <summary>Another worker holds an unexpired recovery lease.</summary>
    RecoveryLeaseHeld = 4,

    /// <summary>This worker's recovery lease expired or was superseded before its final compare-and-swap.</summary>
    RecoveryLeaseLost = 5,

    /// <summary>The persisted record is malformed and cannot be advanced safely.</summary>
    MalformedRecord = 6,

    /// <summary>The domain transition could not apply an authoritative event.</summary>
    TransitionFailed = 7,

    /// <summary>The record a pending delivery committed is no longer present, so recovery cannot prove anything.</summary>
    RecordMissing = 8,
}
