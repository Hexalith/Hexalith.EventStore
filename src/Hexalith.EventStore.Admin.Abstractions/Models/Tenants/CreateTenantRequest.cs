using System.ComponentModel.DataAnnotations;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

/// <summary>
/// Request to create a new tenant.
/// </summary>
/// <param name="TenantId">The tenant identifier (URL-safe, lowercase).</param>
/// <param name="DisplayName">The tenant display name.</param>
/// <param name="SubscriptionTier">Subscription tier: Standard, Premium, Enterprise.</param>
/// <param name="MaxEventsPerDay">Maximum events allowed per day.</param>
/// <param name="MaxStorageBytes">Maximum storage in bytes.</param>
public record CreateTenantRequest(
    [Required] [RegularExpression(@"^[a-z0-9]+(-[a-z0-9]+)*$")] string TenantId,
    [Required] string DisplayName,
    [Required] string SubscriptionTier,
    [Range(1, long.MaxValue)] long MaxEventsPerDay,
    [Range(1, long.MaxValue)] long MaxStorageBytes);
