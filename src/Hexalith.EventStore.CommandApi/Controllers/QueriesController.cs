
using System.Text.Json;

using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.CommandApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/queries")]
[Consumes("application/json")]
public partial class QueriesController(IMediator mediator, IETagService eTagService, ILogger<QueriesController> logger) : ControllerBase {
    private const int MaxIfNoneMatchValues = 10;

    [HttpPost]
    [RequestSizeLimit(1_048_576)]
    [ProducesResponseType(typeof(SubmitQueryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitQueryRequest request,
        [FromHeader(Name = "If-None-Match")] string? ifNoneMatch,
        CancellationToken cancellationToken) {
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

        // Gate 1: ETag pre-check — fetch current ETag for this projection+tenant
        string? currentETag = await eTagService
            .GetCurrentETagAsync(request.Domain, request.Tenant, cancellationToken)
            .ConfigureAwait(false);

        if (currentETag is not null && ETagMatches(ifNoneMatch, currentETag)) {
            Log.ETagPreCheckMatch(logger, correlationId, currentETag[..Math.Min(8, currentETag.Length)]);
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Log.ETagPreCheckMiss(logger, correlationId, currentETag is not null, !string.IsNullOrWhiteSpace(ifNoneMatch));

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
            UserId: userId,
            EntityId: request.EntityId);

        SubmitQueryResult result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        // Set ETag response header before returning body (must be before response body starts)
        if (currentETag is not null) {
            Response.Headers.ETag = $"\"{currentETag}\"";
        }

        return Ok(new SubmitQueryResponse(result.CorrelationId, result.Payload));
    }

    private static bool ETagMatches(string? ifNoneMatch, string currentETag) {
        if (string.IsNullOrWhiteSpace(ifNoneMatch)) {
            return false;
        }

        if (ifNoneMatch.Trim() == "*") {
            return true;
        }

        string[] parts = ifNoneMatch.Split(',');
        if (parts.Length > MaxIfNoneMatchValues) {
            return false; // Skip Gate 1 if too many values (PM-10)
        }

        foreach (string part in parts) {
            string trimmed = part.Trim().Trim('"');
            if (trimmed == currentETag) {
                return true;
            }
        }

        return false;
    }

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 1062,
            Level = LogLevel.Debug,
            Message = "ETag pre-check match. Returning HTTP 304: CorrelationId={CorrelationId}, ETag={ETagPrefix}, Stage=ETagPreCheckMatch")]
        public static partial void ETagPreCheckMatch(ILogger logger, string correlationId, string eTagPrefix);

        [LoggerMessage(
            EventId = 1063,
            Level = LogLevel.Debug,
            Message = "ETag pre-check miss. Proceeding to query routing: CorrelationId={CorrelationId}, HasCurrentETag={HasCurrentETag}, HasIfNoneMatch={HasIfNoneMatch}, Stage=ETagPreCheckMiss")]
        public static partial void ETagPreCheckMiss(ILogger logger, string correlationId, bool hasCurrentETag, bool hasIfNoneMatch);
    }
}
