namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// The durable outcome of offering a delivery to a checkpointed projection.
/// </summary>
/// <remarks>
/// Every member is committed (or proven unchanged) before it is returned, so the caller may acknowledge the
/// delivery. Retryable failures are thrown as <see cref="CheckpointedProjectionUnavailableException"/> instead.
/// </remarks>
public enum CheckpointedProjectionOutcome {
    /// <summary>The exact-next delivery committed its transition and checkpoint atomically.</summary>
    Applied = 1,

    /// <summary>The delivery matched accepted or authoritative identity; nothing changed.</summary>
    Duplicate = 2,

    /// <summary>The delivery was merged as pending evidence of a gapped projection.</summary>
    Pending = 3,

    /// <summary>Fenced recovery replayed authoritative history and restored a healthy checkpoint.</summary>
    Recovered = 4,

    /// <summary>The projection is terminally corrupt; the record denies every consumer decision.</summary>
    Corrupt = 5,
}
