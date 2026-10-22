namespace Hexalith.EventStore.Server.Actors;

/// <summary>Bounded decision returned across the admission-to-aggregate actor transport.</summary>
public enum IdempotencyAdmissionAuthorityDecision
{
    /// <summary>No decision was returned; the caller must use the existing validation path.</summary>
    Unknown = 0,

    /// <summary>The exact execution authority remains current.</summary>
    Current = 1,

    /// <summary>The exact execution authority is no longer current.</summary>
    Stale = 2,
}
