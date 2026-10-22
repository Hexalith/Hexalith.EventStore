namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Exact same-batch conditional replacement; no renewal, new target or global key-block clearing.</summary>
/// <param name="OperationId">Complete deterministic activation identity.</param>
/// <param name="CompromiseBlockReceiptId">Exact previous capability-compromise block.</param>
/// <param name="ExpectedKeyBlockSetRevision">Atomic expected tenant key block-set revision.</param>
/// <param name="GuardReplacementReceiptId">Exact sole-active replacement commit.</param>
/// <param name="Replacement">Same immutable batch/manifest with next attestation and successor dispatch.</param>
public sealed record DeletionReattestationActivation(string OperationId, string CompromiseBlockReceiptId, long ExpectedKeyBlockSetRevision, string GuardReplacementReceiptId, DeletionBatchConsumptionRequest Replacement);
