
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.Server.Identity;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Middleware;
using Hexalith.EventStore.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/queries")]
[Consumes("application/json")]
[Tags("Queries")]
public partial class QueriesController : ControllerBase {
    private IMediator mediator { get; }
    private IETagService eTagService { get; }
    private ITenantValidator tenantValidator { get; }
    private IRbacValidator rbacValidator { get; }
    private ILogger<QueriesController> logger { get; }
    private IIdentityGatewayAdmission? identityAdmission { get; }
    private IConfiguration? configuration { get; }
    private WorkloadAssertionValidator? assertionValidator { get; }
    private IOptionsMonitor<WorkloadAuthenticationOptions>? workloadOptions { get; }
    private Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority? catalogAuthority { get; }

    /// <summary>Creates a catalog-enforced runtime entry point.</summary>
    public QueriesController(
        Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority authority,
        IMediator mediator,
        IETagService eTagService,
        ITenantValidator tenantValidator,
        IRbacValidator rbacValidator,
        ILogger<QueriesController> logger,
        IIdentityGatewayAdmission? identityAdmission = null,
        IConfiguration? configuration = null,
        WorkloadAssertionValidator? assertionValidator = null,
        IOptionsMonitor<WorkloadAuthenticationOptions>? workloadOptions = null)
        : this(mediator, eTagService, tenantValidator, rbacValidator, logger, identityAdmission, configuration, assertionValidator, workloadOptions, authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
    }

    /// <summary>Explicit friend-only composition seam for existing isolated protocol tests.</summary>
    internal QueriesController(
        IMediator mediator,
        IETagService eTagService,
        ITenantValidator tenantValidator,
        IRbacValidator rbacValidator,
        ILogger<QueriesController> logger,
        IIdentityGatewayAdmission? identityAdmission = null,
        IConfiguration? configuration = null,
        WorkloadAssertionValidator? assertionValidator = null,
        IOptionsMonitor<WorkloadAuthenticationOptions>? workloadOptions = null,
        Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority? catalogAuthority = null)
    {
        this.mediator = mediator;
        this.eTagService = eTagService;
        this.tenantValidator = tenantValidator;
        this.rbacValidator = rbacValidator;
        this.logger = logger;
        this.identityAdmission = identityAdmission;
        this.configuration = configuration;
        this.assertionValidator = assertionValidator;
        this.workloadOptions = workloadOptions;
        this.catalogAuthority = catalogAuthority;

    }

    private const int MaxIfNoneMatchValues = 10;
    private const int MaxMetadataHeaderValueLength = 4096;
    private const int MaxWarningCodeCount = 16;

    private readonly record struct HeaderProjectionTypeAnalysis(string? ProjectionType, bool HasMixedProjectionTypes);

