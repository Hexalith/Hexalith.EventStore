
using System.Text.Json;

using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Pipeline.Queries;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.CommandApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/queries")]
[Consumes("application/json")]
public class QueriesController(IMediator mediator, ILogger<QueriesController> logger) : ControllerBase {
    [HttpPost]
    [RequestSizeLimit(1_048_576)]
    [ProducesResponseType(typeof(SubmitQueryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> Submit([FromBody] SubmitQueryRequest request, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();

        // Store tenant in HttpContext for error handlers and rate limiter OnRejected callback
        if (!string.IsNullOrEmpty(request.Tenant)) {
            HttpContext.Items["RequestTenantId"] = request.Tenant;
        }

        // Extract UserId from JWT -- use 'sub' claim ONLY (F-RT2: 'name' may be user-controllable)
        string? userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(userId)) {
            logger.LogWarning(
                "JWT 'sub' claim missing for query submission. Rejecting request as unauthorized. CorrelationId={CorrelationId}.",
                correlationId);

            return Unauthorized();
        }

        byte[] payloadBytes = request.Payload.HasValue
            ? JsonSerializer.SerializeToUtf8Bytes(request.Payload.Value)
            : [];

        var query = new SubmitQuery(
            Tenant: request.Tenant,
            Domain: request.Domain,
            AggregateId: request.AggregateId,
            QueryType: request.QueryType,
            Payload: payloadBytes,
            CorrelationId: correlationId,
            UserId: userId);

        SubmitQueryResult result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        return Ok(new SubmitQueryResponse(result.CorrelationId, result.Payload));
    }
}
