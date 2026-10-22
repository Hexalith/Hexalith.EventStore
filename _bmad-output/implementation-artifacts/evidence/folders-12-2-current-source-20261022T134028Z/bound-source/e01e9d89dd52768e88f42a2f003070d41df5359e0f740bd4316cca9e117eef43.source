namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Candidate writer lifecycle, with no reuse of an abandoned reference.</summary>
public enum InteractionOccurrenceWriterState
{
    /// <summary>Durable unique reference, no ciphertext yet.</summary>
    Reserved,
    /// <summary>Exact sealed result retained; writer outcome still unproved, unreadable to replay.</summary>
    SealedPending,
    /// <summary>Exact committed writer/source proof makes the original reference readable.</summary>
    Active,
    /// <summary>Exact authoritative never-written proof; reference remains permanently used.</summary>
    Aborted,
}
