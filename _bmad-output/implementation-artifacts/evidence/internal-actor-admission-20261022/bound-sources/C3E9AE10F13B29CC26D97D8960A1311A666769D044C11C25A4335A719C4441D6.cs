using System.ComponentModel.DataAnnotations;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

/// <summary>
/// Request to change a user's role within a tenant.
/// </summary>
/// <param name="UserId">The user identifier.</param>
/// <param name="NewRole">The new role to assign.</param>
public record ChangeTenantUserRoleRequest(
    [Required][MinLength(1)] string UserId,
    [Required] string NewRole);
