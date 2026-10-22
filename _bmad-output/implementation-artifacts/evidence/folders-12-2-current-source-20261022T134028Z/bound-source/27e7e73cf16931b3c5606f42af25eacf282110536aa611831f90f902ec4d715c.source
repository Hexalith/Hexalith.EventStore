namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Only the specified post-dispatch cancellation reasons.</summary>
public enum DeletionConsumptionBlockReason
{
    /// <summary>Authenticated accepted admission invalidated integrity; never reopens.</summary>
    AdmissionIntegrity,
    /// <summary>Authenticated per-tenant deletion signing key compromise.</summary>
    CapabilityKeyCompromise,
}
