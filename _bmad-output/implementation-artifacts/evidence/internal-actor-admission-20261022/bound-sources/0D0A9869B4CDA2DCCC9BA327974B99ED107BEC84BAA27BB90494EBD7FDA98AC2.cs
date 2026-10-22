
using Dapr.Actors;

namespace Hexalith.EventStore.Server.Actors.Authorization;

/// <summary>
/// DAPR actor interface for application-managed RBAC authorization.
/// </summary>
/// <remarks>
/// <para>Applications implement this interface to provide dynamic role-based access control
/// managed at runtime via actor state.</para>
/// <para><b>messageCategory:</b> Unlike claims-based authorization (which treats "command" and
/// "query" identically), actor-based implementations CAN distinguish read vs write operations
/// using <see cref="RbacValidationRequest.MessageCategory"/>.</para>
/// <para>See <see cref="ITenantValidatorActor"/> remarks for general implementation guidance.</para>
/// </remarks>
public interface IRbacValidatorActor : IActor {
    /// <summary>
    /// Validates whether a user has RBAC permission for the specified operation.
    /// </summary>
    /// <param name="request">The RBAC validation request containing user, tenant, domain, and permission details.</param>
    /// <returns>An <see cref="ActorValidationResponse"/> indicating whether access is authorized.</returns>
    Task<ActorValidationResponse> ValidatePermissionAsync(RbacValidationRequest request);
}
