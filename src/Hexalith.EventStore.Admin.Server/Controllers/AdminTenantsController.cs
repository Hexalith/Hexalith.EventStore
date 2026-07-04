using System.Net;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.Server.Controllers;

/// <summary>
/// REST API controller for tenant management operations.
/// Read endpoints require ReadOnly policy. Write endpoints require Admin policy.
/// </summary>
[ApiController]
[Route("api/v1/admin/tenants")]
[Tags("Admin - Tenants")]
public class AdminTenantsController(
    ITenantQueryService tenantQueryService,
    ITenantCommandService tenantCommandService,
    ILogger<AdminTenantsController> logger) : ControllerBase {
    // ---- Read endpoints (ReadOnly policy) ----

    /// <summary>
    /// Adds a user to a tenant.
    /// </summary>
    [HttpPost("{tenantId}/users")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> AddUserToTenantAsync(
        string tenantId,
        [FromBody] AddTenantUserRequest request,
        CancellationToken ct = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);

        try {
            AdminOperationResult result = await tenantCommandService
                .AddUserToTenantAsync(tenantId, request.UserId, request.Role, ct)
                .ConfigureAwait(false);
            return result.Success
                ? Accepted(result)
                : CommandFailureResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(AddUserToTenantAsync), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(AddUserToTenantAsync), ex);
        }
    }

    /// <summary>
    /// Changes a user's role within a tenant.
    /// </summary>
    [HttpPost("{tenantId}/change-role")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ChangeUserRoleAsync(
        string tenantId,
        [FromBody] ChangeTenantUserRoleRequest request,
        CancellationToken ct = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);

        try {
            AdminOperationResult result = await tenantCommandService
                .ChangeUserRoleAsync(tenantId, request.UserId, request.NewRole, ct)
                .ConfigureAwait(false);
            return result.Success
                ? Accepted(result)
                : CommandFailureResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(ChangeUserRoleAsync), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(ChangeUserRoleAsync), ex);
        }
    }

    /// <summary>
    /// Creates a new tenant.
    /// </summary>
    [HttpPost("CreateTenant")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateTenantAsync(
        [FromBody] CreateTenantRequest request,
        CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(request);

        try {
            AdminOperationResult result = await tenantCommandService
                .CreateTenantAsync(request, ct)
                .ConfigureAwait(false);
            return result.Success
                ? Accepted(result)
                : CommandFailureResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(CreateTenantAsync), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(CreateTenantAsync), ex);
        }
    }

    // ---- Write endpoints (Admin policy) ----
    /// <summary>
    /// Disables an active tenant.
    /// </summary>
    [HttpPost("{tenantId}/disable")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DisableTenantAsync(
        string tenantId,
        CancellationToken ct = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        try {
            AdminOperationResult result = await tenantCommandService
                .DisableTenantAsync(tenantId, ct)
                .ConfigureAwait(false);
            return result.Success
                ? Accepted(result)
                : CommandFailureResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(DisableTenantAsync), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(DisableTenantAsync), ex);
        }
    }

    /// <summary>
    /// Enables a disabled tenant.
    /// </summary>
    [HttpPost("{tenantId}/enable")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> EnableTenantAsync(
        string tenantId,
        CancellationToken ct = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        try {
            AdminOperationResult result = await tenantCommandService
                .EnableTenantAsync(tenantId, ct)
                .ConfigureAwait(false);
            return result.Success
                ? Accepted(result)
                : CommandFailureResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(EnableTenantAsync), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(EnableTenantAsync), ex);
        }
    }

    /// <summary>
    /// Gets detailed tenant information.
    /// </summary>
    [HttpGet("{tenantId}")]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ProducesResponseType(typeof(TenantDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetTenantDetailAsync(
        string tenantId,
        CancellationToken ct = default) {
        try {
            TenantDetail? result = await tenantQueryService
                .GetTenantDetailAsync(tenantId, ct)
                .ConfigureAwait(false);
            return result is null
                ? CreateProblemResult(StatusCodes.Status404NotFound, "Not Found", $"Tenant '{tenantId}' not found.")
                : Ok(result);
        }
        catch (HttpRequestException ex) {
            return QueryFailure(nameof(GetTenantDetailAsync), ex, tenantId);
        }
        catch (TenantQueryFailedException ex) {
            return UpstreamQueryFailure(nameof(GetTenantDetailAsync), ex);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(GetTenantDetailAsync), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(GetTenantDetailAsync), ex);
        }
    }

    /// <summary>
    /// Gets users assigned to a tenant.
    /// </summary>
    [HttpGet("{tenantId}/users")]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ProducesResponseType(typeof(IReadOnlyList<TenantUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetTenantUsersAsync(
        string tenantId,
        CancellationToken ct = default) {
        try {
            IReadOnlyList<TenantUser> result = await tenantQueryService
                .GetTenantUsersAsync(tenantId, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (HttpRequestException ex) {
            return QueryFailure(nameof(GetTenantUsersAsync), ex, tenantId);
        }
        catch (TenantQueryFailedException ex) {
            return UpstreamQueryFailure(nameof(GetTenantUsersAsync), ex);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(GetTenantUsersAsync), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(GetTenantUsersAsync), ex);
        }
    }

    /// <summary>
    /// Lists all tenants.
    /// </summary>
    // Restricted to Admin: this endpoint has no route tenantId to scope on, so a tenant-scoped
    // ReadOnly/Operator caller must not be able to enumerate every tenant in the platform.
    [HttpGet("ListTenants")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(IReadOnlyList<TenantSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListTenantsAsync(CancellationToken ct = default) {
        try {
            IReadOnlyList<TenantSummary> result = await tenantQueryService
                .ListTenantsAsync(ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (HttpRequestException ex) {
            return QueryFailure(nameof(ListTenantsAsync), ex);
        }
        catch (TenantQueryFailedException ex) {
            return UpstreamQueryFailure(nameof(ListTenantsAsync), ex);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(ListTenantsAsync), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(ListTenantsAsync), ex);
        }
    }

    /// <summary>
    /// Removes a user from a tenant.
    /// </summary>
    [HttpPost("{tenantId}/remove-user")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RemoveUserFromTenantAsync(
        string tenantId,
        [FromBody] RemoveTenantUserRequest request,
        CancellationToken ct = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);

        try {
            AdminOperationResult result = await tenantCommandService
                .RemoveUserFromTenantAsync(tenantId, request.UserId, ct)
                .ConfigureAwait(false);
            return result.Success
                ? Accepted(result)
                : CommandFailureResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(RemoveUserFromTenantAsync), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(RemoveUserFromTenantAsync), ex);
        }
    }

    // ---- Shared helpers ----

    private static int GetCommandFailureStatus(string? errorCode)
        => errorCode?.Trim().ToLowerInvariant() switch {
            "invalid-request" or "400" => StatusCodes.Status400BadRequest,
            "timeout" or "408" or "504" => StatusCodes.Status504GatewayTimeout,
            "unavailable" or "401" or "403" or "404" or "429" or "502" or "503" => StatusCodes.Status503ServiceUnavailable,
            "unexpected" or "unexpected_status" or "unexpected-status" or "500" => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status422UnprocessableEntity,
        };

    private static bool IsServiceUnavailable(Exception ex)
            => ex is TimeoutException
            || (ex is HttpRequestException httpRequestException
            && (httpRequestException.StatusCode is null
                or HttpStatusCode.RequestTimeout
                or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.GatewayTimeout))
            || (ex is Grpc.Core.RpcException rpc && rpc.StatusCode is
                Grpc.Core.StatusCode.Unavailable or
                Grpc.Core.StatusCode.DeadlineExceeded or
                Grpc.Core.StatusCode.Aborted or
                Grpc.Core.StatusCode.ResourceExhausted);

    private ObjectResult CommandFailureResult(AdminOperationResult result) {
        int statusCode = GetCommandFailureStatus(result.ErrorCode);
        string title = statusCode switch {
            StatusCodes.Status400BadRequest => "Bad Request",
            StatusCodes.Status503ServiceUnavailable => "Service Unavailable",
            StatusCodes.Status504GatewayTimeout => "Operation Timed Out",
            StatusCodes.Status500InternalServerError => "Internal Server Error",
            _ => "Operation Failed",
        };

        return CreateProblemResult(
            statusCode,
            title,
            result.Message ?? "The tenant operation failed.",
            result.OperationId,
            result.ErrorCode);
    }

    private ObjectResult CreateProblemResult(int statusCode, string title, string? detail = null) => CreateProblemResult(statusCode, title, detail, operationId: null, errorCode: null);

    private ObjectResult CreateProblemResult(
        int statusCode,
        string title,
        string? detail,
        string? operationId,
        string? errorCode) {
        string correlationId = HttpContext.Items["CorrelationId"]?.ToString()
            ?? Guid.NewGuid().ToString();
        var problem = new ProblemDetails {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Extensions = { ["correlationId"] = correlationId },
        };

        if (!string.IsNullOrWhiteSpace(operationId)) {
            problem.Extensions["operationId"] = operationId;
        }

        if (!string.IsNullOrWhiteSpace(errorCode)) {
            problem.Extensions["errorCode"] = errorCode;
        }

        return new ObjectResult(problem) { StatusCode = statusCode };
    }

    private IActionResult QueryFailure(string method, HttpRequestException exception, string? tenantId = null) {
        if (IsServiceUnavailable(exception)) {
            return ServiceUnavailable(method, exception);
        }

        return exception.StatusCode switch {
            HttpStatusCode.BadRequest => CreateProblemResult(
                StatusCodes.Status400BadRequest,
                "Bad Request",
                "The tenant query was rejected by the EventStore query pipeline."),
            HttpStatusCode.Unauthorized => CreateProblemResult(
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "Authentication required. Please sign in again."),
            HttpStatusCode.Forbidden => CreateProblemResult(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "The authenticated user is not authorized to access the requested tenant data."),
            HttpStatusCode.NotFound => CreateProblemResult(
                StatusCodes.Status404NotFound,
                "Not Found",
                string.IsNullOrWhiteSpace(tenantId)
                    ? "The requested tenant data was not found."
                    : $"Tenant '{tenantId}' not found."),
            HttpStatusCode.TooManyRequests => CreateProblemResult(
                StatusCodes.Status429TooManyRequests,
                "Too Many Requests",
                "The EventStore query pipeline is rate limiting tenant queries. Retry shortly."),
            _ => UnexpectedError(method, exception),
        };
    }

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

    private ObjectResult UpstreamQueryFailure(string method, TenantQueryFailedException ex) {
        logger.LogError(ex, "Tenant query upstream contract failure: {Method}", method);
        return CreateProblemResult(
            StatusCodes.Status502BadGateway,
            "Bad Gateway",
            "The tenant query pipeline returned a failure that the admin server cannot classify.");
    }
}
