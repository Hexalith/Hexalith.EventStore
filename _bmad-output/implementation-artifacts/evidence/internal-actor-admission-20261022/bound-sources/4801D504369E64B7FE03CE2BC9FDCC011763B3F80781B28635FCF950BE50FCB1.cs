namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Separately keyed ordinal owner obligation; Effective is immutable and cannot be revised by a later cycle.</summary>
/// <param name="Ordinal">Exact Ordinal.</param>
/// <param name="OwnerId">Exact OwnerId.</param>
/// <param name="ObligationDigest">Exact ObligationDigest.</param>
/// <param name="EffectiveReceiptId">Exact EffectiveReceiptId.</param>
public sealed record GovernanceOwnerCycle(long Ordinal, string OwnerId, string ObligationDigest, string EffectiveReceiptId);
