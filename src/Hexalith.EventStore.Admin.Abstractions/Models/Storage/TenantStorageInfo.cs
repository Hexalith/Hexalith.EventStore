namespace Hexalith.EventStore.Admin.Abstractions.Models.Storage;

/// <summary>
/// Storage usage information for a single tenant.
/// </summary>
/// <param name="TenantId">The tenant identifier.</param>
/// <param name="EventCount">The total number of events for this tenant.</param>
/// <param name="SizeBytes">The storage size in bytes, or null if the state store backend does not support size queries (NFR44).</param>
/// <param name="GrowthRatePerDay">The daily event growth rate, or null if historical tracking is not available.</param>
public record TenantStorageInfo(string TenantId, long EventCount, long? SizeBytes, double? GrowthRatePerDay)
{
    /// <summary>Gets the tenant identifier.</summary>
    public string TenantId { get; } = !string.IsNullOrWhiteSpace(TenantId)
        ? TenantId
        : throw new ArgumentException("TenantId cannot be null, empty, or whitespace.", nameof(TenantId));

    /// <summary>Gets the daily event growth rate.</summary>
    public double? GrowthRatePerDay { get; } = GrowthRatePerDay is double v && (double.IsNaN(v) || double.IsInfinity(v))
        ? throw new ArgumentOutOfRangeException(nameof(GrowthRatePerDay), GrowthRatePerDay, "Value must be a finite number.")
        : GrowthRatePerDay;
}
