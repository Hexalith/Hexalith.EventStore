using System.ComponentModel.DataAnnotations;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

/// <summary>
/// Request to add a user to a tenant.
/// </summary>
/// <param name="UserId">The user identifier.</param>
/// <param name="Role">The user's role within this tenant.</param>
public record AddTenantUserRequest(
    [Required][MinLength(1)] string UserId,
    [Required] string Role);
