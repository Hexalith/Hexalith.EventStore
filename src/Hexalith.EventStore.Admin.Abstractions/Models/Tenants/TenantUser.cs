namespace Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

/// <summary>
/// A user assigned to a tenant with their role.
/// </summary>
/// <param name="UserId">The user identifier.</param>
/// <param name="Role">The user's role within this tenant.</param>
public record TenantUser(string UserId, string Role);