    /// <summary>Routes only configured owner evidence queries under an exact signed requester binding.</summary>
    [HttpPost("owner-authority/workload")]
    [Authorize(Policy = DaprInternalAuthenticationOptions.GatewayOwnerAuthorityQueryPolicy)]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> SubmitBoundOwnerAuthorityAsync(
        [FromBody] BoundOwnerQueryRequest request, CancellationToken cancellationToken) {
        if (request?.Query is not { } query || query.Payload is null || query.AdditionalProperties is { Count: > 0 }
            || query.EntityId is not null || query.ProjectionType is not null || query.ProjectionActorType is not null
            || query.Paging is not null || query.Search is not null || query.Filters is not null || query.OrderBy is not null
            || query.Freshness is not null || string.IsNullOrWhiteSpace(request.PrincipalId)) {
            return Forbid();
        }

        string? caller = User.FindFirst(EventStoreWorkloadAuthenticationDefaults.WorkloadClaimType)?.Value;
        string? workloadSubject = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        string? allowed = caller is null ? null : configuration?[$"Authentication:BoundOwnerQuery:AllowedQueries:{caller}"];
        string[] permitted = allowed?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
        bool allowedQuery = permitted.Contains(query.QueryType, StringComparer.Ordinal);
        bool signed = Request.Headers.TryGetValue(BoundOwnerQueryProof.HeaderName, out var proof)
            && proof.Count == 1 && caller is not null
            && BoundOwnerQueryProof.Verify(request, proof[0], caller,
                configuration?[$"Authentication:BoundStreamRead:PublicKeys:{caller}"], DateTimeOffset.UtcNow);
        if (string.IsNullOrWhiteSpace(caller) || workloadSubject != "workload:" + caller
            || !allowedQuery || !signed) {
            return Forbid();
        }

        QueryCallerAuthority? ownerAuthority = null;
        if (request.OriginatingQuery is not null || request.OriginatingAssertion is not null)
        {
            if (request.OriginatingQuery is not { CallerAuthority.Origin: "authenticated-caller" or "delegated-owner-query" } original
                || original.TenantId != query.Tenant || original.UserId != request.PrincipalId || assertionValidator is null)
                return Forbid();
            string gatewayWorkload = configuration?["Authentication:QueryAuthority:GatewayWorkloadId"] ?? "eventstore";
            WorkloadAssertionEvaluation origin = await assertionValidator.ValidateForAudienceAsync(
                DaprInternalAuthenticationOptions.SchemeName, request.OriginatingAssertion,
                EventStoreWorkloadOperations.DomainServiceQuery, [gatewayWorkload], caller, cancellationToken).ConfigureAwait(false);
            if (!origin.Succeeded || origin.Principal is null || !QueryCallerAuthorityBinding.Verify(original, origin.Principal, gatewayWorkload)) return Forbid();
            if (original.CallerAuthority.Origin == "delegated-owner-query"
                && (original.CallerAuthority.OriginatingContext is not { } inherited
                    || inherited.OriginalActorId != request.PrincipalId || !original.IsDelegated
                    || inherited.IsDelegated && string.IsNullOrWhiteSpace(inherited.DelegationId)
                    || !inherited.IsDelegated && inherited.DelegationId is not null)) return Forbid();
            ownerAuthority = new("delegated-owner-query", [], original.CallerAuthority.Permissions)
            {
                OriginatingContext = original.CallerAuthority.Origin == "delegated-owner-query"
                    ? original.CallerAuthority.OriginatingContext
                    : new(original.OriginalActorId!, original.AuthenticatedWorkloadId,
                        original.IsDelegated, original.DelegationId, original.Scopes, original.Audience),
            };
        }

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(query.Payload.Value);
        HttpContext.Items[AuthorizationBehavior<SubmitQuery, SubmitQueryResult>.PrevalidatedAuthorizationContextKey]
            = new GatewayAuthorizationContext(query.Tenant, query.Domain, query.QueryType, "query", query.AggregateId, workloadSubject);
        var submitted = new SubmitQuery(query.Tenant, query.Domain, query.AggregateId, query.QueryType,
            payload, correlationId, request.PrincipalId, EntityId: query.AggregateId,
            ProjectionType: catalogAuthority is null ? query.Domain : null, AuthenticatedWorkloadId: caller,
            OriginalActorId: request.PrincipalId, IsDelegated: true,
            Audience: ownerAuthority is not null && workloadOptions?.Get(DaprInternalAuthenticationOptions.SchemeName).Audience is { Length: > 0 } audience
                ? [audience.Trim()] : null) { CallerAuthority = ownerAuthority };
        if (query.QueryType == Hexalith.EventStore.Contracts.Projections.InspectProjectionCurrentQuery.QueryType)
            return await InspectProjectionCurrentAsync(request, ownerAuthority, correlationId, cancellationToken).ConfigureAwait(false);
        SubmitQueryResult result = await mediator.Send(submitted, cancellationToken).ConfigureAwait(false);
        Response.Headers.CacheControl = "no-store";
        return Ok(new SubmitQueryResponse(result.CorrelationId, result.Payload,
            Metadata: NormalizeProducerMetadata(result.Metadata)));
    }

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
        if (string.IsNullOrWhiteSpace(userId) || User.FindAll("sub").Count() != 1) {
            logger.LogWarning(
                "JWT 'sub' claim missing for query submission. Rejecting request as unauthorized. CorrelationId={CorrelationId}.",
                correlationId);

            return Unauthorized();
        }

        await ValidateAuthorizationBeforeLookupAsync(request, userId, correlationId, cancellationToken)
            .ConfigureAwait(false);

