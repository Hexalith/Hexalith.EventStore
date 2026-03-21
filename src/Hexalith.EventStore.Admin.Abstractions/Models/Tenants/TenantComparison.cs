namespace Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

/// <summary>
/// Comparison of usage across multiple tenants.
/// </summary>
/// <param name="Tenants">The tenant summaries being compared.</param>
/// <param name="ComparedAtUtc">When the comparison was generated.</param>
public record TenantComparison(IReadOnlyList<TenantSummary> Tenants, DateTimeOffset ComparedAtUtc)
{
    /// <summary>Gets the tenant summaries being compared.</summary>
    public IReadOnlyList<TenantSummary> Tenants { get; } = Tenants ?? throw new ArgumentNullException(nameof(Tenants));
}
