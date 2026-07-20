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

    /// <summary>Durably prepares a copied target record that remains non-executable.</summary>
    Task PreparePromotionAsync(IdempotencyAdmissionPromotionImportRequest request);

    /// <summary>Durably redirects a source after its target acknowledged the imported record.</summary>
    Task SetRedirectAsync(IdempotencyAdmissionRedirectRequest request);

    /// <summary>Activates a prepared target only after the tenant directory pointer flipped.</summary>
    Task ActivatePromotionAsync(IdempotencyAdmissionPromotionActivationRequest request);

    /// <summary>Removes only an exact metadata tombstone after governed tenant purge eligibility.</summary>
    Task<bool> PurgeTombstoneAsync(IdempotencyAdmissionPurgeRequest request);
}
