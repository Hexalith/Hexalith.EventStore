using Dapr.Actors;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Private same-tenant linearizable post-dispatch protection owner; never expose to public or Workflow principals.</summary>
public interface IDeletionConsumptionActor : IActor
{
    /// <summary>Registers only the independently verified exact active signed batch/dispatch.</summary>
    Task<DeletionConsumptionOutcome> RegisterAsync(DeletionBatchConsumptionRequest request);
    /// <summary>Atomically orders the irreversible original reservation against authenticated cancellation.</summary>
    Task<DeletionConsumptionOutcome> ReserveAndConsumeAsync(DeletionBatchConsumptionRequest request);
    /// <summary>Registers an authenticated admission-integrity block; reserved/consumed effects cannot be cancelled.</summary>
    Task<DeletionConsumptionOutcome> BlockAsync(DeletionBatchBlockRequest request);
    /// <summary>Authenticated private registrar installs the exact global tenant/key block before guard mirroring.</summary>
    Task<DeletionCapabilityRevocationReceipt?> RegisterRevocationAsync(DeletionCapabilityRevocationEnvelope envelope);
    /// <summary>Atomically activates exact same-batch re-attestation or records its replacement-key compromise.</summary>
    Task<DeletionConsumptionOutcome> ActivateAsync(DeletionReattestationActivation activation);
    /// <summary>Reconciles an independently authenticated exact issued-and-blocked successor without any dispatch or reservation.</summary>
    Task<DeletionConsumptionOutcome> ReconcileBlockedReplacementAsync(DeletionBlockedReplacementReconciliation request);
    /// <summary>Reads the exact retained original no-effect reconciliation and outcome.</summary>
    Task<DeletionBlockedReplacementResult?> ReadBlockedReplacementAsync(DeletionBatchCapabilityV1 capability);
    /// <summary>Reads current independently anchored same-batch/global-key comparison without any effect.</summary>
    Task<DeletionActivationComparison?> ReadActivationComparisonAsync(string tenantId, string batchId, string replacementKeyVersion);
    /// <summary>Reads exact durable batch outcome; reserved recovery only completes the original reservation.</summary>
    Task<DeletionConsumptionOutcome> LookupAsync(string tenantId, string batchId);
    /// <summary>Reads exact original authenticated revocation result; changed evidence conflicts.</summary>
    Task<DeletionCapabilityRevocationReceipt?> LookupRevocationAsync(DeletionCapabilityRevocationEnvelope envelope);
}
