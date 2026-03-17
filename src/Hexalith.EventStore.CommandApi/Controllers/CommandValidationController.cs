
using System.Security.Claims;

using Hexalith.EventStore.CommandApi.Authorization;
using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.Contracts.Validation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.CommandApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/commands/validate")]
[Consumes("application/json")]
[Tags("Validation")]
public partial class CommandValidationController(
    ITenantValidator tenantValidator,
    IRbacValidator rbacValidator,
    ILogger<CommandValidationController> logger) : ControllerBase {
    [HttpPost]
    [RequestSizeLimit(1_048_576)]
    [ProducesResponseType(typeof(PreflightValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> Validate(
        [FromBody] ValidateCommandRequest request,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();

        Log.PreflightCheckReceived(logger, correlationId, request.Tenant,
            request.Domain, request.CommandType, request.AggregateId);

        // Store tenant for rate limiter OnRejected callback
        if (!string.IsNullOrEmpty(request.Tenant)) {
            HttpContext.Items["RequestTenantId"] = request.Tenant;
        }

        ClaimsPrincipal user = HttpContext.User;

        // Extract UserId from JWT for logging — mirror CommandsController pattern
        string? userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(userId)) {
            logger.LogWarning(
                "JWT 'sub' claim missing for pre-flight validation. CorrelationId={CorrelationId}.",
                correlationId);

            const string reason = "User is not authenticated.";
            Log.PreflightDenied(logger, correlationId, request.Tenant,
                request.Domain, request.CommandType,
                reason, "jwt");

            return Unauthorized();
        }

        // Tenant validation
        TenantValidationResult tenantResult = await tenantValidator
            .ValidateAsync(user, request.Tenant, cancellationToken, request.AggregateId)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "ITenantValidator.ValidateAsync returned null. This is a server bug, not a user authorization failure.");

        if (!tenantResult.IsAuthorized) {
            Log.PreflightDenied(logger, correlationId, request.Tenant,
                request.Domain, request.CommandType,
                tenantResult.Reason ?? "Tenant access denied.", "tenant");

            return Ok(new PreflightValidationResult(
                false, tenantResult.Reason ?? "Tenant access denied."));
        }

        // RBAC validation
        RbacValidationResult rbacResult = await rbacValidator
            .ValidateAsync(user, request.Tenant, request.Domain,
                request.CommandType, "command", cancellationToken, request.AggregateId)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "IRbacValidator.ValidateAsync returned null. This is a server bug, not a user authorization failure.");

        if (!rbacResult.IsAuthorized) {
            Log.PreflightDenied(logger, correlationId, request.Tenant,
                request.Domain, request.CommandType,
                rbacResult.Reason ?? "RBAC check failed.", "rbac");

            return Ok(new PreflightValidationResult(
                false, rbacResult.Reason ?? "RBAC check failed."));
        }

        Log.PreflightPassed(logger, correlationId, request.Tenant,
            request.Domain, request.CommandType);

        return Ok(new PreflightValidationResult(true));
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1040,
            Level = LogLevel.Debug,
            Message = "Pre-flight command validation received: CorrelationId={CorrelationId}, Tenant={Tenant}, Domain={Domain}, CommandType={CommandType}, AggregateId={AggregateId}")]
        public static partial void PreflightCheckReceived(
            ILogger logger,
            string correlationId,
            string tenant,
            string domain,
            string commandType,
            string? aggregateId);

        [LoggerMessage(
            EventId = 1041,
            Level = LogLevel.Debug,
            Message = "Pre-flight command validation passed: CorrelationId={CorrelationId}, Tenant={Tenant}, Domain={Domain}, CommandType={CommandType}")]
        public static partial void PreflightPassed(
            ILogger logger,
            string correlationId,
            string tenant,
            string domain,
            string commandType);

        [LoggerMessage(
            EventId = 1042,
            Level = LogLevel.Warning,
            Message = "Pre-flight command validation denied: SecurityEvent={SecurityEvent}, CorrelationId={CorrelationId}, Tenant={Tenant}, Domain={Domain}, CommandType={CommandType}, Reason={Reason}, DeniedBy={DeniedBy}")]
        public static partial void PreflightDenied(
            ILogger logger,
            string correlationId,
            string tenant,
            string domain,
            string commandType,
            string reason,
            string deniedBy,
            string securityEvent = "PreflightAuthorizationDenied");
    }
}
