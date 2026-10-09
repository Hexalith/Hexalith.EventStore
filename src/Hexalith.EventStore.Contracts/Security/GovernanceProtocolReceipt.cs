namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Original content-free authenticated protocol outcome. Actual committed high water and maximum matching accepted ordinal are assigned at the joint write linearization; per-request ordinals are in AdmissionAttributionsJson.</summary>
/// <param name="OperationId">Exact OperationId.</param>
/// <param name="IntentDigest">Exact IntentDigest.</param>
/// <param name="Status">Exact Status.</param>
/// <param name="GuardHighWater">Exact GuardHighWater.</param>
/// <param name="AcceptedAtAdmissionFenceOrdinal">Maximum ordinal over matching installed deletion requests; a violation reporter reads its exact request's ordinal from AdmissionAttributionsJson.</param>
/// <param name="ReferenceId">Exact ReferenceId.</param>
/// <param name="ReceiptId">Exact ReceiptId.</param>
public sealed record GovernanceProtocolReceipt(string OperationId, string IntentDigest, string Status, long GuardHighWater, long AcceptedAtAdmissionFenceOrdinal, string ReferenceId, string ReceiptId)
{
    /// <summary>Canonical bounded per-request installed scope ordinals at this write's own linearization; an empty vector can mean only a pre-installation write.</summary>
    public string AdmissionAttributionsJson { get; init; } = "[]";
    /// <summary>Independently authenticated exact original resource, retained at its own joint append linearization; omission cannot prove a violation.</summary>
    public string AcceptedWriteResourceId { get; init; } = "";
    /// <summary>Immutable independently authenticated original write facts, never inferred from another accepted receipt.</summary>
    public GovernanceWriteFacts? AcceptedWriteFacts { get; init; }
    /// <summary>Exact committed source/outbox mutation vector digest at this append's own linearization.</summary>
    public string AcceptedTargetMutationDigest { get; init; } = "";
    /// <summary>Exact original fourteen-field issue artifact correlation, including terminal no-issue results.</summary>
    public DeletionBatchCapabilityV1? Capability { get; init; }
    /// <summary>Original canonical signer identity.</summary>
    public string SigningRequestId { get; init; } = "";
    /// <summary>SHA-256 of the retained detached public signature; no signature secret or content is recorded.</summary>
    public string DetachedJwsDigest { get; init; } = "";
}
