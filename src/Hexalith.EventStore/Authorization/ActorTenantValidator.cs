
using System.Security.Claims;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Configuration;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Server.Actors.Authorization;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Authorization;

/// <summary>
/// Actor-based tenant validator. Delegates tenant authorization to a DAPR actor
/// identified by <see cref="EventStoreAuthorizationOptions.TenantValidatorActorName"/>.
/// </summary>
public partial class ActorTenantValidator(
    IActorProxyFactory actorProxyFactory,
    IOptions<EventStoreAuthorizationOptions> options,
    ILogger<ActorTenantValidator> logger) : ITenantValidator {
    /// <inheritdoc/>
    public async Task<TenantValidationResult> ValidateAsync(
        ClaimsPrincipal user,
        string tenantId,
        CancellationToken cancellationToken,
        string? aggregateId = null) {
        ArgumentNullException.ThrowIfNull(user);

        cancellationToken.ThrowIfCancellationRequested();

        string userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException(
                "No user identifier (NameIdentifier/sub) found in claims. "
                + "Actor-based authorization requires a unique user identifier.");

        string actorType = options.Value.TenantValidatorActorName!;
        Log.ActorTenantValidation(logger, userId, tenantId, actorType);

        ActorValidationResponse response;
        try {
            ITenantValidatorActor proxy = actorProxyFactory.CreateActorProxy<ITenantValidatorActor>(
                new ActorId(tenantId),
                actorType);

            response = await proxy.ValidateTenantAccessAsync(
                new TenantValidationRequest(userId, tenantId, aggregateId)).ConfigureAwait(false);
        }
        catch (Exception ex) {
            Log.ActorTenantValidationFailed(logger, ex, userId, tenantId, actorType,
                ex.InnerException?.GetType().Name ?? ex.GetType().Name);

            throw new AuthorizationServiceUnavailableException(
                actorType,
                tenantId,
                ex.Message,
                ex);
        }

        if (response is null) {
            Log.ActorTenantValidationFailed(logger, null!, userId, tenantId, actorType, "NullResponse");

            throw new AuthorizationServiceUnavailableException(
                actorType,
                tenantId,
                "Actor returned null response",
                new InvalidOperationException("Actor returned null response"));
        }

        if (response.IsAuthorized) {
            Log.ActorTenantValidationAllowed(logger, userId, tenantId);
            return TenantValidationResult.Allowed;
        }

        Log.ActorTenantValidationDenied(logger, "TenantAccessDenied", userId, tenantId, response.Reason);
        return TenantValidationResult.Denied(response.Reason ?? "Tenant access denied by actor.");
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1200,
            Level = LogLevel.Debug,
            Message = "Actor tenant validation: UserId={UserId}, TenantId={TenantId}, ActorType={ActorType}, Stage=ActorTenantValidation")]
        public static partial void ActorTenantValidation(
            ILogger logger, string userId, string tenantId, string actorType);

        [LoggerMessage(
            EventId = 1201,
            Level = LogLevel.Debug,
            Message = "Actor tenant validation allowed: UserId={UserId}, TenantId={TenantId}, Stage=ActorTenantValidationAllowed")]
        public static partial void ActorTenantValidationAllowed(
            ILogger logger, string userId, string tenantId);

        [LoggerMessage(
            EventId = 1202,
            Level = LogLevel.Warning,
            Message = "Actor tenant validation denied: SecurityEvent={SecurityEvent}, UserId={UserId}, TenantId={TenantId}, Reason={Reason}, Stage=ActorTenantValidationDenied")]
        public static partial void ActorTenantValidationDenied(
            ILogger logger, string securityEvent, string userId, string tenantId, string? reason);

        [LoggerMessage(
            EventId = 1203,
            Level = LogLevel.Error,
            Message = "Actor tenant validation failed: UserId={UserId}, TenantId={TenantId}, ActorType={ActorType}, InnerExceptionType={InnerExceptionType}, Stage=ActorTenantValidationFailed")]
        public static partial void ActorTenantValidationFailed(
            ILogger logger, Exception? ex, string userId, string tenantId, string actorType, string innerExceptionType);
    }
}
