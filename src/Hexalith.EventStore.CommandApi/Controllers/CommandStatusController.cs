namespace Hexalith.EventStore.CommandApi.Controllers;

using System.Diagnostics;

using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.CommandApi.Models;
using Hexalith.EventStore.CommandApi.Telemetry;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Telemetry;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Controller for querying command processing status.
/// Route matches the Location header set by <see cref="CommandsController"/> (H5).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/commands/status")]
public class CommandStatusController(
    ICommandStatusStore statusStore,
    ILogger<CommandStatusController> logger) : ControllerBase {
    /// <summary>
    /// Gets the current processing status of a command by correlation ID.
    /// Tenant-scoped: only returns status for the authenticated user's authorized tenants (SEC-3).
    /// </summary>
    [HttpGet("{correlationId}")]
    [ProducesResponseType(typeof(CommandStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<IActionResult> GetStatus(string correlationId, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(correlationId);

        using Activity? activity = EventStoreActivitySources.CommandApi.StartActivity(
            EventStoreActivitySources.QueryStatus, ActivityKind.Server);
        activity?.SetTag(EventStoreActivitySource.TagCorrelationId, correlationId);

        string requestCorrelationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? correlationId;

        try {
            // Validate GUID format
            if (!Guid.TryParse(correlationId, out _)) {
                activity?.SetStatus(ActivityStatusCode.Error, "InvalidCorrelationId");
                return CreateProblemDetails(
                    StatusCodes.Status400BadRequest,
                    "Bad Request",
                    $"Correlation ID '{correlationId}' is not a valid GUID format.",
                    requestCorrelationId);
            }

            // Store correlationId in HttpContext.Items for error handler access
            HttpContext.Items["StatusCorrelationId"] = correlationId;

            // Extract tenant claims
            List<string> tenantClaims = User.FindAll("eventstore:tenant")
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();

            if (tenantClaims.Count == 0) {
                logger.LogWarning(
                    "Status query denied: no tenant claims. CorrelationId={CorrelationId}",
                    requestCorrelationId);

                activity?.SetStatus(ActivityStatusCode.Error, "NoTenantClaims");
                return CreateProblemDetails(
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    "No tenant authorization claims found. Access denied.",
                    requestCorrelationId);
            }

            // Try each authorized tenant (SEC-3: command could be under any authorized tenant)
            foreach (string tenant in tenantClaims) {
                activity?.SetTag(EventStoreActivitySource.TagTenantId, tenant);

                CommandStatusRecord? record = await statusStore
                    .ReadStatusAsync(tenant, correlationId, cancellationToken)
                    .ConfigureAwait(false);

                if (record is not null) {
                    logger.LogDebug(
                        "Status found: CorrelationId={CorrelationId}, Tenant={Tenant}, Status={Status}",
                        correlationId,
                        tenant,
                        record.Status);

                    activity?.SetStatus(ActivityStatusCode.Ok);
                    return Ok(CommandStatusResponse.FromRecord(record));
                }
            }

            // Not found in any authorized tenant -> 404 (also covers tenant mismatch per SEC-3)
            logger.LogDebug(
                "Status not found: CorrelationId={CorrelationId}, TenantsSearched={Tenants}",
                correlationId,
                string.Join(",", tenantClaims));

            activity?.SetStatus(ActivityStatusCode.Error, "NotFound");
            return CreateProblemDetails(
                StatusCodes.Status404NotFound,
                "Not Found",
                $"No command status found for correlation ID '{correlationId}'.",
                requestCorrelationId);
        }
        catch (Exception ex) {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private ObjectResult CreateProblemDetails(int statusCode, string title, string detail, string correlationId) {
        var problemDetails = new ProblemDetails {
            Status = statusCode,
            Title = title,
            Type = "https://tools.ietf.org/html/rfc9457#section-3",
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
