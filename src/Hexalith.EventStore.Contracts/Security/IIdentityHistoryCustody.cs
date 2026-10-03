using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>Provider seam for independently governed attribution protection, destruction and restore safety.</summary>
public interface IIdentityHistoryCustody
{
    /// <summary>Accepts writes only when the provider enforces the full finite purpose lifecycle.</summary>
    Task<IdentityHistoryCustodyEvidence?> AdmitAsync(AggregateIdentity identity, IdentityHistoryPolicy policy,
        DateTimeOffset effectiveAt, CancellationToken cancellationToken = default);

    /// <summary>Checks current non-rollback lifecycle authority before retained evidence is read.</summary>
    Task<bool> CanReadAsync(AggregateIdentity identity, IdentityHistoryCustodyEvidence evidence,
        CancellationToken cancellationToken = default);

    /// <summary>Irreversibly destroys expired source and derived evidence and records restore-safe evidence.</summary>
    Task<bool> DestroyExpiredAsync(AggregateIdentity identity, IdentityHistoryCustodyEvidence evidence,
        CancellationToken cancellationToken = default);

    /// <summary>Protects attribution using purpose keys independent of profile erasure.</summary>
    Task<PayloadProtectionResult> ProtectEventAsync(AggregateIdentity identity, string eventType,
        byte[] payload, string format, IdentityHistoryCustodyEvidence evidence, CancellationToken cancellationToken = default);

    /// <summary>Unprotects retained attribution only under current non-rollback lifecycle authority.</summary>
    Task<PayloadProtectionResult> UnprotectEventAsync(AggregateIdentity identity, string eventType,
        byte[] payload, string format, CancellationToken cancellationToken = default);

    /// <summary>Protects history fields in profile-protected snapshots with independent purpose keys.</summary>
    Task<object> ProtectSnapshotAsync(AggregateIdentity identity, object snapshot, CancellationToken cancellationToken = default);

    /// <summary>Checks restore-safe lifecycle and unprotects retained snapshot history.</summary>
    Task<object?> UnprotectSnapshotAsync(AggregateIdentity identity, object snapshot, CancellationToken cancellationToken = default);
}
