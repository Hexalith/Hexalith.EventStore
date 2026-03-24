namespace Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

/// <summary>
/// A user assigned to a tenant with their role.
/// </summary>
/// <param name="Email">The user's email address.</param>
/// <param name="Role">The user's role within this tenant.</param>
/// <param name="AddedAtUtc">When the user was added to this tenant.</param>
public record TenantUser(string Email, string Role, DateTimeOffset AddedAtUtc);
