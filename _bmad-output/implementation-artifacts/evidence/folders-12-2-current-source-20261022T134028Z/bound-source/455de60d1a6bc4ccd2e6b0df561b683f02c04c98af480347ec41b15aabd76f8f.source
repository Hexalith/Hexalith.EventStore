namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Exact authenticated admission cancellation identity; key compromise uses the separate authenticated registrar.</summary>
/// <param name="TenantId">Exact tenant.</param>
/// <param name="BatchId">Exact batch.</param>
/// <param name="OperationId">Deterministic complete evidence operation.</param>
/// <param name="AdmissionEvidenceId">Independently recorded accepted admission evidence.</param>
public sealed record DeletionBatchBlockRequest(string TenantId, string BatchId, string OperationId, string AdmissionEvidenceId);
