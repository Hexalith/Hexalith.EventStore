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
/// All endpoints require Admin role.
/// </summary>
[ApiController]
[Authorize(Policy = AdminAuthorizationPolicies.Admin)]
[Route("api/v1/admin/tenants")]
[Tags("Admin - Tenants")]
public class AdminTenantsController(
    ITenantQueryService tenantQueryService,
    ILogger<AdminTenantsController> logger) : ControllerBase
{
    /// <summary>
    /// Lists all tenants.
    /// </summary>
    [HttpGet]
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
    /// Gets the quota information for a specific tenant.
    /// </summary>
    [HttpGet("{tenantId}/quotas")]
    [ProducesResponseType(typeof(TenantQuotas), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
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
    /// Compares usage across multiple tenants.
    /// </summary>
    [HttpPost("compare")]
    [ProducesResponseType(typeof(TenantComparison), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
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
