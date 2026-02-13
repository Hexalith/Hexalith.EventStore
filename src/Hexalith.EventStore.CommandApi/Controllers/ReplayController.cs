namespace Hexalith.EventStore.CommandApi.Controllers;

using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.CommandApi.Models;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Controller for replaying previously failed commands.
/// Route: POST /api/v1/commands/replay/{correlationId}
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/commands/replay")]
public class ReplayController(
    ICommandArchiveStore archiveStore,
    ICommandStatusStore statusStore,
    IMediator mediator,
    ILogger<ReplayController> logger) : ControllerBase
{
    private static readonly HashSet<CommandStatus> _replayableStatuses =
    [
        CommandStatus.Rejected,
        CommandStatus.PublishFailed,
        CommandStatus.TimedOut,
    ];

    /// <summary>
    /// Replays a previously failed command by correlation ID.
    /// </summary>
    [HttpPost("{correlationId}")]
    [ProducesResponseType(typeof(ReplayCommandResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> Replay(string correlationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(correlationId);

        string requestCorrelationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? correlationId;

        // Store correlationId in HttpContext.Items for error handler access
        HttpContext.Items["ReplayCorrelationId"] = correlationId;

        // Extract tenant claims (same pattern as Story 2.5/2.6)
        List<string> tenantClaims = User.FindAll("eventstore:tenant")
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        // AC #6: 403 for missing tenant claims
        if (tenantClaims.Count == 0)
        {
            logger.LogWarning(
                "Replay denied: no tenant claims. CorrelationId={CorrelationId}",
                requestCorrelationId);

            return CreateProblemDetails(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "No tenant authorization claims found. Access denied.",
                requestCorrelationId);
        }

        // AC #5: Search for archived command across authorized tenants (SEC-3)
        ArchivedCommand? archivedCommand = null;
        string? foundTenant = null;

        foreach (string tenant in tenantClaims)
        {
            archivedCommand = await archiveStore
                .ReadCommandAsync(tenant, correlationId, cancellationToken)
                .ConfigureAwait(false);

            if (archivedCommand is not null)
            {
                foundTenant = tenant;
                break;
            }
        }

        // AC #4: 404 for non-existent or expired correlation ID
        if (archivedCommand is null || foundTenant is null)
        {
            logger.LogDebug(
                "Archived command not found: CorrelationId={CorrelationId}, TenantsSearched={Tenants}",
                correlationId,
                string.Join(",", tenantClaims));

            return CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Not Found",
                $"No command found for correlation ID '{correlationId}'.",
                requestCorrelationId);
        }

        // Store tenantId in HttpContext.Items for error handler access
        HttpContext.Items["RequestTenantId"] = foundTenant;

        // AC #3: Read current status to validate replayability
        CommandStatusRecord? statusRecord = await statusStore
            .ReadStatusAsync(foundTenant, correlationId, cancellationToken)
            .ConfigureAwait(false);

        // H5: Null status (expired or never written) -- cannot determine replayability
        if (statusRecord is null)
        {
            return CreateConflictProblemDetails(
                "Unknown",
                $"Status tracking for command '{correlationId}' has expired. Cannot determine replayability. Replay is permitted only for commands with terminal failure status (Rejected, PublishFailed, TimedOut).",
                requestCorrelationId);
        }

        // Check if status is replayable
        if (!_replayableStatuses.Contains(statusRecord.Status))
        {
            string detail = statusRecord.Status == CommandStatus.Completed
                ? $"Command '{correlationId}' has already completed successfully. Replay is not permitted for completed commands. Replay is permitted only for commands with terminal failure status (Rejected, PublishFailed, TimedOut)."
                : $"Command '{correlationId}' is currently in-flight (status: {statusRecord.Status}). Wait for processing to complete or time out before replaying. Replay is permitted only for commands with terminal failure status (Rejected, PublishFailed, TimedOut).";

            return CreateConflictProblemDetails(
                statusRecord.Status.ToString(),
                detail,
                requestCorrelationId);
        }

        string previousStatus = statusRecord.Status.ToString();

        // AC #1, #2: Create SubmitCommand from archived data and send through full MediatR pipeline
        SubmitCommand command = archivedCommand.ToSubmitCommand(correlationId);

        logger.LogInformation(
            "Replay initiated: CorrelationId={CorrelationId}, TenantId={TenantId}, PreviousStatus={PreviousStatus}, IsReplay={IsReplay}",
            correlationId,
            foundTenant,
            previousStatus,
            true);

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        // AC #1: Return 202 Accepted with replay response
        string absoluteLocationUri = $"{Request.Scheme}://{Request.Host}/api/v1/commands/status/{correlationId}";
        Response.Headers["Retry-After"] = "1";

        return Accepted(absoluteLocationUri, new ReplayCommandResponse(correlationId, IsReplay: true, PreviousStatus: previousStatus));
    }

    private ObjectResult CreateProblemDetails(int statusCode, string title, string detail, string correlationId)
    {
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = "https://tools.ietf.org/html/rfc9457#section-3",
            Detail = detail,
            Instance = HttpContext?.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
            },
        };

        if (HttpContext is not null)
        {
            Response.ContentType = "application/problem+json";
        }

        return new ObjectResult(problemDetails) { StatusCode = statusCode };
    }

    private ObjectResult CreateConflictProblemDetails(string currentStatus, string detail, string requestCorrelationId)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflict",
            Type = "https://tools.ietf.org/html/rfc9457#section-3",
            Detail = detail,
            Instance = HttpContext?.Request.Path,
            Extensions =
            {
                ["correlationId"] = requestCorrelationId,
                ["currentStatus"] = currentStatus,
            },
        };

        if (HttpContext is not null)
        {
            Response.ContentType = "application/problem+json";
        }

        return new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status409Conflict };
    }
}
