namespace Hexalith.EventStore.Contracts.Identity;

using System.Text.RegularExpressions;

/// <summary>
/// Canonical identity tuple for an aggregate, providing all key derivation properties
/// used by DAPR actors, state store, pub/sub, and queue sessions.
/// </summary>
/// <param name="TenantId">Tenant identifier (lowercase alphanumeric + hyphens, max 64 chars).</param>
/// <param name="Domain">Domain name (lowercase alphanumeric + hyphens, max 64 chars).</param>
/// <param name="AggregateId">Aggregate identifier (alphanumeric + dots/hyphens/underscores, max 256 chars, case-sensitive).</param>
public record AggregateIdentity
{
    private static readonly Regex _tenantDomainRegex = new(@"^[a-z0-9]([a-z0-9-]*[a-z0-9])?$", RegexOptions.Compiled);
    private static readonly Regex _aggregateIdRegex = new(@"^[a-zA-Z0-9]([a-zA-Z0-9._-]*[a-zA-Z0-9])?$", RegexOptions.Compiled);

    /// <summary>
    /// Initializes a new instance of the <see cref="AggregateIdentity"/> record.
    /// TenantId and Domain are forced to lowercase. All components are validated against
    /// security-critical regex patterns and length constraints.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="domain">Domain name.</param>
    /// <param name="aggregateId">Aggregate identifier.</param>
    /// <exception cref="ArgumentNullException">Thrown when any component is null.</exception>
    /// <exception cref="ArgumentException">Thrown when any component is empty, whitespace, exceeds length, or fails regex validation.</exception>
    public AggregateIdentity(string tenantId, string domain, string aggregateId)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(aggregateId);

        tenantId = tenantId.ToLowerInvariant();
        domain = domain.ToLowerInvariant();

        ValidateTenantOrDomain(tenantId, nameof(tenantId));
        ValidateTenantOrDomain(domain, nameof(domain));
        ValidateAggregateId(aggregateId);

        TenantId = tenantId;
        Domain = domain;
        AggregateId = aggregateId;
    }

    /// <summary>Gets the tenant identifier (always lowercase).</summary>
    public string TenantId { get; }

    /// <summary>Gets the domain name (always lowercase).</summary>
    public string Domain { get; }

    /// <summary>Gets the aggregate identifier (case-sensitive).</summary>
    public string AggregateId { get; }

    /// <summary>
    /// Gets the DAPR actor ID in canonical colon-separated form.
    /// Isolation guarantee: each actor instance's state is scoped by DAPR to this ID,
    /// preventing cross-tenant state access. Colons are forbidden in components to ensure
    /// structural disjointness between tenants (FR15, FR28).
    /// </summary>
    public string ActorId => $"{TenantId}:{Domain}:{AggregateId}";

    /// <summary>
    /// Gets the event stream key prefix for state store lookups (append sequence number for full key).
    /// Pattern: {tenant}:{domain}:{aggId}:events: — isolation is guaranteed because colons are
    /// forbidden in tenant/domain/aggregateId components, making each tenant's key space structurally
    /// disjoint from all other tenants (D1, FR15, FR28).
    /// </summary>
    public string EventStreamKeyPrefix => $"{TenantId}:{Domain}:{AggregateId}:events:";

    /// <summary>
    /// Gets the metadata key for state store lookups.
    /// Pattern: {tenant}:{domain}:{aggId}:metadata — tenant-scoped by the same 4-layer isolation model:
    /// (1) input validation rejects colons, (2) composite key prefixing, (3) DAPR actor scoping,
    /// (4) JWT tenant enforcement (FR15, FR28).
    /// </summary>
    public string MetadataKey => $"{TenantId}:{Domain}:{AggregateId}:metadata";

    /// <summary>
    /// Gets the snapshot key for state store lookups.
    /// Pattern: {tenant}:{domain}:{aggId}:snapshot — tenant-scoped by the same 4-layer isolation model:
    /// (1) input validation rejects colons, (2) composite key prefixing, (3) DAPR actor scoping,
    /// (4) JWT tenant enforcement (FR15, FR28).
    /// </summary>
    public string SnapshotKey => $"{TenantId}:{Domain}:{AggregateId}:snapshot";

    /// <summary>Gets the pub/sub topic in dot-separated form.</summary>
    public string PubSubTopic => $"{TenantId}.{Domain}.events";

    /// <summary>Gets the queue session identifier (same as ActorId).</summary>
    public string QueueSession => $"{TenantId}:{Domain}:{AggregateId}";

    /// <summary>Returns the canonical colon-separated form of the identity.</summary>
    /// <returns>A string in the format "tenantId:domain:aggregateId".</returns>
    public override string ToString() => ActorId;

    private static void ValidateTenantOrDomain(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} cannot be empty or whitespace.", parameterName);
        }

        if (value.Length > 64)
        {
            throw new ArgumentException($"{parameterName} cannot exceed 64 characters. Got {value.Length}.", parameterName);
        }

        if (ContainsInvalidCharacters(value))
        {
            throw new ArgumentException($"{parameterName} contains control characters (< 0x20) or non-ASCII characters (> 0x7F).", parameterName);
        }

        if (!_tenantDomainRegex.IsMatch(value))
        {
            throw new ArgumentException($"{parameterName} must match pattern ^[a-z0-9]([a-z0-9-]*[a-z0-9])?$ (lowercase alphanumeric + hyphens, no leading/trailing hyphen). Got '{value}'.", parameterName);
        }
    }

    private static void ValidateAggregateId(string value)
    {
        const string parameterName = "aggregateId";

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("aggregateId cannot be empty or whitespace.", parameterName);
        }

        if (value.Length > 256)
        {
            throw new ArgumentException($"aggregateId cannot exceed 256 characters. Got {value.Length}.", parameterName);
        }

        if (ContainsInvalidCharacters(value))
        {
            throw new ArgumentException("aggregateId contains control characters (< 0x20) or non-ASCII characters (> 0x7F).", parameterName);
        }

        if (!_aggregateIdRegex.IsMatch(value))
        {
            throw new ArgumentException($"aggregateId must match pattern ^[a-zA-Z0-9]([a-zA-Z0-9._-]*[a-zA-Z0-9])?$ (alphanumeric + dots/hyphens/underscores). Got '{value}'.", parameterName);
        }
    }

    private static bool ContainsInvalidCharacters(string value)
    {
        foreach (char c in value)
        {
            if (c < 0x20 || c >= 0x7F)
            {
                return true;
            }
        }

        return false;
    }
}
