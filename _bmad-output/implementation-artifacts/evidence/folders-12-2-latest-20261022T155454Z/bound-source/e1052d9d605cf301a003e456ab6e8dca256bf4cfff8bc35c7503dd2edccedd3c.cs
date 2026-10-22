namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>
/// Command lifecycle status with explicit integer assignments for stable serialization.
/// </summary>
public enum CommandStatus {
    /// <summary>Written at API layer before actor invocation.</summary>
    Received = 0,

    /// <summary>Actor begins 5-step delegation.</summary>
    Processing = 1,

    /// <summary>Events persisted to state store.</summary>
    EventsStored = 2,

    /// <summary>Events published to pub/sub topic.</summary>
    EventsPublished = 3,

    /// <summary>Terminal — all events stored and published.</summary>
    Completed = 4,

    /// <summary>
    /// Terminal — command rejected before completion.
    /// Domain rejections set <see cref="CommandStatusRecord.RejectionEventType"/>;
    /// infrastructure rejections set <see cref="CommandStatusRecord.FailureReason"/>.
    /// </summary>
    Rejected = 5,

    /// <summary>Terminal — events stored but pub/sub permanently failed.</summary>
    PublishFailed = 6,

    /// <summary>Terminal — processing exceeded configured timeout.</summary>
    TimedOut = 7,
}
