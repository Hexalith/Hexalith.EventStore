using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;

namespace Hexalith.EventStore.Admin.Abstractions.Services;

/// <summary>
/// Tenant write operations routed through EventStore command pipeline.
/// EventStore does NOT own tenant state (FR77).
/// </summary>
public interface ITenantCommandService
{
    /// <summary>Creates a new tenant.</summary>
    /// <param name="request">The create tenant request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> CreateTenantAsync(CreateTenantRequest request, CancellationToken ct = default);

    /// <summary>Disables an active tenant.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> DisableTenantAsync(string tenantId, CancellationToken ct = default);

    /// <summary>Enables a disabled tenant.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> EnableTenantAsync(string tenantId, CancellationToken ct = default);

    /// <summary>Adds a user to a tenant.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="role">The user's role.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> AddUserToTenantAsync(string tenantId, string userId, string role, CancellationToken ct = default);

    /// <summary>Removes a user from a tenant.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> RemoveUserFromTenantAsync(string tenantId, string userId, CancellationToken ct = default);

    /// <summary>Changes a user's role within a tenant.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="newRole">The new role to assign.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<AdminOperationResult> ChangeUserRoleAsync(string tenantId, string userId, string newRole, CancellationToken ct = default);
}
