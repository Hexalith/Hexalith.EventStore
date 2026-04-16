namespace Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

/// <summary>
/// Summary information about a tenant.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Name">The tenant name.</param>
/// <param name="Status">The current tenant status.</param>
public record TenantSummary(string TenantId, string Name, TenantStatusType Status) {
    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; } = !string.IsNullOrWhiteSpace(TenantId)
        ? TenantId
        : throw new ArgumentException("TenantId cannot be null, empty, or whitespace.", nameof(TenantId));

    /// <summary>Gets the tenant name.</summary>
    public string Name { get; } = !string.IsNullOrWhiteSpace(Name)
        ? Name
        : throw new ArgumentException("Name cannot be null, empty, or whitespace.", nameof(Name));
}