        var catalogLease = catalogAuthority is null ? null
            : await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease).ConfigureAwait(false);
        if (catalogLease is not null)
        {
            var selected = catalogLease.Snapshot.Resolve("query", request.Tenant, request.Domain, request.QueryType);
            if ((!string.IsNullOrWhiteSpace(request.ProjectionType) && request.ProjectionType != selected.Declaration.ProjectionType)
                || (!string.IsNullOrWhiteSpace(request.ProjectionActorType) && request.ProjectionActorType != selected.Declaration.Execution!.ActorType))
                throw new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException("The selected query target differs from committed authority.");
        }

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

        string? identityProof = null;
        if (IdentityOperationCatalog.RequiresAdmission(request.QueryType)
            || identityAdmission?.RequiresAdmission(request.Domain, request.QueryType) == true)
        {
            if (identityAdmission is null || !string.IsNullOrWhiteSpace(request.EntityId) && request.EntityId != request.AggregateId)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            var scope = new IdentityAdmissionScope(request.Tenant, request.Domain, request.AggregateId,
                request.QueryType, correlationId, correlationId, IdentityAdmissionProof.Digest(payloadBytes));
            identityProof = await identityAdmission.AdmitAsync(User, scope, payloadBytes, cancellationToken).ConfigureAwait(false);
            if (identityProof is null)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            Response.Headers.CacheControl = "no-store";
        }

        QueryCallerAuthority? callerAuthority;
        try
        {
            callerAuthority = Hexalith.EventStore.Queries.GatewayQueryCallerAuthority.Capture(User, configuration, request.Domain, request.QueryType);
        }
        catch (InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

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
                ? (catalogAuthority is null ? request.Domain : null)
                : request.ProjectionType,
            ProjectionActorType: request.ProjectionActorType,
            IsGlobalAdmin: GlobalAdministratorHelper.IsGlobalAdministrator(User),
            Paging: NormalizePaging(request.Paging),
            OriginalActorId: dualPrincipal.OriginalActorId,
            AuthenticatedWorkloadId: dualPrincipal.AuthenticatedWorkloadId,
            IsDelegated: dualPrincipal.IsDelegated,
            Scopes: dualPrincipal.Scopes,
            Audience: dualPrincipal.Audience,
            DelegationId: dualPrincipal.DelegationId) { IdentityAdmissionProof = identityProof, CallerAuthority = callerAuthority };

        SubmitQueryResult result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);

        QueryResponseMetadata producerMetadata = NormalizeProducerMetadata(result.Metadata);
        bool projectionBacked = producerMetadata.Provenance == QueryResponseProvenance.ProjectionBacked;
        bool hasProducerETag = projectionBacked && !string.IsNullOrWhiteSpace(producerMetadata.ETag);
        string? currentETag = hasProducerETag
            ? producerMetadata.ETag
            : null;
        if (currentETag is null && projectionBacked && !string.IsNullOrWhiteSpace(result.ProjectionType)) {
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

        string? projectionVersionHeader = GetLosslessProjectionVersionHeader(metadata.ProjectionVersion);
        string? warningCodes = FormatWarningCodes(metadata.WarningCodes);
        bool conditionalMatch = (string.IsNullOrEmpty(metadata.ProjectionVersion) || projectionVersionHeader is not null)
            && (metadata.WarningCodes is not { Count: > 0 } || warningCodes is not null)
            && ShouldEvaluateConditionalRequest(request, ifNoneMatch, correlationId, hasProducerETag)
            && currentETag is not null
            && ETagMatches(ifNoneMatch, currentETag, hasProducerETag);

        Response.Headers["X-Hexalith-Query-Provenance"] = metadata.Provenance.ToString();
        _ = Response.Headers.Remove(ProjectionLifecyclePolicy.HeaderName);
        if (metadata.Lifecycle != ProjectionLifecycleState.Unknown) {
            Response.Headers[ProjectionLifecyclePolicy.HeaderName] = metadata.Lifecycle.ToString();
        }

        if (projectionBacked && projectionVersionHeader is not null) {
            Response.Headers["X-Hexalith-Projection-Version"] = projectionVersionHeader;
        }

        if (metadata.ServedAt is { } metadataServedAt) {
            Response.Headers["X-Hexalith-Served-At"] = metadataServedAt.ToString("O", CultureInfo.InvariantCulture);
        }

        if (projectionBacked && metadata.IsStale is { } isStale) {
            Response.Headers["X-Hexalith-Is-Stale"] = isStale ? "true" : "false";
        }

        if (metadata.IsDegraded is { } isDegraded) {
            Response.Headers["X-Hexalith-Is-Degraded"] = isDegraded ? "true" : "false";
        }

        if (warningCodes is not null) {
            Response.Headers["X-Hexalith-Warning-Codes"] = warningCodes;
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
        string correlationId,
        bool hasProducerETag) {
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

        if (analysis.ProjectionType is null && !hasProducerETag) {
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
        if (!TryGetConditionalValidators(ifNoneMatch, out List<string> validators)) {
            return new HeaderProjectionTypeAnalysis(null, false);
        }

        string? firstProjectionType = null;
        foreach (string validator in validators) {
            if (SelfRoutingETag.TryDecode(validator.Trim('"'), out string? projectionType, out _)) {
                if (firstProjectionType is null) {
                    firstProjectionType = projectionType;
                }
                else if (!string.Equals(firstProjectionType, projectionType, StringComparison.Ordinal)) {
                    return new HeaderProjectionTypeAnalysis(null, true);
                }
            }
        }

        return new HeaderProjectionTypeAnalysis(firstProjectionType, false);
    }

    private static bool ETagMatches(string? ifNoneMatch, string currentETag, bool hasProducerETag) {
        if (ifNoneMatch is null || !TryGetConditionalValidators(ifNoneMatch, out List<string> validators)) {
            return false;
        }

        foreach (string validator in validators) {
            string candidate = validator.Trim('"');
            if ((hasProducerETag && validator.StartsWith('"') || SelfRoutingETag.TryDecode(candidate, out _, out _))
                && string.Equals(candidate, currentETag, StringComparison.Ordinal)) {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetConditionalValidators(string ifNoneMatch, out List<string> validators) {
        validators = [];
        ReadOnlySpan<char> header = ifNoneMatch.AsSpan().Trim();
        int start = 0;
        bool inQuotes = false;
        for (int i = 0; i <= header.Length; i++) {
            if (i < header.Length) {
                if (header[i] == '"') {
                    inQuotes = !inQuotes;
                }

                if (header[i] != ',' || inQuotes) {
                    continue;
                }
            }

            string candidate = header[start..i].Trim().ToString();
            if (validators.Count == MaxIfNoneMatchValues || candidate.Length == 0 || inQuotes) {
                return false;
            }

            if (candidate.StartsWith('"')) {
                if (!EntityTagHeaderValue.TryParse(candidate, out EntityTagHeaderValue? parsed)
                    || parsed.IsWeak || parsed.Tag == "*") {
                    return false;
                }
            }
            else if (candidate == "*" || candidate.StartsWith("W/", StringComparison.OrdinalIgnoreCase)
                || candidate.Any(static character => character == '"' || char.IsWhiteSpace(character) || char.IsControl(character))) {
                return false;
            }

            validators.Add(candidate);
            start = i + 1;
        }

        // Validate the entire bounded list before decoding any projection or matching a candidate.
        return validators.All(static validator => validator.StartsWith('"')
            || SelfRoutingETag.TryDecode(validator, out _, out _));
    }

    private static string? GetLosslessProjectionVersionHeader(string? version)
        => !string.IsNullOrEmpty(version)
            && version.Length <= MaxMetadataHeaderValueLength
            && !char.IsWhiteSpace(version[0])
            && !char.IsWhiteSpace(version[^1])
            && !version.Any(static character => char.IsControl(character) || character > 0x7e)
                ? version
                : null;

    private static string? FormatWarningCodes(IReadOnlyList<string>? warningCodes) {
        if (warningCodes is null || warningCodes.Count == 0 || warningCodes.Count > MaxWarningCodeCount
            || warningCodes.Any(static code => string.IsNullOrWhiteSpace(code)
                || code.Length > MaxMetadataHeaderValueLength
                || code.Any(static character => !(character is >= 'A' and <= 'Z' or >= 'a' and <= 'z'
                    or >= '0' and <= '9' or '-' or '_' or '.')))) {
            return null;
        }

        string value = string.Join(",", warningCodes);
        return value.Length <= MaxMetadataHeaderValueLength ? value : null;
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
