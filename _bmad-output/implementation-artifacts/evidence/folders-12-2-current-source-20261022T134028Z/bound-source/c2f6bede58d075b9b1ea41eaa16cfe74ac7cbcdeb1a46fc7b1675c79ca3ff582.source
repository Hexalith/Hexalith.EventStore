namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Exact signed active attestation plus independently committed guard issue/dispatch evidence and sorted immutable targets.</summary>
/// <param name="Capability">Closed signed payload.</param>
/// <param name="DetachedJws">Exact detached signature artifact.</param>
/// <param name="CommittedIssuedGuardRevision">Successful issue revision recorded separately from signed bytes.</param>
/// <param name="DispatchReceiptId">Exact conditional committed guard dispatch receipt.</param>
/// <param name="DispatchGuardRevision">Successful dispatch revision.</param>
/// <param name="Targets">Exact complete sorted manifest.</param>
public sealed record DeletionBatchConsumptionRequest(DeletionBatchCapabilityV1 Capability, string DetachedJws, long CommittedIssuedGuardRevision, string DispatchReceiptId, long DispatchGuardRevision, IReadOnlyList<ProtectionTarget> Targets);
