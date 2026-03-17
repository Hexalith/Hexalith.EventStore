
using System.Security.Claims;

using Hexalith.EventStore.CommandApi.Authorization;
using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.Contracts.Validation;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.CommandApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/queries/validate")]
[Consumes("application/json")]
[Tags("Validation")]
public partial class QueryValidationController(
    ITenantValidator tenantValidator,
    IRbacValidator rbacValidator,
    ILogger<QueryValidationController> logger) : ControllerBase {
    /// <summary>
    /// Pre-flight validation for a query submission.
    /// </summary>
    /// <remarks>
    /// Validates tenant authorization and RBAC permissions without executing the query.
    /// Returns a result indicating whether the query would be accepted.
    /// </remarks>
    /// <response code="200">Validation result returned. Check the IsAuthorized property.</response>
    /// <response code="400">Malformed request body.</response>
    /// <response code="401">Authentication required. Provide a valid JWT Bearer token.</response>
    /// <response code="403">Forbidden. Valid JWT but not authorized for the requested tenant.</response>
    /// <response code="429">Rate limit exceeded. Retry after the Retry-After interval.</response>
    /// <response code="503">Service unavailable. The processing pipeline is temporarily down.</response>
    [HttpPost]
    [RequestSizeLimit(1_048_576)]
    [ProducesResponseType(typeof(PreflightValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> Validate(
        [FromBody] ValidateQueryRequest request,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();

        Log.PreflightQueryCheckReceived(logger, correlationId, request.Tenant,
            request.Domain, request.QueryType, request.AggregateId);

        // Store tenant for rate limiter OnRejected callback
        if (!string.IsNullOrEmpty(request.Tenant)) {
            HttpContext.Items["RequestTenantId"] = request.Tenant;
        }

        ClaimsPrincipal user = HttpContext.User;

        // Extract UserId from JWT for logging — mirror CommandValidationController pattern
        string? userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(userId)) {
            logger.LogWarning(
                "JWT 'sub' claim missing for pre-flight query validation. CorrelationId={CorrelationId}.",
                correlationId);

            const string reason = "User is not authenticated.";
            Log.PreflightQueryDenied(logger, correlationId, request.Tenant,
                request.Domain, request.QueryType,
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
            Log.PreflightQueryDenied(logger, correlationId, request.Tenant,
                request.Domain, request.QueryType,
                tenantResult.Reason ?? "Tenant access denied.", "tenant");

            return Ok(new PreflightValidationResult(
                false, tenantResult.Reason ?? "Tenant access denied."));
        }

        // RBAC validation
        RbacValidationResult rbacResult = await rbacValidator
            .ValidateAsync(user, request.Tenant, request.Domain,
                request.QueryType, "query", cancellationToken, request.AggregateId)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "IRbacValidator.ValidateAsync returned null. This is a server bug, not a user authorization failure.");

        if (!rbacResult.IsAuthorized) {
            Log.PreflightQueryDenied(logger, correlationId, request.Tenant,
                request.Domain, request.QueryType,
                rbacResult.Reason ?? "RBAC check failed.", "rbac");

            return Ok(new PreflightValidationResult(
                false, rbacResult.Reason ?? "RBAC check failed."));
        }

        Log.PreflightQueryPassed(logger, correlationId, request.Tenant,
            request.Domain, request.QueryType);

        return Ok(new PreflightValidationResult(true));
    }

    private static partial class Log {
        [LoggerMessage(
            EventId = 1045,
            Level = LogLevel.Debug,
            Message = "Pre-flight query validation received: CorrelationId={CorrelationId}, Tenant={Tenant}, Domain={Domain}, QueryType={QueryType}, AggregateId={AggregateId}")]
        public static partial void PreflightQueryCheckReceived(
            ILogger logger,
            string correlationId,
            string tenant,
            string domain,
            string queryType,
            string? aggregateId);

        [LoggerMessage(
            EventId = 1046,
            Level = LogLevel.Debug,
            Message = "Pre-flight query validation passed: CorrelationId={CorrelationId}, Tenant={Tenant}, Domain={Domain}, QueryType={QueryType}")]
        public static partial void PreflightQueryPassed(
            ILogger logger,
            string correlationId,
            string tenant,
            string domain,
            string queryType);

        [LoggerMessage(
            EventId = 1047,
            Level = LogLevel.Warning,
            Message = "Pre-flight query validation denied: SecurityEvent={SecurityEvent}, CorrelationId={CorrelationId}, Tenant={Tenant}, Domain={Domain}, QueryType={QueryType}, Reason={Reason}, DeniedBy={DeniedBy}")]
        public static partial void PreflightQueryDenied(
            ILogger logger,
            string correlationId,
            string tenant,
            string domain,
            string queryType,
            string reason,
            string deniedBy,
            string securityEvent = "PreflightQueryAuthorizationDenied");
    }
}
