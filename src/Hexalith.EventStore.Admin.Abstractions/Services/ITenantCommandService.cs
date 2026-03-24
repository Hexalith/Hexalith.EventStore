using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Tenant write operations delegated to Hexalith.Tenants peer service.
/// EventStore does NOT own tenant state (FR77).
/// </summary>
public interface ITenantCommandService
{
    /// <summary>Creates a new tenant.</summary>
    /// <param name="request">The create tenant request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> CreateTenantAsync(CreateTenantRequest request, CancellationToken ct = default);

    /// <summary>Disables (suspends) an active tenant.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> DisableTenantAsync(string tenantId, CancellationToken ct = default);

    /// <summary>Enables a suspended tenant.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> EnableTenantAsync(string tenantId, CancellationToken ct = default);

    /// <summary>Adds a user to a tenant.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="role">The user's role.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> AddUserToTenantAsync(string tenantId, string email, string role, CancellationToken ct = default);

    /// <summary>Removes a user from a tenant.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> RemoveUserFromTenantAsync(string tenantId, string email, CancellationToken ct = default);

    /// <summary>Changes a user's role within a tenant.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="newRole">The new role to assign.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> ChangeUserRoleAsync(string tenantId, string email, string newRole, CancellationToken ct = default);
}
