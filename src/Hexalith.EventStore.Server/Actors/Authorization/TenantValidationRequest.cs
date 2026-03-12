
namespace Hexalith.EventStore.Server.Actors.Authorization;

/// <summary>
/// Request DTO for actor-based tenant validation.
/// Extracted from <see cref="System.Security.Claims.ClaimsPrincipal"/> by the proxy
/// and serialized to the DAPR actor.
/// </summary>
/// <param name="UserId">The unique user identifier (from NameIdentifier/sub claim).</param>
/// <param name="TenantId">The tenant ID to validate access for.</param>
public record TenantValidationRequest(string UserId, string TenantId, string? AggregateId = null);
