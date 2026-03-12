
using Hexalith.EventStore.CommandApi.Authorization;
using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Pipeline.Queries;

using MediatR;

namespace Hexalith.EventStore.CommandApi.Pipeline;

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

        string? tenant, domain, messageType, messageCategory;
        if (request is SubmitCommand command) {
            (tenant, domain, messageType, messageCategory) = (command.Tenant, command.Domain, command.CommandType, "command");
        }
        else if (request is SubmitQuery query) {
            (tenant, domain, messageType, messageCategory) = (query.Tenant, query.Domain, query.QueryType, "query");
        }
        else {
            return await next().ConfigureAwait(false);
        }

        HttpContext httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext is not available in AuthorizationBehavior.");
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
            .ValidateAsync(user, tenant, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "ITenantValidator.ValidateAsync returned null. This is a server bug, not a user authorization failure.");
        if (!tenantResult.IsAuthorized) {
            Log.AuthorizationFailed(logger, correlationId, causationId, "N/A",
                tenant, domain, messageType,
                tenantResult.Reason ?? "Tenant access denied.", sourceIp);
            throw new CommandAuthorizationException(
                tenant, domain, messageType,
                tenantResult.Reason ?? "Tenant access denied.");
        }

        // RBAC validation (was inline domain + permission checks)
        RbacValidationResult rbacResult = await rbacValidator
            .ValidateAsync(user, tenant, domain,
                messageType, messageCategory, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "IRbacValidator.ValidateAsync returned null. This is a server bug, not a user authorization failure.");
        if (!rbacResult.IsAuthorized) {
            // Collect tenant claims for logging context only
            var tenantClaims = user.FindAll("eventstore:tenant")
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();
            string tenantClaimsCsv = tenantClaims.Count == 0 ? "none" : string.Join(",", tenantClaims);
            Log.AuthorizationFailed(logger, correlationId, causationId, tenantClaimsCsv,
                tenant, domain, messageType,
                rbacResult.Reason ?? "RBAC check failed.", sourceIp);
            throw new CommandAuthorizationException(
                tenant, domain, messageType,
                rbacResult.Reason ?? "RBAC check failed.");
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
