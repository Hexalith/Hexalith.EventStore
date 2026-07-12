namespace Hexalith.EventStore.Server.Projections;

/// <summary>
/// A canonical, platform-derived address for an aggregate-owned projection read-model value. Instances can
/// only be minted by <see cref="IProjectionReadModelAddressFactory"/> (the constructor is internal), so a
/// REST caller can never forge a raw store/key/ETag target: ownership is proven by construction, not by
/// string-prefix guessing. The same address is used for writes and for coordinated erasure.
/// </summary>
public sealed record ProjectionReadModelAddress {
    internal ProjectionReadModelAddress(
        string storeName,
        string key,
        string tenantId,
        string domain,
        string projectionName,
        string aggregateId,
        string slot) {
        StoreName = storeName;
        Key = key;
        TenantId = tenantId;
        Domain = domain;
        ProjectionName = projectionName;
        AggregateId = aggregateId;
        Slot = slot;
    }

    /// <summary>Gets the DAPR state-store component name resolved from projection options.</summary>
    public string StoreName { get; }

    /// <summary>Gets the canonical, reserved-char-free read-model state key.</summary>
    public string Key { get; }

    /// <summary>Gets the owning tenant identifier.</summary>
    public string TenantId { get; }

    /// <summary>Gets the owning domain.</summary>
    public string Domain { get; }

    /// <summary>Gets the projection name.</summary>
    public string ProjectionName { get; }

    /// <summary>Gets the owning aggregate identifier.</summary>
    public string AggregateId { get; }

    /// <summary>Gets the aggregate-owned logical slot name.</summary>
    public string Slot { get; }
}
