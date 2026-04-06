namespace Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

/// <summary>
/// Detailed tenant information.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="Name">The tenant name.</param>
/// <param name="Description">Optional tenant description.</param>
/// <param name="Status">The current tenant status.</param>
/// <param name="CreatedAt">When the tenant was created.</param>
public record TenantDetail(
    string TenantId,
    string Name,
    string? Description,
    TenantStatusType Status,
    DateTimeOffset CreatedAt)
{
    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; } = !string.IsNullOrWhiteSpace(TenantId)
        ? TenantId
        : throw new ArgumentException("TenantId cannot be null, empty, or whitespace.", nameof(TenantId));
}
