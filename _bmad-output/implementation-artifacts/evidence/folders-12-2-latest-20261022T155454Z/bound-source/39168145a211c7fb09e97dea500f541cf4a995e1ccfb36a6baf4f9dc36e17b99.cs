namespace Hexalith.EventStore.Admin.Abstractions.Models.Storage;

/// <summary>
/// Snapshot policy configuration for an aggregate type.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="AggregateType">The aggregate type name.</param>
/// <param name="IntervalEvents">The number of events between automatic snapshots.</param>
/// <param name="CreatedAtUtc">When the policy was created.</param>
public record SnapshotPolicy(string TenantId, string Domain, string AggregateType, int IntervalEvents, DateTimeOffset CreatedAtUtc) {
    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; } = !string.IsNullOrWhiteSpace(TenantId)
        ? TenantId
        : throw new ArgumentException("TenantId cannot be null, empty, or whitespace.", nameof(TenantId));

    /// <summary>Gets the domain name.</summary>
    public string Domain { get; } = !string.IsNullOrWhiteSpace(Domain)
        ? Domain
        : throw new ArgumentException("Domain cannot be null, empty, or whitespace.", nameof(Domain));

    /// <summary>Gets the aggregate type name.</summary>
    public string AggregateType { get; } = !string.IsNullOrWhiteSpace(AggregateType)
        ? AggregateType
        : throw new ArgumentException("AggregateType cannot be null, empty, or whitespace.", nameof(AggregateType));
}
