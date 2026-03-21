using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Projections;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.Server.Controllers;

/// <summary>
/// REST API controller for querying and managing projections.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/admin/projections")]
[Tags("Admin - Projections")]
public class AdminProjectionsController(
    IProjectionQueryService projectionQueryService,
    IProjectionCommandService projectionCommandService,
    ILogger<AdminProjectionsController> logger) : ControllerBase
{
    /// <summary>
    /// Lists all projections, optionally filtered by tenant.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectionStatus>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListProjections(
        [FromQuery] string? tenantId,
        CancellationToken ct = default)
    {
        try
        {
            string? effectiveTenantId = ResolveTenantScope(tenantId);
            IReadOnlyList<ProjectionStatus> result = await projectionQueryService
                .ListProjectionsAsync(effectiveTenantId, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(ListProjections), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(ListProjections), ex);
        }
    }

    /// <summary>
    /// Gets detailed information about a specific projection.
    /// </summary>
    [HttpGet("{tenantId}/{projectionName}")]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(ProjectionDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetProjectionDetail(
        string tenantId,
        string projectionName,
        CancellationToken ct = default)
    {
        try
        {
            ProjectionDetail result = await projectionQueryService
                .GetProjectionDetailAsync(tenantId, projectionName, ct)
                .ConfigureAwait(false);
            return result is null
                ? CreateProblemResult(StatusCodes.Status404NotFound, "Not Found", $"Projection '{projectionName}' not found.")
                : Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(GetProjectionDetail), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(GetProjectionDetail), ex);
        }
    }

    /// <summary>
    /// Pauses a running projection.
    /// </summary>
    [HttpPost("{tenantId}/{projectionName}/pause")]
    [Authorize(Policy = AdminAuthorizationPolicies.Operator)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> PauseProjection(
        string tenantId,
        string projectionName,
        CancellationToken ct = default)
    {
        try
        {
            AdminOperationResult result = await projectionCommandService
                .PauseProjectionAsync(tenantId, projectionName, ct)
                .ConfigureAwait(false);
            return MapOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(PauseProjection), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(PauseProjection), ex);
        }
    }

    /// <summary>
    /// Resumes a paused projection.
    /// </summary>
    [HttpPost("{tenantId}/{projectionName}/resume")]
    [Authorize(Policy = AdminAuthorizationPolicies.Operator)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ResumeProjection(
        string tenantId,
        string projectionName,
        CancellationToken ct = default)
    {
        try
        {
            AdminOperationResult result = await projectionCommandService
                .ResumeProjectionAsync(tenantId, projectionName, ct)
                .ConfigureAwait(false);
            return MapOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(ResumeProjection), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(ResumeProjection), ex);
        }
    }

    /// <summary>
    /// Resets a projection, optionally from a specific position.
    /// </summary>
    [HttpPost("{tenantId}/{projectionName}/reset")]
    [Authorize(Policy = AdminAuthorizationPolicies.Operator)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ResetProjection(
        string tenantId,
        string projectionName,
        [FromBody] ProjectionResetRequest? request,
        CancellationToken ct = default)
    {
        try
        {
            AdminOperationResult result = await projectionCommandService
                .ResetProjectionAsync(tenantId, projectionName, request?.FromPosition, ct)
                .ConfigureAwait(false);
            return MapAsyncOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(ResetProjection), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(ResetProjection), ex);
        }
    }

    /// <summary>
    /// Replays a projection between two positions.
    /// </summary>
    [HttpPost("{tenantId}/{projectionName}/replay")]
    [Authorize(Policy = AdminAuthorizationPolicies.Operator)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ReplayProjection(
        string tenantId,
        string projectionName,
        [FromBody] ProjectionReplayRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            AdminOperationResult result = await projectionCommandService
                .ReplayProjectionAsync(tenantId, projectionName, request.FromPosition, request.ToPosition, ct)
                .ConfigureAwait(false);
            return MapAsyncOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(ReplayProjection), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(ReplayProjection), ex);
        }
    }

    private string? ResolveTenantScope(string? requestedTenantId)
    {
        if (requestedTenantId is not null)
        {
            return requestedTenantId;
        }

        if (User.HasClaim(AdminClaimTypes.AdminRole, nameof(Abstractions.Models.Common.AdminRole.Admin)))
        {
            return null;
        }

        return User.FindFirst(AdminClaimTypes.Tenant)?.Value;
    }

    private IActionResult MapOperationResult(AdminOperationResult? result)
    {
        if (result is null)
        {
            return CreateProblemResult(StatusCodes.Status500InternalServerError, "Internal Server Error", "No result returned from the service.");
        }

        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "NotFound" => CreateProblemResult(StatusCodes.Status404NotFound, "Not Found", result.Message),
            "Unauthorized" => CreateProblemResult(StatusCodes.Status403Forbidden, "Forbidden", result.Message),
            "InvalidOperation" => CreateProblemResult(StatusCodes.Status422UnprocessableEntity, "Invalid Operation", result.Message),
            _ => CreateProblemResult(StatusCodes.Status500InternalServerError, "Internal Server Error", result.Message),
        };
    }

    private IActionResult MapAsyncOperationResult(AdminOperationResult? result)
    {
        if (result is null)
        {
            return CreateProblemResult(StatusCodes.Status500InternalServerError, "Internal Server Error", "No result returned from the service.");
        }

        if (result.Success)
        {
            return Accepted(result);
        }

        return result.ErrorCode switch
        {
            "NotFound" => CreateProblemResult(StatusCodes.Status404NotFound, "Not Found", result.Message),
            "Unauthorized" => CreateProblemResult(StatusCodes.Status403Forbidden, "Forbidden", result.Message),
            "InvalidOperation" => CreateProblemResult(StatusCodes.Status422UnprocessableEntity, "Invalid Operation", result.Message),
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
