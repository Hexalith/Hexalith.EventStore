using System.Runtime.Serialization;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Exact signed active attestation plus independently committed guard issue/dispatch evidence and sorted immutable targets.</summary>
/// <param name="Capability">Closed signed payload.</param>
/// <param name="DetachedJws">Exact detached signature artifact.</param>
/// <param name="CommittedIssuedGuardRevision">Successful issue revision recorded separately from signed bytes.</param>
/// <param name="DispatchReceiptId">Exact conditional committed guard dispatch receipt.</param>
/// <param name="DispatchGuardRevision">Successful dispatch revision.</param>
/// <param name="Targets">Exact complete sorted manifest.</param>
[DataContract]
public sealed record DeletionBatchConsumptionRequest([property: DataMember] DeletionBatchCapabilityV1 Capability, [property: DataMember] string DetachedJws,
    [property: DataMember] long CommittedIssuedGuardRevision, [property: DataMember] string DispatchReceiptId, [property: DataMember] long DispatchGuardRevision,
    [property: DataMember] IReadOnlyList<ProtectionTarget> Targets);
