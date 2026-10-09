namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Exact ordinal transition evidence. Invalidated artifacts and original acceptance attribution are explicit and content free.</summary>
/// <param name="Ordinal">Exact Ordinal.</param>
/// <param name="GlobalCutId">Exact GlobalCutId.</param>
/// <param name="TokenId">Exact TokenId.</param>
/// <param name="OwnerId">Exact OwnerId.</param>
/// <param name="ObligationDigest">Exact ObligationDigest.</param>
/// <param name="ViolationId">Exact ViolationId.</param>
/// <param name="ViolationKind">Exact ViolationKind.</param>
/// <param name="AcceptedAtOrdinal">Exact AcceptedAtOrdinal.</param>
/// <param name="AcceptedAtGuardHighWater">Exact AcceptedAtGuardHighWater.</param>
/// <param name="ResourceId">Exact ResourceId.</param>
/// <param name="InvalidatedArtifactIds">Exact InvalidatedArtifactIds.</param>
/// <param name="NoCutReceiptId">Exact NoCutReceiptId.</param>
public sealed record GovernanceOrdinalCommand(long Ordinal, string GlobalCutId, string TokenId, string OwnerId, string ObligationDigest, string ViolationId, string ViolationKind, long AcceptedAtOrdinal, long AcceptedAtGuardHighWater, string ResourceId, IReadOnlyList<string> InvalidatedArtifactIds, string NoCutReceiptId);
