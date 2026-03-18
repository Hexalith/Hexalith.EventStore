namespace Hexalith.EventStore.Server.Configuration;

/// <summary>
/// Configuration options for aggregate state snapshot creation.
/// Snapshot intervals are mandatory (enforcement rule #15) and configurable per domain.
/// </summary>
/// <remarks>
/// <para><b>Minimum interval enforcement:</b> All intervals must be >= 10 to prevent accidental
/// performance degradation from overly frequent snapshots. Configuration validation rejects
/// values below this threshold.</para>
/// <para><b>DEPLOYMENT SECURITY:</b> Snapshot intervals can be loaded from DAPR config store
/// for dynamic updates (NFR20). Write access to the config store must be restricted to admin
/// service accounts only (Red Team H2).</para>
/// </remarks>
public record SnapshotOptions {
    /// <summary>
    /// Minimum allowed snapshot interval. Values below this threshold are rejected
    /// to prevent performance degradation from overly frequent snapshots.
    /// </summary>
    public const int MinimumInterval = 10;

    /// <summary>
    /// Gets the default snapshot interval (number of events between snapshots).
    /// Applies to all domains unless overridden in <see cref="DomainIntervals"/>.
    /// Default: 100 events (enforcement rule #15).
    /// </summary>
    public int DefaultInterval { get; init; } = 100;

    /// <summary>
    /// Gets per-domain snapshot interval overrides.
    /// Key: domain name (lowercase). Value: snapshot interval for that domain.
    /// Domains not present in this dictionary use <see cref="DefaultInterval"/>.
    /// </summary>
    public Dictionary<string, int> DomainIntervals { get; init; } = [];

    /// <summary>
    /// Gets per-tenant-domain snapshot interval overrides.
    /// Key: "tenantId:domain" (lowercase, colon-separated). Value: snapshot interval for that tenant-domain pair.
    /// Resolution order: <see cref="TenantDomainIntervals"/> > <see cref="DomainIntervals"/> > <see cref="DefaultInterval"/>.
    /// </summary>
    public Dictionary<string, int> TenantDomainIntervals { get; init; } = [];

    /// <summary>
    /// Validates that all configured intervals meet the minimum threshold.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when any interval is below <see cref="MinimumInterval"/>.</exception>
    public void Validate() {
        if (DefaultInterval < MinimumInterval) {
            throw new InvalidOperationException(
                $"Snapshot DefaultInterval must be >= {MinimumInterval}. Got {DefaultInterval}.");
        }

        foreach (KeyValuePair<string, int> entry in DomainIntervals) {
            if (entry.Value < MinimumInterval) {
                throw new InvalidOperationException(
                    $"Snapshot interval for domain '{entry.Key}' must be >= {MinimumInterval}. Got {entry.Value}.");
            }
        }

        foreach (KeyValuePair<string, int> entry in TenantDomainIntervals) {
            if (entry.Value < MinimumInterval) {
                throw new InvalidOperationException(
                    $"Snapshot interval for tenant-domain '{entry.Key}' must be >= {MinimumInterval}. Got {entry.Value}.");
            }
        }
    }
}
