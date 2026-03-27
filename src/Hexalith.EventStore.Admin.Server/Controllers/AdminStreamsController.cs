using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.Server.Controllers;

/// <summary>
/// REST API controller for browsing and inspecting event streams.
/// </summary>
[ApiController]
[Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
[Route("api/v1/admin/streams")]
[Tags("Admin - Streams")]
public class AdminStreamsController(
    IStreamQueryService streamQueryService,
    ILogger<AdminStreamsController> logger) : ControllerBase
{
    /// <summary>
    /// Gets recently active streams, optionally filtered by tenant and domain.
    /// </summary>
    [HttpGet]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(PagedResult<StreamSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetRecentlyActiveStreams(
        [FromQuery] string? tenantId,
        [FromQuery] string? domain,
        [FromQuery] int count = 1000,
        CancellationToken ct = default)
    {
        try
        {
            string? effectiveTenantId = ResolveTenantScope(tenantId);
            PagedResult<StreamSummary> result = await streamQueryService
                .GetRecentlyActiveStreamsAsync(effectiveTenantId, domain, count, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(GetRecentlyActiveStreams), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(GetRecentlyActiveStreams), ex);
        }
    }

    /// <summary>
    /// Gets the timeline of commands, events, and queries for a specific stream.
    /// </summary>
    [HttpGet("{tenantId}/{domain}/{aggregateId}/timeline")]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(PagedResult<TimelineEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetStreamTimeline(
        string tenantId,
        string domain,
        string aggregateId,
        [FromQuery] long? fromSequence,
        [FromQuery] long? toSequence,
        [FromQuery] int count = 100,
        CancellationToken ct = default)
    {
        try
        {
            PagedResult<TimelineEntry> result = await streamQueryService
                .GetStreamTimelineAsync(tenantId, domain, aggregateId, fromSequence, toSequence, count, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(GetStreamTimeline), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(GetStreamTimeline), ex);
        }
    }

    /// <summary>
    /// Gets the aggregate state at a specific sequence position.
    /// </summary>
    [HttpGet("{tenantId}/{domain}/{aggregateId}/state")]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AggregateStateSnapshot), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetAggregateState(
        string tenantId,
        string domain,
        string aggregateId,
        [FromQuery] long sequenceNumber,
        CancellationToken ct = default)
    {
        try
        {
            AggregateStateSnapshot result = await streamQueryService
                .GetAggregateStateAtPositionAsync(tenantId, domain, aggregateId, sequenceNumber, ct)
                .ConfigureAwait(false);
            return result is null
                ? CreateProblemResult(StatusCodes.Status404NotFound, "Not Found", "Aggregate state not found at the specified position.")
                : Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(GetAggregateState), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(GetAggregateState), ex);
        }
    }

    /// <summary>
    /// Diffs aggregate state between two sequence positions.
    /// </summary>
    [HttpGet("{tenantId}/{domain}/{aggregateId}/diff")]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AggregateStateDiff), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DiffAggregateState(
        string tenantId,
        string domain,
        string aggregateId,
        [FromQuery] long fromSequence,
        [FromQuery] long toSequence,
        CancellationToken ct = default)
    {
        try
        {
            AggregateStateDiff result = await streamQueryService
                .DiffAggregateStateAsync(tenantId, domain, aggregateId, fromSequence, toSequence, ct)
                .ConfigureAwait(false);
            return result is null
                ? CreateProblemResult(StatusCodes.Status404NotFound, "Not Found", "Aggregate state not found for the specified range.")
                : Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(DiffAggregateState), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(DiffAggregateState), ex);
        }
    }

    /// <summary>
    /// Gets per-field blame (provenance) for an aggregate's state.
    /// </summary>
    [HttpGet("{tenantId}/{domain}/{aggregateId}/blame")]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AggregateBlameView), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetAggregateBlame(
        string tenantId,
        string domain,
        string aggregateId,
        [FromQuery] long? at,
        CancellationToken ct = default)
    {
        if (at.HasValue && at.Value < 1)
        {
            return CreateProblemResult(StatusCodes.Status400BadRequest, "Bad Request", "Parameter 'at' must be >= 1 when provided.");
        }

        try
        {
            AggregateBlameView result = await streamQueryService
                .GetAggregateBlameAsync(tenantId, domain, aggregateId, at, ct)
                .ConfigureAwait(false);
            return result is null
                ? CreateProblemResult(StatusCodes.Status404NotFound, "Not Found", "Stream not found.")
                : Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(GetAggregateBlame), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(GetAggregateBlame), ex);
        }
    }

    /// <summary>
    /// Gets detailed information about a single event.
    /// </summary>
    [HttpGet("{tenantId}/{domain}/{aggregateId}/events/{sequenceNumber:long}")]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(EventDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetEventDetail(
        string tenantId,
        string domain,
        string aggregateId,
        long sequenceNumber,
        CancellationToken ct = default)
    {
        try
        {
            EventDetail result = await streamQueryService
                .GetEventDetailAsync(tenantId, domain, aggregateId, sequenceNumber, ct)
                .ConfigureAwait(false);
            return result is null
                ? CreateProblemResult(StatusCodes.Status404NotFound, "Not Found", "Event not found.")
                : Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(GetEventDetail), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(GetEventDetail), ex);
        }
    }

    /// <summary>
    /// Traces the causation chain starting from a specific event.
    /// </summary>
    [HttpGet("{tenantId}/{domain}/{aggregateId}/causation")]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(CausationChain), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> TraceCausationChain(
        string tenantId,
        string domain,
        string aggregateId,
        [FromQuery] long sequenceNumber,
        CancellationToken ct = default)
    {
        try
        {
            CausationChain result = await streamQueryService
                .TraceCausationChainAsync(tenantId, domain, aggregateId, sequenceNumber, ct)
                .ConfigureAwait(false);
            return result is null
                ? CreateProblemResult(StatusCodes.Status404NotFound, "Not Found", "Causation chain not found.")
                : Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(TraceCausationChain), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(TraceCausationChain), ex);
        }
    }

    private string? ResolveTenantScope(string? requestedTenantId)
    {
        if (requestedTenantId is not null)
        {
            return requestedTenantId;
        }

        // Admin users can see all tenants
        if (User.HasClaim(AdminClaimTypes.AdminRole, nameof(Abstractions.Models.Common.AdminRole.Admin)))
        {
            return null;
        }

        // Non-admin: scope to first authorized tenant
        return User.FindFirst(AdminClaimTypes.Tenant)?.Value;
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
