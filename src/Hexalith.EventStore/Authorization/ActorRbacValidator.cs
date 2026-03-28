
using System.Security.Claims;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Configuration;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Server.Actors.Authorization;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Authorization;

/// <summary>
/// Actor-based RBAC validator. Delegates role-based authorization to a DAPR actor
/// identified by <see cref="EventStoreAuthorizationOptions.RbacValidatorActorName"/>.
/// Unlike claims-based, this CAN distinguish "command" vs "query" message categories.
/// </summary>
public partial class ActorRbacValidator(
    IActorProxyFactory actorProxyFactory,
    IOptions<EventStoreAuthorizationOptions> options,
    ILogger<ActorRbacValidator> logger) : IRbacValidator {
    /// <inheritdoc/>
    public async Task<RbacValidationResult> ValidateAsync(
        ClaimsPrincipal user,
        string tenantId,
        string domain,
        string messageType,
        string messageCategory,
        CancellationToken cancellationToken,
        string? aggregateId = null) {
        ArgumentNullException.ThrowIfNull(user);

        cancellationToken.ThrowIfCancellationRequested();

        string userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException(
                "No user identifier (NameIdentifier/sub) found in claims. "
                + "Actor-based authorization requires a unique user identifier.");

        string actorType = options.Value.RbacValidatorActorName!;
        Log.ActorRbacValidation(logger, userId, tenantId, domain, messageType, messageCategory, actorType);

        ActorValidationResponse response;
        try {
            IRbacValidatorActor proxy = actorProxyFactory.CreateActorProxy<IRbacValidatorActor>(
                new ActorId(tenantId),
                actorType);

            response = await proxy.ValidatePermissionAsync(
                new RbacValidationRequest(userId, tenantId, domain, messageType, messageCategory, aggregateId)).ConfigureAwait(false);
        }
        catch (Exception ex) {
            Log.ActorRbacValidationFailed(logger, ex, userId, tenantId, actorType,
                ex.InnerException?.GetType().Name ?? ex.GetType().Name);

            throw new AuthorizationServiceUnavailableException(
                actorType,
                tenantId,
                ex.Message,
                ex);
        }

        if (response is null) {
            Log.ActorRbacValidationFailed(logger, null!, userId, tenantId, actorType, "NullResponse");

            throw new AuthorizationServiceUnavailableException(
                actorType,
                tenantId,
                "Actor returned null response",
                new InvalidOperationException("Actor returned null response"));
        }

        if (response.IsAuthorized) {
            Log.ActorRbacValidationAllowed(logger, userId, tenantId, domain, messageType, messageCategory);
            return RbacValidationResult.Allowed;
        }

        Log.ActorRbacValidationDenied(logger, "RbacAccessDenied", userId, tenantId, domain, messageType, messageCategory, response.Reason);
        return RbacValidationResult.Denied(response.Reason ?? "RBAC access denied by actor.");
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1210,
            Level = LogLevel.Debug,
            Message = "Actor RBAC validation: UserId={UserId}, TenantId={TenantId}, Domain={Domain}, MessageType={MessageType}, MessageCategory={MessageCategory}, ActorType={ActorType}, Stage=ActorRbacValidation")]
        public static partial void ActorRbacValidation(
            ILogger logger, string userId, string tenantId, string domain, string messageType, string messageCategory, string actorType);

        [LoggerMessage(
            EventId = 1211,
            Level = LogLevel.Debug,
            Message = "Actor RBAC validation allowed: UserId={UserId}, TenantId={TenantId}, Domain={Domain}, MessageType={MessageType}, MessageCategory={MessageCategory}, Stage=ActorRbacValidationAllowed")]
        public static partial void ActorRbacValidationAllowed(
            ILogger logger, string userId, string tenantId, string domain, string messageType, string messageCategory);

        [LoggerMessage(
            EventId = 1212,
            Level = LogLevel.Warning,
            Message = "Actor RBAC validation denied: SecurityEvent={SecurityEvent}, UserId={UserId}, TenantId={TenantId}, Domain={Domain}, MessageType={MessageType}, MessageCategory={MessageCategory}, Reason={Reason}, Stage=ActorRbacValidationDenied")]
        public static partial void ActorRbacValidationDenied(
            ILogger logger, string securityEvent, string userId, string tenantId, string domain, string messageType, string messageCategory, string? reason);

        [LoggerMessage(
            EventId = 1213,
            Level = LogLevel.Error,
            Message = "Actor RBAC validation failed: UserId={UserId}, TenantId={TenantId}, ActorType={ActorType}, InnerExceptionType={InnerExceptionType}, Stage=ActorRbacValidationFailed")]
        public static partial void ActorRbacValidationFailed(
            ILogger logger, Exception? ex, string userId, string tenantId, string actorType, string innerExceptionType);
    }
}
