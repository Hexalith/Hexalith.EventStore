using Hexalith.EventStore.Admin.Abstractions.Models.Dapr;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.Server.Controllers;

/// <summary>
/// REST API controller for querying DAPR infrastructure — components and sidecar info.
/// </summary>
[ApiController]
[Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
[Route("api/v1/admin/dapr")]
[Tags("Admin - DAPR")]
public class AdminDaprController(
    IDaprInfrastructureQueryService daprService,
    ILogger<AdminDaprController> logger) : ControllerBase
{
    /// <summary>
    /// Gets detailed information about all registered DAPR components.
    /// </summary>
    [HttpGet("components")]
    [ProducesResponseType(typeof(IReadOnlyList<DaprComponentDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetComponents(CancellationToken ct = default)
    {
        try
        {
            IReadOnlyList<DaprComponentDetail> result = await daprService
                .GetComponentsAsync(ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(GetComponents), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(GetComponents), ex);
        }
    }

    /// <summary>
    /// Gets summary information about the DAPR sidecar runtime.
    /// </summary>
    [HttpGet("sidecar")]
    [ProducesResponseType(typeof(DaprSidecarInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetSidecar(CancellationToken ct = default)
    {
        try
        {
            DaprSidecarInfo? result = await daprService
                .GetSidecarInfoAsync(ct)
                .ConfigureAwait(false);
            return result is not null ? Ok(result) : NotFound();
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(GetSidecar), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(GetSidecar), ex);
        }
    }

    /// <summary>
    /// Gets actor runtime information including registered types, active counts, and configuration.
    /// </summary>
    [HttpGet("actors")]
    [ProducesResponseType(typeof(DaprActorRuntimeInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetActorRuntimeInfoAsync(CancellationToken ct = default)
    {
        try
        {
            DaprActorRuntimeInfo result = await daprService
                .GetActorRuntimeInfoAsync(ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(GetActorRuntimeInfoAsync), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(GetActorRuntimeInfoAsync), ex);
        }
    }

    /// <summary>
    /// Gets the state of a specific actor instance.
    /// </summary>
    /// <param name="actorType">The actor type name.</param>
    /// <param name="actorId">The actor instance ID (query parameter to support colon-delimited IDs).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("actors/{actorType}/state")]
    [ProducesResponseType(typeof(DaprActorInstanceState), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetActorInstanceStateAsync(
        string actorType, [FromQuery(Name = "id")] string actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(actorType) || string.IsNullOrWhiteSpace(actorId))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = "Both actorType and id query parameter are required.",
                Instance = HttpContext.Request.Path,
            });
        }

        try
        {
            DaprActorInstanceState? result = await daprService
                .GetActorInstanceStateAsync(actorType, actorId, ct)
                .ConfigureAwait(false);

            if (result is null)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = $"Actor type '{actorType}' is not recognized or all state keys returned not-found.",
                    Instance = HttpContext.Request.Path,
                });
            }

            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(GetActorInstanceStateAsync), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(GetActorInstanceStateAsync), ex);
        }
    }

    /// <summary>
    /// Gets an overview of DAPR pub/sub infrastructure including components, subscriptions, and metadata availability.
    /// </summary>
    [HttpGet("pubsub")]
    [ProducesResponseType(typeof(DaprPubSubOverview), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetPubSubOverviewAsync(CancellationToken ct = default)
    {
        try
        {
            DaprPubSubOverview result = await daprService
                .GetPubSubOverviewAsync(ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(GetPubSubOverviewAsync), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(GetPubSubOverviewAsync), ex);
        }
    }

    /// <summary>
    /// Gets the DAPR resiliency specification including retry, timeout, and circuit breaker policies.
    /// </summary>
    [HttpGet("resiliency")]
    [ProducesResponseType(typeof(DaprResiliencySpec), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetResiliencySpecAsync(CancellationToken ct = default)
    {
        try
        {
            DaprResiliencySpec result = await daprService
                .GetResiliencySpecAsync(ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(GetResiliencySpecAsync), ex);
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
