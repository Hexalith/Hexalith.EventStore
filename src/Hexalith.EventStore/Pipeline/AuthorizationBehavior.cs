
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Pipeline.Queries;

using MediatR;

namespace Hexalith.EventStore.Pipeline;

public partial class AuthorizationBehavior<TRequest, TResponse>(
    IHttpContextAccessor httpContextAccessor,
    ITenantValidator tenantValidator,
    IRbacValidator rbacValidator,
    ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull {
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        string? tenant, domain, messageType, messageCategory, aggregateId;
        if (request is SubmitCommand command) {
            (tenant, domain, messageType, messageCategory, aggregateId) = (command.Tenant, command.Domain, command.CommandType, "command", command.AggregateId);
        }
        else if (request is SubmitQuery query) {
            (tenant, domain, messageType, messageCategory, aggregateId) = (query.Tenant, query.Domain, query.QueryType, "query", query.AggregateId);
        }
        else {
            return await next().ConfigureAwait(false);
        }

        // Internal service calls (e.g. TenantBootstrapHostedService) send commands via MediatR
        // without an HTTP request context. Skip API-level authorization for these — domain-level
        // RBAC in aggregate Handle methods still applies.
        if (httpContextAccessor.HttpContext is not { } httpContext)
        {
            return await next().ConfigureAwait(false);
        }
        System.Security.Claims.ClaimsPrincipal user = httpContext.User
            ?? throw new InvalidOperationException("HttpContext.User is not available in AuthorizationBehavior.");

        if (user.Identity?.IsAuthenticated != true) {
            throw new CommandAuthorizationException(
                tenant,
                null,
                null,
                "User is not authenticated.");
        }

        string correlationId = httpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? "unknown";
        string causationId = correlationId; // For original submissions, CausationId = CorrelationId
        string? sourceIp = httpContext.Connection.RemoteIpAddress?.ToString();

        // Tenant validation (moved from controller — Layer 4 consolidation)
        TenantValidationResult tenantResult = await tenantValidator
            .ValidateAsync(user, tenant, cancellationToken, aggregateId).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "ITenantValidator.ValidateAsync returned null. This is a server bug, not a user authorization failure.");
        if (!tenantResult.IsAuthorized) {
            string tenantReason = EnsureReasonNamesTenant(
                tenant,
                tenantResult.Reason ?? "Tenant access denied.");
            Log.AuthorizationFailed(logger, correlationId, causationId, "N/A",
                tenant, domain, messageType,
                tenantReason, sourceIp);
            throw new CommandAuthorizationException(
                tenant, domain, messageType,
                tenantReason);
        }

        // RBAC validation (was inline domain + permission checks)
        RbacValidationResult rbacResult = await rbacValidator
            .ValidateAsync(user, tenant, domain,
                messageType, messageCategory, cancellationToken, aggregateId).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "IRbacValidator.ValidateAsync returned null. This is a server bug, not a user authorization failure.");
        if (!rbacResult.IsAuthorized) {
            string rbacReason = EnsureReasonNamesTenant(
                tenant,
                rbacResult.Reason ?? "RBAC check failed.");
            // Collect tenant claims for logging context only
            var tenantClaims = user.FindAll("eventstore:tenant")
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();
            string tenantClaimsCsv = tenantClaims.Count == 0 ? "none" : string.Join(",", tenantClaims);
            Log.AuthorizationFailed(logger, correlationId, causationId, tenantClaimsCsv,
                tenant, domain, messageType,
                rbacReason, sourceIp);
            throw new CommandAuthorizationException(
                tenant, domain, messageType,
                rbacReason);
        }

        Log.AuthorizationPassed(
            logger,
            correlationId,
            causationId,
            tenant,
            domain,
            messageType);

        return await next().ConfigureAwait(false);
    }

    private static string EnsureReasonNamesTenant(string? tenant, string reason) {
        if (string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(tenant)) {
            return reason;
        }

        return reason.Contains($"tenant '{tenant}'", StringComparison.OrdinalIgnoreCase)
            ? reason
            : $"Not authorized for tenant '{tenant}'. {reason}";
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1020,
            Level = LogLevel.Debug,
            Message = "Authorization succeeded: CorrelationId={CorrelationId}, CausationId={CausationId}, Tenant={Tenant}, Domain={Domain}, MessageType={MessageType}, Stage=AuthorizationPassed")]
        public static partial void AuthorizationPassed(
            ILogger logger,
            string correlationId,
            string causationId,
            string tenant,
            string domain,
            string messageType);

        [LoggerMessage(
            EventId = 1021,
            Level = LogLevel.Warning,
            Message = "Authorization failed: SecurityEvent={SecurityEvent}, CorrelationId={CorrelationId}, CausationId={CausationId}, TenantClaims={TenantClaims}, Tenant={Tenant}, Domain={Domain}, MessageType={MessageType}, Reason={Reason}, SourceIp={SourceIp}, FailureLayer={FailureLayer}, Stage=AuthorizationFailed")]
        public static partial void AuthorizationFailed(
            ILogger logger,
            string correlationId,
            string causationId,
            string tenantClaims,
            string tenant,
            string domain,
            string messageType,
            string reason,
            string? sourceIp,
            string failureLayer = "MediatR.AuthorizationBehavior",
            string securityEvent = "AuthorizationDenied");
    }
}
