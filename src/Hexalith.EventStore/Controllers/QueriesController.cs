
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

        DualPrincipalIdentity dualPrincipal = DualPrincipalClaimsHelper.Extract(User, userId);

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
            ProjectionActorType: request.ProjectionActorType,
            IsGlobalAdmin: GlobalAdministratorHelper.IsGlobalAdministrator(User),
            Paging: NormalizePaging(request.Paging),
            OriginalActorId: dualPrincipal.OriginalActorId,
            AuthenticatedWorkloadId: dualPrincipal.AuthenticatedWorkloadId,
            IsDelegated: dualPrincipal.IsDelegated,
            Scopes: dualPrincipal.Scopes,
            Audience: dualPrincipal.Audience,
            DelegationId: dualPrincipal.DelegationId);

        SubmitQueryResult result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        QueryResponseMetadata producerMetadata = NormalizeProducerMetadata(result.Metadata);
        bool projectionBacked = producerMetadata.Provenance == QueryResponseProvenance.ProjectionBacked;
        string? currentETag = null;
        if (projectionBacked && !string.IsNullOrWhiteSpace(result.ProjectionType)) {
            try {
                currentETag = await eTagService
                    .GetCurrentETagAsync(result.ProjectionType, request.Tenant, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) {
                // Fail-open: no ETag header on response is acceptable
                Log.ETagPreCheckFailed(logger, correlationId, ex.GetType().Name);
            }
        }

        DateTimeOffset servedAt = DateTimeOffset.UtcNow;
        QueryResponseMetadata metadata = MergeQueryMetadata(
            producerMetadata,
            currentETag,
            isNotModified: false,
            servedAt);
        EnforceFreshnessPolicy(request, correlationId, producerMetadata, metadata, servedAt);

        bool conditionalMatch = ShouldEvaluateConditionalRequest(request, ifNoneMatch, correlationId)
            && currentETag is not null
            && ETagMatches(ifNoneMatch, currentETag);

        Response.Headers["X-Hexalith-Query-Provenance"] = metadata.Provenance.ToString();
        _ = Response.Headers.Remove(ProjectionLifecyclePolicy.HeaderName);
        if (metadata.Lifecycle != ProjectionLifecycleState.Unknown) {
            Response.Headers[ProjectionLifecyclePolicy.HeaderName] = metadata.Lifecycle.ToString();
        }

        if (currentETag is not null) {
            Response.Headers.ETag = $"\"{currentETag}\"";
        }

        if (conditionalMatch && currentETag is not null) {
            Log.ETagPreCheckMatch(logger, correlationId, currentETag[..Math.Min(8, currentETag.Length)]);
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return Ok(new SubmitQueryResponse(result.CorrelationId, result.Payload, Metadata: metadata));
    }

    private bool ShouldEvaluateConditionalRequest(
        SubmitQueryRequest request,
        string? ifNoneMatch,
        string correlationId) {
        if (string.IsNullOrWhiteSpace(ifNoneMatch)) {
            return false;
        }

        if (ifNoneMatch.AsSpan().Trim().SequenceEqual("*".AsSpan())) {
            Log.ETagPreCheckMiss(logger, correlationId, false, true);
            return false;
        }

        if (HasExplicitPolicyInputs(request) || HasUnsafeQueryPayload(request)) {
            Log.ETagPreCheckSkippedForPolicyInputs(logger, correlationId);
            return false;
        }

        HeaderProjectionTypeAnalysis analysis = AnalyzeHeaderProjectionTypes(ifNoneMatch);
        if (analysis.HasMixedProjectionTypes) {
            Log.MixedProjectionTypesSkipped(logger, correlationId);
            return false;
        }

        if (analysis.ProjectionType is null) {
            Log.ETagDecodeSkipped(logger, correlationId);
            return false;
        }

        return true;
    }

    private static QueryResponseMetadata NormalizeProducerMetadata(QueryResponseMetadata? metadata) {
        QueryResponseMetadata source = metadata ?? new QueryResponseMetadata();
        QueryResponseProvenance provenance = NormalizeProvenance(source.Provenance);
        ProjectionLifecycleState lifecycle = ProjectionLifecyclePolicy.Normalize(source.Lifecycle, provenance);
        QueryResponseMetadata normalized = source with {
            Provenance = provenance,
            Lifecycle = lifecycle,
            IsStale = ProjectionLifecyclePolicy.ProjectIsStale(lifecycle, source.IsStale),
            IsDegraded = ProjectionLifecyclePolicy.ProjectIsDegraded(lifecycle, source.IsDegraded),
        };
        return normalized.Provenance == QueryResponseProvenance.ProjectionBacked
            ? normalized
            : normalized with {
                ETag = null,
                IsNotModified = null,
                IsStale = null,
                ProjectionVersion = null,
            };
    }

    private static QueryResponseProvenance NormalizeProvenance(QueryResponseProvenance provenance)
        => provenance switch {
            QueryResponseProvenance.ProjectionBacked => QueryResponseProvenance.ProjectionBacked,
            QueryResponseProvenance.HandlerComputed => QueryResponseProvenance.HandlerComputed,
            _ => QueryResponseProvenance.Unknown,
        };

    private static QueryResponseMetadata MergeQueryMetadata(
        QueryResponseMetadata? producerMetadata,
        string? gatewayETag,
        bool isNotModified,
        DateTimeOffset servedAt)
        => new(
            ETag: gatewayETag ?? producerMetadata?.ETag,
            IsNotModified: producerMetadata?.Provenance == QueryResponseProvenance.ProjectionBacked
                ? isNotModified
                : null,
            IsStale: producerMetadata?.IsStale,
            IsDegraded: producerMetadata?.IsDegraded,
            ProjectionVersion: producerMetadata?.ProjectionVersion,
            ServedAt: producerMetadata?.ServedAt ?? servedAt,
            Paging: producerMetadata?.Paging,
            WarningCodes: producerMetadata?.WarningCodes) {
            Provenance = producerMetadata?.Provenance ?? QueryResponseProvenance.Unknown,
            Lifecycle = producerMetadata?.Lifecycle ?? ProjectionLifecycleState.Unknown,
        };

    private static void EnforceFreshnessPolicy(
        SubmitQueryRequest request,
        string correlationId,
        QueryResponseMetadata? producerMetadata,
        QueryResponseMetadata metadata,
        DateTimeOffset servedAt) {
        if (!HasMeaningfulFreshness(request.Freshness)) {
            return;
        }

        if (metadata.Provenance != QueryResponseProvenance.ProjectionBacked) {
            throw new QueryExecutionFailedException(
                correlationId,
                request.Tenant,
                request.Domain,
                request.AggregateId,
                request.QueryType,
                StatusCodes.Status400BadRequest,
                "Projection freshness could not be verified for this query response.",
                QueryProblemReasonCodes.ProjectionStale);
        }

        bool isCurrent = metadata.Lifecycle switch {
            ProjectionLifecycleState.Current => true,
            ProjectionLifecycleState.Unknown => metadata.IsStale == false,
            _ => false,
        };

        if (isCurrent
            && request.Freshness?.MaxStaleness is null) {
            return;
        }

        if (isCurrent
            && request.Freshness?.MaxStaleness is { } maxStaleness
            && producerMetadata?.ServedAt is { } producerServedAt
            && servedAt - producerServedAt <= maxStaleness) {
            // Compare the age as a bounded TimeSpan difference rather than subtracting MaxStaleness
            // from the served-at timestamp: a caller-supplied MaxStaleness large enough to push the
            // timestamp below DateTimeOffset.MinValue would otherwise throw ArgumentOutOfRangeException
            // (an unhandled 500) instead of satisfying the policy.
            return;
        }

        throw new QueryExecutionFailedException(
            correlationId,
            request.Tenant,
            request.Domain,
            request.AggregateId,
            request.QueryType,
            StatusCodes.Status400BadRequest,
            "Projection freshness could not be verified for this query response.",
            QueryProblemReasonCodes.ProjectionStale);
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

            string candidateValue = candidate.ToString();
            if (SelfRoutingETag.TryDecode(candidateValue, out _, out _)
                && candidate.SequenceEqual(currentETag.AsSpan())) {
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

    private static QueryPagingOptions? NormalizePaging(QueryPagingOptions? paging)
        => HasMeaningfulPaging(paging)
            ? paging! with {
                Cursor = string.IsNullOrWhiteSpace(paging.Cursor) ? null : paging.Cursor,
            }
            : null;

    private static bool HasMeaningfulFreshness(QueryFreshnessPolicy? freshness)
        => freshness is not null
            && (freshness.RequireFresh is true || freshness.MaxStaleness.HasValue);

    private static bool HasUnsafeQueryPayload(SubmitQueryRequest request) {
        if (request.Payload is null) {
            return false;
        }

        JsonElement value = request.Payload.Value;
        return value.ValueKind switch {
            JsonValueKind.Undefined or JsonValueKind.Null => false,
            JsonValueKind.Object => HasUnsafeObjectPayload(value, request),
            JsonValueKind.Array => value.GetArrayLength() > 0,
            _ => true,
        };
    }

    private static bool HasUnsafeObjectPayload(JsonElement payload, SubmitQueryRequest request) {
        using JsonElement.ObjectEnumerator properties = payload.EnumerateObject();
        if (!properties.MoveNext()) {
            return false;
        }

        JsonProperty property = properties.Current;
        if (properties.MoveNext()) {
            return true;
        }

        if (!string.Equals(property.Name, "id", StringComparison.OrdinalIgnoreCase)
            || property.Value.ValueKind != JsonValueKind.String) {
            return true;
        }

        string expectedId = string.IsNullOrWhiteSpace(request.EntityId)
            ? request.AggregateId
            : request.EntityId;

        return !string.Equals(property.Value.GetString(), expectedId, StringComparison.Ordinal);
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
