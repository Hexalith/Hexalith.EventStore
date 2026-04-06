using System.ComponentModel.DataAnnotations;

namespace Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

/// <summary>
/// Request to remove a user from a tenant.
/// </summary>
/// <param name="UserId">The user identifier.</param>
public record RemoveTenantUserRequest([Required] string UserId);
