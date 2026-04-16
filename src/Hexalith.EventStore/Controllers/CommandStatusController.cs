
using System.Diagnostics;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Models;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Telemetry;
using Hexalith.EventStore.Telemetry;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.Controllers;
/// <summary>
/// Controller for querying command processing status.
/// Route matches the Location header set by <see cref="CommandsController"/> (H5).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/commands/status")]
[Tags("Commands")]
public class CommandStatusController(
    ICommandStatusStore statusStore,
    ILogger<CommandStatusController> logger) : ControllerBase {
    /// <summary>
    /// Gets the current processing status of a command by correlation ID.
    /// </summary>
    /// <remarks>
    /// Tenant-scoped: only returns status for the authenticated user's authorized tenants (SEC-3).
    ///
    /// **Command Lifecycle States:**
    ///
    /// *In-flight states* (continue polling with the Retry-After interval from the 202 response):
    /// - **Received**: Command accepted, queued for processing
    /// - **Processing**: Domain service invocation in progress
    /// - **EventsStored**: Events persisted to state store
    /// - **EventsPublished**: Events published to pub/sub topics
    ///
    /// *Terminal states* (polling should stop):
    /// - **Completed**: Full pipeline completed successfully
    /// - **Rejected**: Domain business rule rejection
    /// - **PublishFailed**: Event publication failed after retry exhaustion
    /// - **TimedOut**: Processing exceeded timeout threshold
    ///
    /// Terminal states mean the command has reached its final outcome. In-flight states indicate
    /// the consumer should continue polling with the Retry-After interval.
    /// </remarks>
    /// <response code="200">Command status found. See response body for current state.</response>
    /// <response code="400">Bad request. Invalid correlation ID format.</response>
    /// <response code="401">Authentication required.</response>
    /// <response code="403">Forbidden. No tenant authorization claims found.</response>
    /// <response code="404">No command status found for the given correlation ID.</response>
    /// <response code="429">Rate limit exceeded. Retry after the Retry-After interval.</response>
    [HttpGet("{correlationId}")]
    [ProducesResponseType(typeof(CommandStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> GetStatus(string correlationId, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(correlationId);

        using Activity? activity = EventStoreActivitySources.EventStore.StartActivity(
            EventStoreActivitySources.QueryStatus, ActivityKind.Server);
        _ = (activity?.SetTag(EventStoreActivitySource.TagCorrelationId, correlationId));

        string requestCorrelationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? correlationId;

        try {
            if (string.IsNullOrWhiteSpace(correlationId)) {
                _ = (activity?.SetStatus(ActivityStatusCode.Error, "InvalidCorrelationId"));
                return CreateProblemDetails(
                    StatusCodes.Status400BadRequest,
                    ProblemTypeUris.BadRequest,
                    "Bad Request",
                    "Correlation ID is required.",
                    requestCorrelationId);
            }

            // Store correlationId in HttpContext.Items for error handler access
            HttpContext.Items["StatusCorrelationId"] = correlationId;

            // Extract tenant claims
            var tenantClaims = User.FindAll("eventstore:tenant")
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();

            if (tenantClaims.Count == 0) {
                logger.LogWarning(
                    "Status query denied: no tenant claims. CorrelationId={CorrelationId}",
                    requestCorrelationId);

                _ = (activity?.SetStatus(ActivityStatusCode.Error, "NoTenantClaims"));
                return CreateProblemDetails(
                    StatusCodes.Status403Forbidden,
                    ProblemTypeUris.Forbidden,
                    "Forbidden",
                    "No tenant authorization claims found. Access denied.",
                    requestCorrelationId);
            }

            // Try each authorized tenant (SEC-3: command could be under any authorized tenant)
            foreach (string tenant in tenantClaims) {
                _ = (activity?.SetTag(EventStoreActivitySource.TagTenantId, tenant));

                CommandStatusRecord? record = await statusStore
                    .ReadStatusAsync(tenant, correlationId, cancellationToken)
                    .ConfigureAwait(false);

                if (record is not null) {
                    logger.LogDebug(
                        "Status found: CorrelationId={CorrelationId}, Tenant={Tenant}, Status={Status}",
                        correlationId,
                        tenant,
                        record.Status);

                    // Only include Retry-After for non-terminal statuses (consumer should keep polling).
                    if (!IsTerminalStatus(record.Status)) {
                        Response.Headers["Retry-After"] = "1";
                    }

                    _ = (activity?.SetStatus(ActivityStatusCode.Ok));
                    return Ok(CommandStatusResponse.FromRecord(correlationId, record));
                }
            }

            // Not found in any authorized tenant -> 404 (also covers tenant mismatch per SEC-3)
            logger.LogDebug(
                "Status not found: CorrelationId={CorrelationId}, TenantsSearched={Tenants}",
                correlationId,
                string.Join(",", tenantClaims));

            _ = (activity?.SetStatus(ActivityStatusCode.Error, "NotFound"));
            return CreateProblemDetails(
                StatusCodes.Status404NotFound,
                ProblemTypeUris.CommandStatusNotFound,
                "Not Found",
                $"No command status found for correlation ID '{correlationId}'.",
                requestCorrelationId);
        }
        catch (Exception ex) {
            _ = (activity?.AddException(ex));
            _ = (activity?.SetStatus(ActivityStatusCode.Error, ex.Message));
            throw;
        }
    }

    private ObjectResult CreateProblemDetails(int statusCode, string type, string title, string detail, string correlationId) {
        var problemDetails = new ProblemDetails {
            Status = statusCode,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
            },
        };

        Response.ContentType = "application/problem+json";

        return new ObjectResult(problemDetails) { StatusCode = statusCode };
    }

    private static bool IsTerminalStatus(CommandStatus status) =>
        status is CommandStatus.Completed
            or CommandStatus.Rejected
            or CommandStatus.PublishFailed
            or CommandStatus.TimedOut;
}
