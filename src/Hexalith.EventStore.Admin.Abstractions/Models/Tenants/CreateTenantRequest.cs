using System.ComponentModel.DataAnnotations;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

/// <summary>
/// Request to create a new tenant.
/// </summary>
/// <param name="TenantId">The tenant identifier (URL-safe, lowercase).</param>
/// <param name="Name">The tenant name.</param>
/// <param name="Description">Optional tenant description.</param>
public record CreateTenantRequest(
    [Required] [RegularExpression(@"^[a-z0-9]+(-[a-z0-9]+)*$")] string TenantId,
    [Required] string Name,
    string? Description);
