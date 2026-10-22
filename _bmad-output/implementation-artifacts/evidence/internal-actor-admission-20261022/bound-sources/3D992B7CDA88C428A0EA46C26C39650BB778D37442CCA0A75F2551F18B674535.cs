namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Internal actor-side result of <see cref="IAggregateActor.CreateManualSnapshotAsync"/>.
/// </summary>
/// <remarks>
/// The EventStore controller derives the deterministic, sequence-scoped operation id from
/// <see cref="SequenceNumber"/> when the outcome is <see cref="ManualSnapshotOutcome.Created"/>
/// or <see cref="ManualSnapshotOutcome.AlreadyCurrent"/>. The actor itself does not own
/// operation-id derivation.
/// </remarks>
/// <param name="Outcome">Classification of the manual snapshot attempt.</param>
/// <param name="SequenceNumber">Stream sequence number at the time of the call (0 when the stream is missing).</param>
/// <param name="SnapshotKey">The state-store snapshot key, when known.</param>
/// <param name="ReasonCode">Safe stable reason code on non-success outcomes; null otherwise.</param>
/// <param name="Message">Safe operator-facing message on non-success outcomes; null otherwise.</param>
public sealed record ManualSnapshotResult(
    ManualSnapshotOutcome Outcome,
    long SequenceNumber,
    string? SnapshotKey,
    string? ReasonCode,
    string? Message);

/// <summary>
/// Distinguishable outcomes from <see cref="IAggregateActor.CreateManualSnapshotAsync"/>.
/// </summary>
public enum ManualSnapshotOutcome {
    /// <summary>A fresh snapshot was staged and committed at the current stream sequence.</summary>
    Created,

    /// <summary>A snapshot already exists at the current stream sequence; no rewrite occurred.</summary>
    AlreadyCurrent,

    /// <summary>The aggregate stream does not exist.</summary>
    NotFound,

    /// <summary>An existing snapshot or rehydrated event tail could not be safely read (fail-closed).</summary>
    UnreadableProtected,

    /// <summary>An infrastructure failure occurred (state-store unavailable, etc.).</summary>
    InfrastructureFailure,
}
