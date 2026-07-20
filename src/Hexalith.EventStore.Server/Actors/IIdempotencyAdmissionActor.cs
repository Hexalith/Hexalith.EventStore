using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Serializes durable admission for one tenant/key actor partition.</summary>
public interface IIdempotencyAdmissionActor : IActor
{
    /// <summary>Reserves or classifies a tenant/key request.</summary>
    Task<IdempotencyAdmissionResult> AdmitAsync(IdempotencyAdmissionRequest request);

    /// <summary>Marks that the fenced writer is crossing the side-effect boundary.</summary>
    Task BeginAsync(IdempotencyAdmissionTransitionRequest request);

    /// <summary>Finalizes a deterministic result for replay.</summary>
    Task CompleteAsync(IdempotencyAdmissionCompletionRequest request);

    /// <summary>Persists recoverable or unknown-outcome state under the active fence.</summary>
    Task MarkRecoveryAsync(IdempotencyAdmissionRecoveryRequest request);
}
