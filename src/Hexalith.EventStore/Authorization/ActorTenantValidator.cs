
using System.Security.Claims;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Configuration;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Identity;
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

        if (!TenantCanonicalizer.TryCanonicalize(tenantId, out string canonicalTenant)) {
            return TenantValidationResult.Denied("Tenant is invalid or reserved.",
                string.IsNullOrEmpty(tenantId) ? AuthorizationFailureReason.TenantMissing : AuthorizationFailureReason.TenantMismatch);
        }

        if (!TenantCanonicalizer.TryCanonicalizeGrants(
            user.FindAll("eventstore:tenant").Select(claim => claim.Value), out _)) {
            return TenantValidationResult.Denied("Tenant authorization claims are invalid or ambiguous.",
                AuthorizationFailureReason.TenantAmbiguous);
        }

        string userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException(
                "No user identifier (NameIdentifier/sub) found in claims. "
                + "Actor-based authorization requires a unique user identifier.");

        string actorType = options.Value.TenantValidatorActorName!;
        Log.ActorTenantValidation(logger, userId, canonicalTenant, actorType);

        ActorValidationResponse response;
        try {
            ITenantValidatorActor proxy = actorProxyFactory.CreateActorProxy<ITenantValidatorActor>(
                new ActorId(canonicalTenant),
                actorType);

            response = await proxy.ValidateTenantAccessAsync(
                new TenantValidationRequest(userId, canonicalTenant, aggregateId)).ConfigureAwait(false);
        }
        catch (Exception ex) {
            Log.ActorTenantValidationFailed(logger, ex, userId, canonicalTenant, actorType,
                ex.InnerException?.GetType().Name ?? ex.GetType().Name);

            throw new AuthorizationServiceUnavailableException(
                actorType,
                canonicalTenant,
                ex.Message,
                ex);
        }

        if (response is null) {
            Log.ActorTenantValidationFailed(logger, null!, userId, canonicalTenant, actorType, "NullResponse");

            throw new AuthorizationServiceUnavailableException(
                actorType,
                canonicalTenant,
                "Actor returned null response",
                new InvalidOperationException("Actor returned null response"));
        }

        if (response.IsAuthorized) {
            Log.ActorTenantValidationAllowed(logger, userId, canonicalTenant);
            return TenantValidationResult.AllowedFor(canonicalTenant);
        }

        Log.ActorTenantValidationDenied(logger, "TenantAccessDenied", userId, canonicalTenant, response.Reason);
        AuthorizationFailureReason reasonCode = AuthorizationFailureReasonExtensions.FromReasonCode(
            response.ReasonCode,
            AuthorizationFailureReason.TenantUnavailable);
        if (response.ReasonCode is not null && reasonCode == AuthorizationFailureReason.TenantUnavailable
            && !string.Equals(response.ReasonCode, AuthorizationFailureReason.TenantUnavailable.ToReasonCode(), StringComparison.Ordinal)) {
            Log.ActorTenantUnrecognizedReasonCode(logger, response.ReasonCode, userId, canonicalTenant);
        }

        return TenantValidationResult.Denied(response.Reason ?? "Tenant access denied by actor.", reasonCode);
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

        [LoggerMessage(
            EventId = 1204,
            Level = LogLevel.Warning,
            Message = "Actor tenant validation returned unrecognized reason code: ReasonCode={ReasonCode}, UserId={UserId}, TenantId={TenantId}, Stage=ActorTenantUnrecognizedReasonCode")]
        public static partial void ActorTenantUnrecognizedReasonCode(
            ILogger logger, string reasonCode, string userId, string tenantId);
    }
}
