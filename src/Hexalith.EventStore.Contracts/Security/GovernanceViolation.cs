namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Append-only violation with exact original acceptance evidence and exactly one successor ordinal; prior obligations survive all recuts.</summary>
/// <param name="ViolationId">Exact ViolationId.</param>
/// <param name="Ordinal">Exact Ordinal.</param>
/// <param name="SuccessorOrdinal">Exact SuccessorOrdinal.</param>
/// <param name="Kind">Exact Kind.</param>
/// <param name="AcceptedAtOrdinal">Exact AcceptedAtOrdinal.</param>
/// <param name="AcceptedAtGuardHighWater">Exact AcceptedAtGuardHighWater.</param>
/// <param name="ResourceId">Exact ResourceId.</param>
/// <param name="InvalidatedArtifactIds">Exact InvalidatedArtifactIds.</param>
/// <param name="NoCutReceiptId">Exact NoCutReceiptId.</param>
/// <param name="ReceiptId">Exact ReceiptId.</param>
public sealed record GovernanceViolation(string ViolationId, long Ordinal, long SuccessorOrdinal, string Kind, long AcceptedAtOrdinal, long AcceptedAtGuardHighWater, string ResourceId, IReadOnlyList<string> InvalidatedArtifactIds, string NoCutReceiptId, string ReceiptId)
{
    /// <summary>Exact independently authenticated post-seal content target, preserved for singleton coverage correlation.</summary>
    public GovernanceProtectionTarget? ProtectionTarget { get; init; }
}
