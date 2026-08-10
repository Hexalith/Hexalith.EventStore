using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Serializes durable admission for one tenant/key actor partition.</summary>
public interface IIdempotencyAdmissionActor : IActor
{
    /// <summary>Inspects protected state without reserving or otherwise mutating it.</summary>
    Task<IdempotencyAdmissionInspection> InspectAsync();

    /// <summary>Reserves or classifies a tenant/key request.</summary>
    Task<IdempotencyAdmissionResult> AdmitAsync(IdempotencyAdmissionRequest request);

    /// <summary>Marks that the fenced writer is crossing the side-effect boundary.</summary>
    Task BeginAsync(IdempotencyAdmissionTransitionRequest request);

    /// <summary>Finalizes a deterministic result for replay.</summary>
    Task CompleteAsync(IdempotencyAdmissionCompletionRequest request);

    /// <summary>Persists recoverable or unknown-outcome state under the active fence.</summary>
    Task MarkRecoveryAsync(IdempotencyAdmissionRecoveryRequest request);

    /// <summary>Fails closed unless durable state still authorizes the exact protected operation.</summary>
    Task ValidateAuthorityAsync(IdempotencyAdmissionAuthorityRequest request);

    /// <summary>Durably prepares a copied target record that remains non-executable.</summary>
    Task PreparePromotionAsync(IdempotencyAdmissionPromotionImportRequest request);

    /// <summary>Returns a hash-bound acknowledgement of the exact non-executable imported target.</summary>
    Task<IdempotencyAdmissionPromotionAcknowledgement> AcknowledgePromotionAsync(
        IdempotencyAdmissionPromotionAcknowledgementRequest request);

    /// <summary>Removes only an exact unactivated prepared target before its source redirects.</summary>
    Task RollbackPromotionAsync(IdempotencyAdmissionPromotionRollbackRequest request);

    /// <summary>Durably redirects a source after its target acknowledged the imported record.</summary>
    Task SetRedirectAsync(IdempotencyAdmissionRedirectRequest request);

    /// <summary>Activates a prepared target only after the tenant directory pointer flipped.</summary>
    Task ActivatePromotionAsync(IdempotencyAdmissionPromotionActivationRequest request);

    /// <summary>Removes only an exact metadata tombstone after governed tenant purge eligibility.</summary>
    Task<bool> PurgeTombstoneAsync(IdempotencyAdmissionPurgeRequest request);
}
