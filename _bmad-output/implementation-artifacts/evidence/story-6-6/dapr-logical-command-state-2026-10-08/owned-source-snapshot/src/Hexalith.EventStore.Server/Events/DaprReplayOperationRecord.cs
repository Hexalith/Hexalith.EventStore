namespace Hexalith.EventStore.Server.Events;

/// <summary>Contains the dedicated operation actor's bounded current generation and committed page pointer.</summary>
/// <param name="TenantId">The canonical tenant.</param>
/// <param name="OperationId">The stable operation identity.</param>
/// <param name="OwnerId">The trusted host's current owner.</param>
/// <param name="Generation">The positive takeover fence.</param>
/// <param name="SourceBindingHash">The fixed source identity.</param>
/// <param name="RegistryFingerprint">The pinned active registry.</param>
/// <param name="TargetSequence">The immutable operation target.</param>
/// <param name="PageOrdinal">The last committed page, zero before page one.</param>
/// <param name="CompletedSequence">The last committed source sequence.</param>
/// <param name="Accumulator">The complete committed accumulator.</param>
/// <param name="IsComplete">Whether the exact target/final result committed.</param>
internal sealed record DaprReplayOperationRecord(string TenantId, string OperationId, string OwnerId, long Generation,
    byte[] SourceBindingHash, byte[] RegistryFingerprint, long TargetSequence, long PageOrdinal,
    long CompletedSequence, byte[] Accumulator, bool IsComplete)
{
    /// <summary>Gets the exact supplied reconstruction binding, absent on event-source-only operations.</summary>
    public byte[]? ReconstructionBindingHash { get; init; }
    /// <summary>Gets the canonical committed state digest, absent on event-source-only operations.</summary>
    public byte[]? CanonicalStateHash { get; init; }
    /// <summary>Gets the cumulative effective-event chain from operation genesis.</summary>
    public byte[]? EffectiveChainHash { get; init; }
    /// <summary>Gets the exact cumulative page transcript from operation genesis.</summary>
    public byte[]? TranscriptHash { get; init; }
    /// <summary>Gets the optional private exact command route fixed before Begin.</summary>
    public byte[]? CommandRouteHash { get; init; }
    /// <summary>Gets the terminal retained purpose-07 proof digest, only on completed command-bound operations.</summary>
    public byte[]? CommandProofHash { get; init; }
}
