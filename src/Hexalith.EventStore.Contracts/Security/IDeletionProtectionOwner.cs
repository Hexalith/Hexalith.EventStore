namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Private transport-neutral existing atomic protection owner seam; signature-only callers have no destruction authority.</summary>
public interface IDeletionProtectionOwner
{
    /// <summary>Registers exact online guard-issued/dispatch-authorized batch without reservation.</summary>
    Task<DeletionConsumptionOutcome> RegisterAsync(DeletionBatchConsumptionRequest request, CancellationToken cancellationToken = default);
    /// <summary>Serializes irreversible original all-target reservation with hold/integrity/key blocks.</summary>
    Task<DeletionConsumptionOutcome> ReserveAndConsumeAsync(DeletionBatchConsumptionRequest request, CancellationToken cancellationToken = default);
    /// <summary>Activates exact same-batch sole replacement and successor dispatch under expected block-set compare.</summary>
    Task<DeletionConsumptionOutcome> ActivateAsync(DeletionReattestationActivation activation, CancellationToken cancellationToken = default);
    /// <summary>Reads a fresh authenticated exact blocked-batch/current global compare without activating or granting any consumption authority.</summary>
    Task<DeletionActivationComparison?> ReadActivationComparisonAsync(string tenantId, string batchId, string replacementKeyVersion, CancellationToken cancellationToken = default)
        => Task.FromResult<DeletionActivationComparison?>(null);
    /// <summary>Settles only the exact independently proved issued-and-revoked successor, without dispatch or consumption; omitted implementations remain unavailable.</summary>
    Task<DeletionConsumptionOutcome> ReconcileBlockedReplacementAsync(DeletionBlockedReplacementReconciliation request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(new DeletionConsumptionOutcome(request.Capability.TenantId, request.Capability.BatchId, DeletionConsumptionStatus.Unavailable, 0, 0, null, null, null, null, []));
    }
    /// <summary>Reads only the exact retained original issued-but-blocked phase and outcome, never advancing an ordinal.</summary>
    Task<DeletionBlockedReplacementResult?> ReadBlockedReplacementAsync(DeletionBatchCapabilityV1 capability, CancellationToken cancellationToken = default)
        => Task.FromResult<DeletionBlockedReplacementResult?>(null);
    /// <summary>Reads only the exact retained reservation request and reserved/consumed outcome without recovery or mutation; omitted implementations remain unavailable.</summary>
    /// <remarks>
    /// Null means a selection, permission, current-state check or actor entry deadline could not establish an original, or the optional port is omitted.
    /// Invalid tenant/selection arguments, malformed retained state or failed owned capture, and state/authority/transport faults may throw instead;
    /// these exceptions disclose no usable original and grant no effect. The private adapter propagates caller cancellation and its deadline as
    /// <see cref="OperationCanceledException"/> and throws when bounded transport admission is saturated. Omitted implementations return null even for a cancelled token.
    /// </remarks>
    Task<DeletionReservedManifestOriginal?> ReadReservedManifestOriginalAsync(string tenantId, string batchId, string reservationReceiptId, CancellationToken cancellationToken = default)
        => Task.FromResult<DeletionReservedManifestOriginal?>(null);
    /// <summary>Reads/reconciles only exact original durable result; absence or unavailable state grants no effect.</summary>
    Task<DeletionConsumptionOutcome> LookupAsync(string tenantId, string batchId, CancellationToken cancellationToken = default);
}
