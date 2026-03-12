
namespace Hexalith.EventStore.Server.Actors.Authorization;

/// <summary>
/// Request DTO for actor-based RBAC validation.
/// Extracted from <see cref="System.Security.Claims.ClaimsPrincipal"/> by the proxy
/// and serialized to the DAPR actor.
/// </summary>
/// <param name="UserId">The unique user identifier (from NameIdentifier/sub claim).</param>
/// <param name="TenantId">The tenant ID for the operation.</param>
/// <param name="Domain">The domain name for authorization checking.</param>
/// <param name="MessageType">The command type or query type name.</param>
/// <param name="MessageCategory">The message category: "command" or "query".</param>
public record RbacValidationRequest(
    string UserId,
    string TenantId,
    string Domain,
    string MessageType,
    string MessageCategory);
