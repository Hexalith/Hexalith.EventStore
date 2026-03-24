using System.ComponentModel.DataAnnotations;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

/// <summary>
/// Request to change a user's role within a tenant.
/// </summary>
/// <param name="Email">The user's email address.</param>
/// <param name="NewRole">The new role to assign.</param>
public record ChangeTenantUserRoleRequest(
    [Required] [EmailAddress] string Email,
    [Required] string NewRole);
