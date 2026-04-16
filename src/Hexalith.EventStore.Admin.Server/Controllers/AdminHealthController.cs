using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Models.Health;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.Server.Controllers;

/// <summary>
/// REST API controller for querying system health and DAPR component status.
/// Health is system-wide — no tenant scoping.
/// </summary>
[ApiController]
[Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
[Route("api/v1/admin/health")]
[Tags("Admin - Health")]
public class AdminHealthController(
    IHealthQueryService healthQueryService,
    ILogger<AdminHealthController> logger) : ControllerBase {
    /// <summary>
    /// Gets the overall system health report.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(SystemHealthReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetSystemHealth(CancellationToken ct = default) {
        try {
            SystemHealthReport result = await healthQueryService
                .GetSystemHealthAsync(ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(GetSystemHealth), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(GetSystemHealth), ex);
        }
    }

    /// <summary>
    /// Gets the health status of all DAPR components.
    /// </summary>
    [HttpGet("dapr")]
    [ProducesResponseType(typeof(IReadOnlyList<DaprComponentHealth>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetDaprComponentStatus(CancellationToken ct = default) {
        try {
            IReadOnlyList<DaprComponentHealth> result = await healthQueryService
                .GetDaprComponentStatusAsync(ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(GetDaprComponentStatus), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(GetDaprComponentStatus), ex);
        }
    }

    /// <summary>
    /// Gets DAPR component health history for a time range.
    /// </summary>
    [HttpGet("dapr/history")]
    [ProducesResponseType(typeof(DaprComponentHealthTimeline), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetComponentHealthHistoryAsync(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] string? component = null,
        CancellationToken ct = default) {
        DateTimeOffset effectiveFrom = from ?? DateTimeOffset.UtcNow.AddHours(-24);
        DateTimeOffset effectiveTo = to ?? DateTimeOffset.UtcNow;

        if (effectiveFrom > effectiveTo) {
            return BadRequest(new ProblemDetails {
                Title = "Invalid time range",
                Detail = "'from' must be earlier than 'to'.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        if ((effectiveTo - effectiveFrom).TotalDays > 7) {
            return BadRequest(new ProblemDetails {
                Title = "Time range too large",
                Detail = "Maximum queryable range is 7 days.",
                Status = StatusCodes.Status400BadRequest,
            });
        }

        try {
            DaprComponentHealthTimeline timeline = await healthQueryService
                .GetComponentHealthHistoryAsync(effectiveFrom, effectiveTo, component, ct)
                .ConfigureAwait(false);
            return Ok(timeline);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(GetComponentHealthHistoryAsync), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(GetComponentHealthHistoryAsync), ex);
        }
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
