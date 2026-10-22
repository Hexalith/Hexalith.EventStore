namespace Hexalith.EventStore.DomainService;

/// <summary>Stable identity of one tenant/domain shared-projection rebuild session.</summary>
/// <param name="TenantId">The tenant whose shared projection is rebuilt.</param>
/// <param name="Domain">The canonical domain name.</param>
/// <param name="ProjectionType">The exact named shared projection route.</param>
/// <param name="OperationId">The stable operation identity reused by retries.</param>
/// <param name="CatalogFingerprint">The exact admitted route-catalog fingerprint.</param>
public sealed record DomainSharedProjectionRebuildIdentity(
    string TenantId,
    string Domain,
    string ProjectionType,
    string OperationId,
    string CatalogFingerprint);
