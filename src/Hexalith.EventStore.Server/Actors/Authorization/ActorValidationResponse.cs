
namespace Hexalith.EventStore.Server.Actors.Authorization;

/// <summary>
/// Shared response DTO for actor-based authorization validation.
/// Used by both <see cref="ITenantValidatorActor"/> and <see cref="IRbacValidatorActor"/>.
/// </summary>
/// <param name="IsAuthorized">Whether the authorization check passed.</param>
/// <param name="Reason">The reason for denial, or null if authorized.</param>
/// <param name="ReasonCode">Stable authorization failure reason code, or null for legacy/allowed responses.</param>
public record ActorValidationResponse(bool IsAuthorized, string? Reason = null, string? ReasonCode = null);
