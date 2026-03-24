using System.ComponentModel.DataAnnotations;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

/// <summary>
/// Request to remove a user from a tenant.
/// </summary>
/// <param name="Email">The user's email address.</param>
public record RemoveTenantUserRequest([Required] [EmailAddress] string Email);
