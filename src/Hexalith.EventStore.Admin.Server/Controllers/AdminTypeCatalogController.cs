using Hexalith.EventStore.Admin.Abstractions.Models.TypeCatalog;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.Server.Controllers;

/// <summary>
/// REST API controller for querying the event/command/aggregate type catalog.
/// Type catalog is tenant-agnostic — types are registered globally via reflection.
/// </summary>
[ApiController]
[Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
[Route("api/v1/admin/types")]
[Tags("Admin - Type Catalog")]
public class AdminTypeCatalogController(
    ITypeCatalogService typeCatalogService,
    ILogger<AdminTypeCatalogController> logger) : ControllerBase
{
    /// <summary>
    /// Lists all registered event types, optionally filtered by domain.
    /// </summary>
    [HttpGet("events")]
    [ProducesResponseType(typeof(IReadOnlyList<EventTypeInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListEventTypes(
        [FromQuery] string? domain,
        CancellationToken ct = default)
    {
        try
        {
            IReadOnlyList<EventTypeInfo> result = await typeCatalogService
                .ListEventTypesAsync(domain, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(ListEventTypes), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(ListEventTypes), ex);
        }
    }

    /// <summary>
    /// Lists all registered command types, optionally filtered by domain.
    /// </summary>
    [HttpGet("commands")]
    [ProducesResponseType(typeof(IReadOnlyList<CommandTypeInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListCommandTypes(
        [FromQuery] string? domain,
        CancellationToken ct = default)
    {
        try
        {
            IReadOnlyList<CommandTypeInfo> result = await typeCatalogService
                .ListCommandTypesAsync(domain, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(ListCommandTypes), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(ListCommandTypes), ex);
        }
    }

    /// <summary>
    /// Lists all registered aggregate types, optionally filtered by domain.
    /// </summary>
    [HttpGet("aggregates")]
    [ProducesResponseType(typeof(IReadOnlyList<AggregateTypeInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListAggregateTypes(
        [FromQuery] string? domain,
        CancellationToken ct = default)
    {
        try
        {
            IReadOnlyList<AggregateTypeInfo> result = await typeCatalogService
                .ListAggregateTypesAsync(domain, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(ListAggregateTypes), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(ListAggregateTypes), ex);
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
