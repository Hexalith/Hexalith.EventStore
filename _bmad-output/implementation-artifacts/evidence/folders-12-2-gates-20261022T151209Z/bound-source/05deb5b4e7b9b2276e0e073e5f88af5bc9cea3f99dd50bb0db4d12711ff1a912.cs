using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>One retained journal record. The transition bytes are the original, not a replacement identity.</summary>
/// <param name="Transition">Retained original transition.</param>
/// <param name="Disposition">Current disposition.</param>
/// <param name="AnchorRevision">Anchor revision. Admit leaves it at the predecessor.</param>
public sealed record ProtectionAnchorRecord(AnchoredStateTransition Transition, ProtectionAnchorDisposition Disposition, long AnchorRevision);
