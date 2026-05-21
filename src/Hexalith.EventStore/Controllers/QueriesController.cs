
using System.Text.Json;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/queries")]
[Consumes("application/json")]
[Tags("Queries")]
public partial class QueriesController(
    IMediator mediator,
    IETagService eTagService,
    ITenantValidator tenantValidator,
    IRbacValidator rbacValidator,
    ILogger<QueriesController> logger) : ControllerBase {
    private const int MaxIfNoneMatchValues = 10;

    private readonly record struct HeaderProjectionTypeAnalysis(string? ProjectionType, bool HasMixedProjectionTypes);

    [HttpPost]
    [RequestSizeLimit(1_048_576)]
    [ProducesResponseType(typeof(SubmitQueryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
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

        await ValidateAuthorizationBeforeLookupAsync(request, userId, correlationId, cancellationToken)
            .ConfigureAwait(false);

        // Gate 1: ETag pre-check — decode projection type from self-routing ETag
        string? currentETag = null;
        bool gate1Skipped = false;
        bool skipConditionalPreCheck = HasExplicitPolicyInputs(request) || HasNonEmptyPayload(request.Payload);

        if (skipConditionalPreCheck && !string.IsNullOrWhiteSpace(ifNoneMatch)) {
            gate1Skipped = true;
            Log.ETagPreCheckSkippedForPolicyInputs(logger, correlationId);
        }
        else if (!string.IsNullOrWhiteSpace(ifNoneMatch) && ifNoneMatch.AsSpan().Trim().SequenceEqual("*".AsSpan())) {
            // Wildcard If-None-Match: skip Gate 1 entirely (no projection type to decode)
            gate1Skipped = true;
            Log.ETagPreCheckMiss(logger, correlationId, false, true);
        }
        else if (!string.IsNullOrWhiteSpace(ifNoneMatch)) {
            // Decode self-routing ETag to determine projection type for ETag actor lookup.
            // If the header contains decodable ETags for multiple projection types, skip Gate 1
            // to avoid returning 304 based on a validator for a different projection.
            HeaderProjectionTypeAnalysis analysis = AnalyzeHeaderProjectionTypes(ifNoneMatch);

            if (analysis.HasMixedProjectionTypes) {
                Log.MixedProjectionTypesSkipped(logger, correlationId);
            }
            else if (analysis.ProjectionType is not null) {
                try {
                    currentETag = await eTagService
                        .GetCurrentETagAsync(analysis.ProjectionType, request.Tenant, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) {
                    Log.ETagPreCheckFailed(logger, correlationId, ex.GetType().Name);
                    currentETag = null;
                }
            }
            else {
                // No ETag decoded (malformed, old-format, etc.) — cache miss
                Log.ETagDecodeSkipped(logger, correlationId);
            }
        }

        if (!gate1Skipped && currentETag is not null && ETagMatches(ifNoneMatch, currentETag)) {
            Response.Headers.ETag = $"\"{currentETag}\"";
            Log.ETagPreCheckMatch(logger, correlationId, currentETag[..Math.Min(8, currentETag.Length)]);
            return StatusCode(StatusCodes.Status304NotModified);
        }

        if (!gate1Skipped) {
            Log.ETagPreCheckMiss(logger, correlationId, currentETag is not null, !string.IsNullOrWhiteSpace(ifNoneMatch));
        }

        byte[] payloadBytes = request.Payload.HasValue
            ? JsonSerializer.SerializeToUtf8Bytes(request.Payload.Value)
            : [];

        // Aggregate-scoped queries persist projection state under the aggregate identity.
        // If callers omit EntityId, route to AggregateId so read actor identity matches the
        // projection write path. Explicit EntityId still wins for sub-aggregate queries.
        string entityId = string.IsNullOrWhiteSpace(request.EntityId)
            ? request.AggregateId
            : request.EntityId;

        var query = new SubmitQuery(
            Tenant: request.Tenant,
            Domain: request.Domain,
            AggregateId: request.AggregateId,
            QueryType: request.QueryType,
            Payload: payloadBytes,
            CorrelationId: correlationId,
            UserId: userId,
            EntityId: entityId,
            ProjectionType: string.IsNullOrWhiteSpace(request.ProjectionType)
                ? request.Domain
                : request.ProjectionType,
            ProjectionActorType: request.ProjectionActorType);

        SubmitQueryResult result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        // Set ETag response header — fetch from ETag actor if not already known
        // Uses runtime-discovered projection type (FR63), falls back to request.Domain
        if (currentETag is null) {
            string projectionTypeForETag = string.IsNullOrWhiteSpace(result.ProjectionType)
                ? (string.IsNullOrWhiteSpace(request.ProjectionType) ? request.Domain : request.ProjectionType)
                : result.ProjectionType;
            try {
                currentETag = await eTagService
                    .GetCurrentETagAsync(projectionTypeForETag, request.Tenant, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception) {
                // Fail-open: no ETag header on response is acceptable
            }
        }

        if (currentETag is not null) {
            Response.Headers.ETag = $"\"{currentETag}\"";
        }

        var metadata = new QueryResponseMetadata(
            ETag: currentETag,
            IsNotModified: false,
            ServedAt: DateTimeOffset.UtcNow,
            Paging: request.Paging is null
                ? null
                : new QueryPagingMetadata(
                    request.Paging.PageSize ?? QueryPolicyLimits.DefaultPageSize,
                    request.Paging.Offset));

        return Ok(new SubmitQueryResponse(result.CorrelationId, result.Payload, Metadata: metadata));
    }

    private async Task ValidateAuthorizationBeforeLookupAsync(
        SubmitQueryRequest request,
        string subjectId,
        string correlationId,
        CancellationToken cancellationToken) {
        TenantValidationResult tenantResult = await tenantValidator
            .ValidateAsync(User, request.Tenant, cancellationToken, request.AggregateId)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "ITenantValidator.ValidateAsync returned null. This is a server bug, not a user authorization failure.");

        if (!tenantResult.IsAuthorized) {
            Log.QueryAuthorizationDenied(logger, correlationId, request.Tenant, request.Domain, request.QueryType, tenantResult.Reason ?? "Tenant access denied.");
            throw new CommandAuthorizationException(
                request.Tenant,
                request.Domain,
                request.QueryType,
                tenantResult.Reason ?? "Tenant access denied.",
                tenantResult.ReasonCode);
        }

        RbacValidationResult rbacResult = await rbacValidator
            .ValidateAsync(User, request.Tenant, request.Domain, request.QueryType, "query", cancellationToken, request.AggregateId)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "IRbacValidator.ValidateAsync returned null. This is a server bug, not a user authorization failure.");

        if (!rbacResult.IsAuthorized) {
            Log.QueryAuthorizationDenied(logger, correlationId, request.Tenant, request.Domain, request.QueryType, rbacResult.Reason ?? "RBAC check failed.");
            throw new CommandAuthorizationException(
                request.Tenant,
                request.Domain,
                request.QueryType,
                rbacResult.Reason ?? "RBAC check failed.",
                rbacResult.ReasonCode);
        }

        HttpContext.Items[AuthorizationBehavior<SubmitQuery, SubmitQueryResult>.PrevalidatedAuthorizationContextKey]
            = new GatewayAuthorizationContext(
                request.Tenant,
                request.Domain,
                request.QueryType,
                "query",
                request.AggregateId,
                subjectId);
    }

    /// <summary>
    /// Analyzes the If-None-Match header for self-routing ETags.
    /// Returns the first decoded projection type when all decodable ETags agree on the same projection type.
    /// If multiple decodable projection types are present, Gate 1 should be skipped for safety.
    /// </summary>
    private static HeaderProjectionTypeAnalysis AnalyzeHeaderProjectionTypes(string ifNoneMatch) {
        ReadOnlySpan<char> header = ifNoneMatch.AsSpan().Trim();
        int start = 0;
        string? firstProjectionType = null;

        for (int i = 0; i <= header.Length; i++) {
            if (i < header.Length && header[i] != ',') {
                continue;
            }

            ReadOnlySpan<char> candidate = header[start..i].Trim();
            if (candidate.Length >= 2 && candidate[0] == '"' && candidate[^1] == '"') {
                candidate = candidate[1..^1];
            }

            if (SelfRoutingETag.TryDecode(candidate.ToString(), out string? projectionType, out _)) {
                if (firstProjectionType is null) {
                    firstProjectionType = projectionType;
                }
                else if (!string.Equals(firstProjectionType, projectionType, StringComparison.Ordinal)) {
                    return new HeaderProjectionTypeAnalysis(null, true);
                }
            }

            start = i + 1;
        }

        return new HeaderProjectionTypeAnalysis(firstProjectionType, false);
    }

    private bool ETagMatches(string? ifNoneMatch, string currentETag) {
        if (string.IsNullOrWhiteSpace(ifNoneMatch)) {
            return false;
        }

        ReadOnlySpan<char> header = ifNoneMatch.AsSpan().Trim();
        if (header.SequenceEqual("*".AsSpan())) {
            return true;
        }

        int valueCount = 1;
        for (int i = 0; i < header.Length; i++) {
            if (header[i] == ',') {
                valueCount++;
                if (valueCount > MaxIfNoneMatchValues) {
                    Log.TooManyIfNoneMatchValues(logger, valueCount, MaxIfNoneMatchValues);
                    return false;
                }
            }
        }

        int start = 0;
        for (int i = 0; i <= header.Length; i++) {
            if (i < header.Length && header[i] != ',') {
                continue;
            }

            ReadOnlySpan<char> candidate = header[start..i].Trim();
            if (candidate.Length >= 2 && candidate[0] == '"' && candidate[^1] == '"') {
                candidate = candidate[1..^1];
            }

            if (candidate.SequenceEqual(currentETag.AsSpan())) {
                return true;
            }

            start = i + 1;
        }

        return false;
    }

    private static bool HasExplicitPolicyInputs(SubmitQueryRequest request)
        => HasMeaningfulPaging(request.Paging)
            || !string.IsNullOrWhiteSpace(request.Search)
            || request.Filters is { Count: > 0 }
            || request.OrderBy is { Count: > 0 }
            || HasMeaningfulFreshness(request.Freshness);

    private static bool HasMeaningfulPaging(QueryPagingOptions? paging)
        => paging is not null
            && (paging.PageSize.HasValue || paging.Offset.HasValue || !string.IsNullOrWhiteSpace(paging.Cursor));

    private static bool HasMeaningfulFreshness(QueryFreshnessPolicy? freshness)
        => freshness is not null
            && (freshness.RequireFresh is true || freshness.MaxStaleness.HasValue);

    private static bool HasNonEmptyPayload(JsonElement? payload) {
        if (payload is null) {
            return false;
        }

        JsonElement value = payload.Value;
        return value.ValueKind switch {
            JsonValueKind.Undefined or JsonValueKind.Null => false,
            JsonValueKind.Object => value.EnumerateObject().Any(),
            JsonValueKind.Array => value.GetArrayLength() > 0,
            _ => true,
        };
    }

    private static partial class Log {
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

        [LoggerMessage(
            EventId = 1064,
            Level = LogLevel.Warning,
            Message = "If-None-Match contained too many values. ParsedValueCount={ParsedValueCount}, MaximumAllowed={MaximumAllowed}. Skipping Gate 1 pre-check.")]
        public static partial void TooManyIfNoneMatchValues(ILogger logger, int parsedValueCount, int maximumAllowed);

        [LoggerMessage(
            EventId = 1065,
            Level = LogLevel.Warning,
            Message = "ETag pre-check failed. Proceeding without ETag optimization: CorrelationId={CorrelationId}, ExceptionType={ExceptionType}.")]
        public static partial void ETagPreCheckFailed(ILogger logger, string correlationId, string exceptionType);

        [LoggerMessage(
            EventId = 1066,
            Level = LogLevel.Debug,
            Message = "Self-routing ETag decode skipped (malformed or old-format). Proceeding to query: CorrelationId={CorrelationId}.")]
        public static partial void ETagDecodeSkipped(ILogger logger, string correlationId);

        [LoggerMessage(
            EventId = 1067,
            Level = LogLevel.Debug,
            Message = "Gate 1 skipped because If-None-Match contains decodable ETags for multiple projection types. CorrelationId={CorrelationId}.")]
        public static partial void MixedProjectionTypesSkipped(ILogger logger, string correlationId);

        [LoggerMessage(
            EventId = 1068,
            Level = LogLevel.Debug,
            Message = "ETag pre-check skipped because request carries explicit query policy inputs or non-empty payload. CorrelationId={CorrelationId}.")]
        public static partial void ETagPreCheckSkippedForPolicyInputs(ILogger logger, string correlationId);

        [LoggerMessage(
            EventId = 1069,
            Level = LogLevel.Warning,
            Message = "Query authorization denied before lookup: SecurityEvent={SecurityEvent}, CorrelationId={CorrelationId}, Tenant={Tenant}, Domain={Domain}, QueryType={QueryType}, Reason={Reason}, Stage=QueryAuthorizationBeforeLookup")]
        public static partial void QueryAuthorizationDenied(
            ILogger logger,
            string correlationId,
            string tenant,
            string domain,
            string queryType,
            string reason,
            string securityEvent = "QueryAuthorizationDenied");
    }
}
