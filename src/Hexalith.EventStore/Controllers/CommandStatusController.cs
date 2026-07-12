
using System.Diagnostics;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Models;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Diagnostics;
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
    ILogger<CommandStatusController> logger,
    ICommandCorrelationIndex? correlationIndex = null) : ControllerBase {
    /// <summary>
    /// Gets the current processing status of a command by message ID, with bounded correlation compatibility.
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
    /// <response code="400">Bad request. Invalid message or correlation identifier format.</response>
    /// <response code="401">Authentication required.</response>
    /// <response code="403">Forbidden. No tenant authorization claims found.</response>
    /// <response code="404">No command status found for the given message or correlation identifier.</response>
    /// <response code="429">Rate limit exceeded. Retry after the Retry-After interval.</response>
    [HttpGet("{messageId}")]
    [ProducesResponseType(typeof(CommandStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> GetStatus(string messageId, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(messageId);

        using Activity? activity = EventStoreActivitySources.EventStore.StartActivity(
            EventStoreActivitySources.QueryStatus, ActivityKind.Server);
        _ = (activity?.SetTag(EventStoreActivitySource.TagCorrelationId, messageId));

        string requestCorrelationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? messageId;

        try {
            if (!CorrelationIdMiddleware.IsValidIdentifier(messageId)) {
                _ = (activity?.SetStatus(ActivityStatusCode.Error, "InvalidCorrelationId"));
                return CreateProblemDetails(
                    StatusCodes.Status400BadRequest,
                    ProblemTypeUris.BadRequest,
                    "Bad Request",
                    $"Message or correlation identifier must be 1-{CorrelationIdMiddleware.MaxIdentifierLength} characters of alphanumerics and hyphens (with alphanumeric anchors).",
                    requestCorrelationId);
            }

            // Store correlationId in HttpContext.Items for error handler access
            HttpContext.Items["StatusCorrelationId"] = messageId;

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

            var matches = new List<(string Tenant, string MessageId, CommandStatusRecord Record)>();
            var legacyMatches = new List<(string Tenant, CommandStatusRecord Record)>();

            // Search only authorized tenants. Message-primary records are authoritative; the
            // bounded index is the sole compatibility lookup for a correlation identifier.
            foreach (string tenant in tenantClaims) {
                _ = (activity?.SetTag(EventStoreActivitySource.TagTenantId, tenant));

                CommandStatusRecord? directRecord = await statusStore
                    .ReadStatusAsync(tenant, messageId, cancellationToken)
                    .ConfigureAwait(false);

                if (directRecord is not null
                    && string.Equals(directRecord.MessageId, messageId, StringComparison.Ordinal)) {
                    matches.Add((tenant, messageId, directRecord));
                    continue;
                }

                if (correlationIndex is not null) {
                    CommandCorrelationResolution resolution = await correlationIndex
                        .ResolveAsync(tenant, messageId, cancellationToken)
                        .ConfigureAwait(false);

                    if (resolution.Outcome == CommandCorrelationResolutionOutcome.Ambiguous) {
                        return CreateAmbiguityProblemDetails(requestCorrelationId);
                    }

                    if (resolution is { Outcome: CommandCorrelationResolutionOutcome.Resolved, MessageId: not null }) {
                        CommandStatusRecord? indexedRecord = await statusStore
                            .ReadStatusAsync(tenant, resolution.MessageId, cancellationToken)
                            .ConfigureAwait(false);
                        if (indexedRecord is not null) {
                            matches.Add((tenant, resolution.MessageId, indexedRecord));
                            continue;
                        }
                    }
                }

                if (directRecord is not null) {
                    legacyMatches.Add((tenant, directRecord));
                }
            }

            if (matches.Count > 1 || (matches.Count == 0 && legacyMatches.Count > 1)) {
                return CreateAmbiguityProblemDetails(requestCorrelationId);
            }

            if (matches.Count == 1) {
                (string tenant, string resolvedMessageId, CommandStatusRecord record) = matches[0];
                return CreateStatusResult(tenant, resolvedMessageId, record, activity);
            }

            if (legacyMatches.Count == 1) {
                (string tenant, CommandStatusRecord record) = legacyMatches[0];
                return CreateStatusResult(tenant, messageId, record, activity);
            }

            // Not found in any authorized tenant -> 404 (also covers tenant mismatch per SEC-3)
            logger.LogDebug(
                "Status not found: CorrelationId={CorrelationId}, TenantsSearched={Tenants}",
                messageId,
                string.Join(",", tenantClaims));

            _ = (activity?.SetStatus(ActivityStatusCode.Error, "NotFound"));
            return CreateProblemDetails(
                StatusCodes.Status404NotFound,
                ProblemTypeUris.CommandStatusNotFound,
                "Not Found",
                $"No command status found for message or correlation identifier '{messageId}'.",
                requestCorrelationId);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            ProtectedDataDiagnosticRedactor.RecordActivityException(activity, ex, "command-status");
            throw;
        }
    }

    private IActionResult CreateStatusResult(
        string tenant,
        string messageId,
        CommandStatusRecord record,
        Activity? activity) {
        logger.LogDebug(
            "Status found: MessageId={MessageId}, CorrelationId={CorrelationId}, Tenant={Tenant}, Status={Status}",
            messageId,
            record.CorrelationId,
            tenant,
            record.Status);

        if (!record.Status.IsTerminal()) {
            Response.Headers["Retry-After"] = "1";
        }

        _ = (activity?.SetStatus(ActivityStatusCode.Ok));
        return Ok(CommandStatusResponse.FromRecord(messageId, record));
    }

    private ObjectResult CreateAmbiguityProblemDetails(string requestCorrelationId) {
        _ = (Activity.Current?.SetStatus(ActivityStatusCode.Error, "AmbiguousCorrelation"));
        return CreateProblemDetails(
            StatusCodes.Status409Conflict,
            ProblemTypeUris.CommandCorrelationAmbiguous,
            "Ambiguous Command Correlation",
            "The correlation identifier maps to multiple commands. Query again using the command MessageId.",
            requestCorrelationId);
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
}
