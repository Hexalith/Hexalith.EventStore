using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Tenants;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Models;

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
    ILogger<AdminTenantsController> logger) : ControllerBase
{
    // ---- Read endpoints (ReadOnly policy) ----

    /// <summary>
    /// Lists all tenants.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ProducesResponseType(typeof(IReadOnlyList<TenantSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListTenants(CancellationToken ct = default)
    {
        try
        {
            IReadOnlyList<TenantSummary> result = await tenantQueryService
                .ListTenantsAsync(ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(ListTenants), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(ListTenants), ex);
        }
    }

    /// <summary>
    /// Gets detailed tenant information including quotas.
    /// </summary>
    [HttpGet("{tenantId}")]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ProducesResponseType(typeof(TenantDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetTenantDetail(
        string tenantId,
        CancellationToken ct = default)
    {
        try
        {
            TenantDetail? result = await tenantQueryService
                .GetTenantDetailAsync(tenantId, ct)
                .ConfigureAwait(false);
            return result is null
                ? CreateProblemResult(StatusCodes.Status404NotFound, "Not Found", $"Tenant '{tenantId}' not found.")
                : Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(GetTenantDetail), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(GetTenantDetail), ex);
        }
    }

    /// <summary>
    /// Gets the quota information for a specific tenant.
    /// </summary>
    [HttpGet("{tenantId}/quotas")]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ProducesResponseType(typeof(TenantQuotas), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetTenantQuotas(
        string tenantId,
        CancellationToken ct = default)
    {
        try
        {
            TenantQuotas result = await tenantQueryService
                .GetTenantQuotasAsync(tenantId, ct)
                .ConfigureAwait(false);
            return result is null
                ? CreateProblemResult(StatusCodes.Status404NotFound, "Not Found", $"Tenant '{tenantId}' not found.")
                : Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(GetTenantQuotas), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(GetTenantQuotas), ex);
        }
    }

    /// <summary>
    /// Gets users assigned to a tenant.
    /// </summary>
    [HttpGet("{tenantId}/users")]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ProducesResponseType(typeof(IReadOnlyList<TenantUser>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetTenantUsers(
        string tenantId,
        CancellationToken ct = default)
    {
        try
        {
            IReadOnlyList<TenantUser> result = await tenantQueryService
                .GetTenantUsersAsync(tenantId, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(GetTenantUsers), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(GetTenantUsers), ex);
        }
    }

    /// <summary>
    /// Compares usage across multiple tenants.
    /// </summary>
    [HttpPost("compare")]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ProducesResponseType(typeof(TenantComparison), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CompareTenantUsage(
        [FromBody] TenantCompareRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            TenantComparison result = await tenantQueryService
                .CompareTenantUsageAsync(request.TenantIds, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(CompareTenantUsage), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(CompareTenantUsage), ex);
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
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            AdminOperationResult result = await tenantCommandService
                .CreateTenantAsync(request, ct)
                .ConfigureAwait(false);
            return result.Success
                ? Accepted(result)
                : CreateProblemResult(StatusCodes.Status422UnprocessableEntity, "Operation Failed", result.Message);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(CreateTenant), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(CreateTenant), ex);
        }
    }

    /// <summary>
    /// Disables (suspends) an active tenant.
    /// </summary>
    [HttpPost("{tenantId}/disable")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DisableTenant(
        string tenantId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        try
        {
            AdminOperationResult result = await tenantCommandService
                .DisableTenantAsync(tenantId, ct)
                .ConfigureAwait(false);
            return result.Success
                ? Accepted(result)
                : CreateProblemResult(StatusCodes.Status422UnprocessableEntity, "Operation Failed", result.Message);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(DisableTenant), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(DisableTenant), ex);
        }
    }

    /// <summary>
    /// Enables a suspended tenant.
    /// </summary>
    [HttpPost("{tenantId}/enable")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> EnableTenant(
        string tenantId,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        try
        {
            AdminOperationResult result = await tenantCommandService
                .EnableTenantAsync(tenantId, ct)
                .ConfigureAwait(false);
            return result.Success
                ? Accepted(result)
                : CreateProblemResult(StatusCodes.Status422UnprocessableEntity, "Operation Failed", result.Message);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(EnableTenant), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
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
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            AdminOperationResult result = await tenantCommandService
                .AddUserToTenantAsync(tenantId, request.Email, request.Role, ct)
                .ConfigureAwait(false);
            return result.Success
                ? Accepted(result)
                : CreateProblemResult(StatusCodes.Status422UnprocessableEntity, "Operation Failed", result.Message);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(AddUserToTenant), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
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
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            AdminOperationResult result = await tenantCommandService
                .RemoveUserFromTenantAsync(tenantId, request.Email, ct)
                .ConfigureAwait(false);
            return result.Success
                ? Accepted(result)
                : CreateProblemResult(StatusCodes.Status422UnprocessableEntity, "Operation Failed", result.Message);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(RemoveUserFromTenant), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
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
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            AdminOperationResult result = await tenantCommandService
                .ChangeUserRoleAsync(tenantId, request.Email, request.NewRole, ct)
                .ConfigureAwait(false);
            return result.Success
                ? Accepted(result)
                : CreateProblemResult(StatusCodes.Status422UnprocessableEntity, "Operation Failed", result.Message);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(ChangeUserRole), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(ChangeUserRole), ex);
        }
    }

    // ---- Shared helpers ----

    private static bool IsServiceUnavailable(Exception ex)
        => ex is HttpRequestException or TimeoutException
            || (ex is Grpc.Core.RpcException rpc && rpc.StatusCode is
                Grpc.Core.StatusCode.Unavailable or
                Grpc.Core.StatusCode.DeadlineExceeded or
                Grpc.Core.StatusCode.Aborted or
                Grpc.Core.StatusCode.ResourceExhausted);

    private ObjectResult ServiceUnavailable(string method, Exception ex)
    {
        logger.LogError(ex, "Admin service unavailable: {Method}", method);
        return CreateProblemResult(
            StatusCodes.Status503ServiceUnavailable,
            "Service Unavailable",
            "The admin backend service is temporarily unavailable. Retry shortly.");
    }

    private ObjectResult UnexpectedError(string method, Exception ex)
    {
        logger.LogError(ex, "Unexpected error in {Method}", method);
        return CreateProblemResult(
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            "An unexpected error occurred.");
    }

    private ObjectResult CreateProblemResult(int statusCode, string title, string? detail = null)
    {
        string correlationId = HttpContext.Items["CorrelationId"]?.ToString()
            ?? Guid.NewGuid().ToString();
        return new ObjectResult(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Extensions = { ["correlationId"] = correlationId },
        })
        { StatusCode = statusCode };
    }
}
