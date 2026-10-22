using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.Server.Controllers;

/// <summary>
/// REST API controller for correlation ID trace map queries.
/// Operates on a different URL namespace (/traces/{tenantId}/{correlationId}) because
/// the trace map takes a correlation ID as primary identifier, not a stream identity.
/// </summary>
[ApiController]
[Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
[Route("api/v1/admin/traces")]
[Tags("Admin - Traces")]
public class AdminTracesController(
    IStreamQueryService streamQueryService,
    ILogger<AdminTracesController> logger) : ControllerBase {
    /// <summary>
    /// Gets the correlation trace map for a given correlation ID, showing the complete command lifecycle.
    /// </summary>
    [HttpGet("{tenantId}/{correlationId}")]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(CorrelationTraceMap), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetCorrelationTraceMap(
        string tenantId,
        string correlationId,
        [FromQuery] string? domain,
        [FromQuery] string? aggregateId,
        CancellationToken ct = default) {
        if (string.IsNullOrWhiteSpace(correlationId)) {
            return CreateProblemResult(
                StatusCodes.Status400BadRequest,
                "Bad Request",
                "correlationId is required.");
        }

        try {
            CorrelationTraceMap result = await streamQueryService
                .GetCorrelationTraceMapAsync(tenantId, correlationId, domain, aggregateId, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(GetCorrelationTraceMap), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(GetCorrelationTraceMap), ex);
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
