namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Candidate authoritative reservation outcomes; Unknown/Unavailable never authorizes encryption or a new reference.</summary>
public enum InteractionOccurrenceReservationStatus
{
    /// <summary>Original unique reservation may be used only under current completion lease.</summary>
    Reserved,
    /// <summary>Original exact ciphertext is retained; retry returns it without another core invocation.</summary>
    Sealed,
    /// <summary>Exact immutable intent mismatch.</summary>
    Conflict,
    /// <summary>Authoritative unique-reference collision; no reservation/effect occurred.</summary>
    ReferenceCollision,
    /// <summary>Missing/current/rollback/writer authority; no new encryption.</summary>
    Unavailable,
}
