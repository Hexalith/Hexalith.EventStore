using System.ComponentModel.DataAnnotations;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

/// <summary>
/// Request to add a user to a tenant.
/// </summary>
/// <param name="Email">The user's email address.</param>
/// <param name="Role">The user's role within this tenant.</param>
public record AddTenantUserRequest(
    [Required] [EmailAddress] string Email,
    [Required] string Role);
