using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.Server.Controllers;

/// <summary>
/// REST API controller for consistency check operations.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/admin/consistency")]
[Tags("Admin - Consistency")]
public class AdminConsistencyController(
    IConsistencyQueryService queryService,
    IConsistencyCommandService commandService,
    ILogger<AdminConsistencyController> logger) : ControllerBase {
    /// <summary>
    /// Gets the full result of a consistency check including anomaly details.
    /// </summary>
    [HttpGet("checks/{checkId}")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(ConsistencyCheckResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetCheckResult(
        string checkId,
        CancellationToken ct = default) {
        try {
            ConsistencyCheckResult? result = await queryService
                .GetCheckResultAsync(checkId, ct)
                .ConfigureAwait(false);

            if (result is null) {
                return CreateProblemResult(StatusCodes.Status404NotFound, "Not Found", $"Consistency check '{checkId}' not found.");
            }

            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(GetCheckResult), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(GetCheckResult), ex);
        }
    }

    /// <summary>
    /// Gets consistency check summaries, optionally filtered by tenant.
    /// </summary>
    [HttpGet("checks")]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(IReadOnlyList<ConsistencyCheckSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetChecks(
        [FromQuery] string? tenantId,
        CancellationToken ct = default) {
        try {
            string? effectiveTenantId = ResolveTenantScope(tenantId);
            IReadOnlyList<ConsistencyCheckSummary> result = await queryService
                .GetChecksAsync(effectiveTenantId, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(GetChecks), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(GetChecks), ex);
        }
    }

    /// <summary>
    /// Triggers a new consistency check.
    /// </summary>
    [HttpPost("checks")]
    [Authorize(Policy = AdminAuthorizationPolicies.Operator)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [RequestSizeLimit(AdminRequestSizeLimits.OrdinaryJsonBody)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> TriggerCheck(
        [FromBody] ConsistencyCheckRequest request,
        CancellationToken ct = default) {
        try {
            ArgumentNullException.ThrowIfNull(request);
            if (!TryResolveTenantScopeForBody(request.TenantId, out string? effectiveTenantId)) {
                return TenantDenied();
            }

            AdminOperationResult result = await commandService
                .TriggerCheckAsync(effectiveTenantId, request.Domain, request.CheckTypes, ct)
                .ConfigureAwait(false);
            return MapAsyncOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(TriggerCheck), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(TriggerCheck), ex);
        }
    }

    /// <summary>
    /// Cancels a running consistency check.
    /// </summary>
    [HttpPost("checks/{checkId}/cancel")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CancelCheck(
        string checkId,
        CancellationToken ct = default) {
        try {
            AdminOperationResult result = await commandService
                .CancelCheckAsync(checkId, ct)
                .ConfigureAwait(false);
            return MapOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(CancelCheck), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(CancelCheck), ex);
        }
    }

    private string? ResolveTenantScope(string? requestedTenantId) {
        if (requestedTenantId is not null) {
            return requestedTenantId;
        }

        if (User.HasClaim(AdminClaimTypes.AdminRole, nameof(Abstractions.Models.Common.AdminRole.Admin))) {
            return null;
        }

        return User.FindFirst(AdminClaimTypes.Tenant)?.Value;
    }

    private bool TryResolveTenantScopeForBody(string? requestedTenantId, out string? effectiveTenantId) {
        if (User.HasClaim(AdminClaimTypes.AdminRole, nameof(Abstractions.Models.Common.AdminRole.Admin))) {
            effectiveTenantId = requestedTenantId;
            return true;
        }

        string[] tenantClaims = User
            .FindAll(AdminClaimTypes.Tenant)
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (tenantClaims.Length == 0) {
            effectiveTenantId = null;
            return false;
        }

        if (requestedTenantId is null) {
            effectiveTenantId = tenantClaims[0];
            return true;
        }

        effectiveTenantId = requestedTenantId;
        return tenantClaims.Contains(requestedTenantId, StringComparer.Ordinal);
    }

    private ObjectResult TenantDenied() {
        string correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? "unknown";
        logger.LogWarning("Admin consistency tenant access denied. CorrelationId={CorrelationId}", correlationId);
        return new ObjectResult(new ProblemDetails {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = "The request is not authorized for the requested scope.",
            Extensions = { ["correlationId"] = correlationId },
        }) { StatusCode = StatusCodes.Status403Forbidden };
    }

    private IActionResult MapOperationResult(AdminOperationResult? result) {
        if (result is null) {
            return CreateProblemResult(StatusCodes.Status500InternalServerError, "Internal Server Error", "No result returned from the service.");
        }

        if (result.Success) {
            return Ok(result);
        }

        return result.ErrorCode switch {
            "NotFound" => CreateProblemResult(StatusCodes.Status404NotFound, "Not Found", result.Message),
            "Unauthorized" => CreateProblemResult(StatusCodes.Status403Forbidden, "Forbidden", result.Message),
            "InvalidOperation" => CreateProblemResult(StatusCodes.Status422UnprocessableEntity, "Invalid Operation", result.Message),
            "Conflict" => CreateProblemResult(StatusCodes.Status409Conflict, "Conflict", result.Message),
            _ => CreateProblemResult(StatusCodes.Status500InternalServerError, "Internal Server Error", result.Message),
        };
    }

    private IActionResult MapAsyncOperationResult(AdminOperationResult? result) {
        if (result is null) {
            return CreateProblemResult(StatusCodes.Status500InternalServerError, "Internal Server Error", "No result returned from the service.");
        }

        if (result.Success) {
            return Accepted(result);
        }

        return result.ErrorCode switch {
            "NotFound" => CreateProblemResult(StatusCodes.Status404NotFound, "Not Found", result.Message),
            "Unauthorized" => CreateProblemResult(StatusCodes.Status403Forbidden, "Forbidden", result.Message),
            "InvalidOperation" => CreateProblemResult(StatusCodes.Status422UnprocessableEntity, "Invalid Operation", result.Message),
            "Conflict" => CreateProblemResult(StatusCodes.Status409Conflict, "Conflict", result.Message),
            _ => CreateProblemResult(StatusCodes.Status500InternalServerError, "Internal Server Error", result.Message),
        };
    }

    private static bool IsServiceUnavailable(Exception ex)
        => ex is HttpRequestException or TimeoutException
            || (ex is Grpc.Core.RpcException rpc && rpc.StatusCode is
                Grpc.Core.StatusCode.Unavailable or
                Grpc.Core.StatusCode.DeadlineExceeded or
                Grpc.Core.StatusCode.Aborted or
                Grpc.Core.StatusCode.ResourceExhausted);

    private ObjectResult ServiceUnavailable(string method, Exception ex) {
        logger.LogError(ex, "Admin service unavailable: {Method}", method);
        return CreateProblemResult(
            StatusCodes.Status503ServiceUnavailable,
            "Service Unavailable",
            "The admin backend service is temporarily unavailable. Retry shortly.");
    }

    private ObjectResult UnexpectedError(string method, Exception ex) {
        logger.LogError(ex, "Unexpected error in {Method}", method);
        return CreateProblemResult(
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            "An unexpected error occurred.");
    }

    private ObjectResult CreateProblemResult(int statusCode, string title, string? detail = null) {
        string correlationId = HttpContext.Items["CorrelationId"]?.ToString()
            ?? Guid.NewGuid().ToString();
        return new ObjectResult(new ProblemDetails {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Extensions = { ["correlationId"] = correlationId },
        }) { StatusCode = statusCode };
    }
}
