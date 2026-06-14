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
    /// Lists all tenants.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ProducesResponseType(typeof(IReadOnlyList<TenantSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListTenants(CancellationToken ct = default) {
        try {
            IReadOnlyList<TenantSummary> result = await tenantQueryService
                .ListTenantsAsync(ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (HttpRequestException ex) {
            return QueryFailure(nameof(ListTenants), ex);
        }
        catch (TenantQueryFailedException ex) {
            return UpstreamQueryFailure(nameof(ListTenants), ex);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(ListTenants), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(ListTenants), ex);
        }
    }

    /// <summary>
    /// Gets detailed tenant information.
    /// </summary>
    [HttpGet("{tenantId}")]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ProducesResponseType(typeof(TenantDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetTenantDetail(
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
            return QueryFailure(nameof(GetTenantDetail), ex, tenantId);
        }
        catch (TenantQueryFailedException ex) {
            return UpstreamQueryFailure(nameof(GetTenantDetail), ex);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(GetTenantDetail), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(GetTenantDetail), ex);
        }
    }

    /// <summary>
    /// Gets users assigned to a tenant.
    /// </summary>
    [HttpGet("{tenantId}/users")]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ProducesResponseType(typeof(IReadOnlyList<TenantUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetTenantUsers(
        string tenantId,
        CancellationToken ct = default) {
        try {
            IReadOnlyList<TenantUser> result = await tenantQueryService
                .GetTenantUsersAsync(tenantId, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (HttpRequestException ex) {
            return QueryFailure(nameof(GetTenantUsers), ex, tenantId);
        }
        catch (TenantQueryFailedException ex) {
            return UpstreamQueryFailure(nameof(GetTenantUsers), ex);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(GetTenantUsers), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(GetTenantUsers), ex);
        }
    }

    // ---- Write endpoints (Admin policy) ----

    /// <summary>
    /// Creates a new tenant.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateTenant(
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
            return ServiceUnavailable(nameof(CreateTenant), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(CreateTenant), ex);
        }
    }

    /// <summary>
    /// Disables an active tenant.
    /// </summary>
    [HttpPost("{tenantId}/disable")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DisableTenant(
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
            return ServiceUnavailable(nameof(DisableTenant), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(DisableTenant), ex);
        }
    }

    /// <summary>
    /// Enables a disabled tenant.
    /// </summary>
    [HttpPost("{tenantId}/enable")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> EnableTenant(
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
            return ServiceUnavailable(nameof(EnableTenant), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(EnableTenant), ex);
        }
    }

    /// <summary>
    /// Adds a user to a tenant.
    /// </summary>
    [HttpPost("{tenantId}/users")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> AddUserToTenant(
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
            return ServiceUnavailable(nameof(AddUserToTenant), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(AddUserToTenant), ex);
        }
    }

    /// <summary>
    /// Removes a user from a tenant.
    /// </summary>
    [HttpPost("{tenantId}/remove-user")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RemoveUserFromTenant(
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
            return ServiceUnavailable(nameof(RemoveUserFromTenant), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(RemoveUserFromTenant), ex);
        }
    }

    /// <summary>
    /// Changes a user's role within a tenant.
    /// </summary>
    [HttpPost("{tenantId}/change-role")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ChangeUserRole(
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
            return ServiceUnavailable(nameof(ChangeUserRole), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(ChangeUserRole), ex);
        }
    }

    // ---- Shared helpers ----

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

    private ObjectResult UpstreamQueryFailure(string method, TenantQueryFailedException ex) {
        logger.LogError(ex, "Tenant query upstream contract failure: {Method}", method);
        return CreateProblemResult(
            StatusCodes.Status502BadGateway,
            "Bad Gateway",
            "The tenant query pipeline returned a failure that the admin server cannot classify.");
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

    private static int GetCommandFailureStatus(string? errorCode)
        => errorCode?.Trim().ToLowerInvariant() switch {
            "invalid-request" or "400" => StatusCodes.Status400BadRequest,
            "timeout" or "408" or "504" => StatusCodes.Status504GatewayTimeout,
            "unavailable" or "401" or "403" or "404" or "429" or "502" or "503" => StatusCodes.Status503ServiceUnavailable,
            "unexpected" or "unexpected_status" or "unexpected-status" or "500" => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status422UnprocessableEntity,
        };

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
}
