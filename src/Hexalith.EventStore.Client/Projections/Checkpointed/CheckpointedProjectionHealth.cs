namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// The persisted health of a checkpointed projection record.
/// </summary>
/// <remarks>
/// Zero is deliberately not a member, so an absent or defaulted value is malformed rather than healthy.
/// Only <see cref="Healthy"/> may authorize a consumer decision.
/// </remarks>
public enum CheckpointedProjectionHealth {
    /// <summary>The projection is contiguous through its checkpoint and may be trusted.</summary>
    Healthy = 1,

    /// <summary>A future delivery proved a gap; recovery must replay authoritative history first.</summary>
    Gapped = 2,

    /// <summary>A delivery or authoritative history contradicted accepted identity; terminal.</summary>
    Corrupt = 3,

    /// <summary>The projection state could not be read or proven; never persisted by the writer.</summary>
    Unavailable = 4,
}
