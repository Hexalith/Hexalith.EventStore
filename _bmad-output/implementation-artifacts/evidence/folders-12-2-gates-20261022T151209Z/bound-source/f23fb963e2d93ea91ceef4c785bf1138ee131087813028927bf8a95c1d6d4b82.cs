namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Durable owner states and explicit non-authoritative operation results.</summary>
public enum DeletionConsumptionStatus
{
    /// <summary>Registered exact attestation; no irreversible reservation yet.</summary>
    Unconsumed,
    /// <summary>Irreversibly reserved original batch; only its exact recovery can proceed.</summary>
    ConsumptionReserved,
    /// <summary>Authenticated cancellation won before reserve.</summary>
    ConsumptionBlocked,
    /// <summary>Complete ordered irreversible target vector is durable.</summary>
    Consumed,
    /// <summary>Exact target is already covered by its original irreversible receipt.</summary>
    AlreadyDestroyedByBatch,
    /// <summary>Compare or immutable identity conflict; no new effect.</summary>
    Conflict,
    /// <summary>Replacement key compromise keeps the exact batch blocked.</summary>
    ActivationBlockedByReplacementKeyCompromise,
    /// <summary>Missing authority/backend or unknown evidence; no new effect.</summary>
    Unavailable,
}
