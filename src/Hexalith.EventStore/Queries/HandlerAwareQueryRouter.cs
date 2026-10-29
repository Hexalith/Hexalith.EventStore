using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;

namespace Hexalith.EventStore.Queries;

/// <summary>
/// <see cref="IQueryRouter"/> decorator implementing capability-declared query routing. When the target
/// domain advertises an <c>IDomainQueryHandler</c> for the query type (per
/// <see cref="IDomainQueryHandlerRegistry"/>), the query is invoked against the domain service's
/// <c>/query</c> endpoint via <see cref="IDomainQueryInvoker"/>; otherwise it delegates to the wrapped
/// projection-actor router. Handler-based queries do not participate in projection ETag caching (they
/// compute fresh results) unless the handler explicitly returns a projection type together with
/// projection-backed response metadata.
/// </summary>
/// <remarks>
/// The wrapped <paramref name="inner"/> is the concrete projection-actor <c>QueryRouter</c>, supplied by the
/// DI factory (not resolved as <see cref="IQueryRouter"/>, which would resolve back to this decorator).
/// </remarks>
public sealed class HandlerAwareQueryRouter : IQueryRouter {
    private IQueryRouter inner { get; }
    private IDomainQueryHandlerRegistry registry { get; }
    private IDomainQueryInvoker invoker { get; }
    private ILogger<HandlerAwareQueryRouter> logger { get; }
    private Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority? catalogAuthority { get; }

    /// <summary>Creates a catalog-enforced runtime entry point.</summary>
    public HandlerAwareQueryRouter(
        Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority authority,
        IQueryRouter inner,
        IDomainQueryHandlerRegistry registry,
        IDomainQueryInvoker invoker,
        ILogger<HandlerAwareQueryRouter> logger)
        : this(inner, registry, invoker, logger, authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
    }

    /// <summary>Explicit friend-only composition seam for existing isolated protocol tests.</summary>
    internal HandlerAwareQueryRouter(
        IQueryRouter inner,
        IDomainQueryHandlerRegistry registry,
        IDomainQueryInvoker invoker,
        ILogger<HandlerAwareQueryRouter> logger,
        Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority? catalogAuthority = null)
    {
        this.inner = inner;
        this.registry = registry;
        this.invoker = invoker;
        this.logger = logger;
        this.catalogAuthority = catalogAuthority;

    }

    /// <inheritdoc/>
    public async Task<QueryRouterResult> RouteQueryAsync(SubmitQuery query, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var catalogLease = catalogAuthority is null ? null : await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease).ConfigureAwait(false);
        var selected = catalogLease?.Snapshot.Resolve("query", query.Tenant, query.Domain, query.QueryType);
        if (selected is { } committed)
        {
            if (committed.Declaration.Execution is not { } execution)
                throw new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException("The committed declaration has no execution metadata.");
            if ((!string.IsNullOrWhiteSpace(query.ProjectionType) && query.ProjectionType != committed.Declaration.ProjectionType)
                || (!string.IsNullOrWhiteSpace(query.ProjectionActorType) && query.ProjectionActorType != execution.ActorType))
                throw new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException("Caller-selected projection or actor differs from the committed query target.");
            if (execution.RequiresDelegatedContext && query.CallerAuthority is null)
                throw new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException("The committed query requires delegated caller authority.");
        }

        bool handlerBased = selected is { } route
            ? (route.Declaration.Execution ?? throw new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException("The committed declaration has no execution metadata.")).TargetKind == "domain-service"
            : await registry
            .SupportsQueryAsync(query.Domain, query.QueryType, cancellationToken)
            .ConfigureAwait(false);

        if (!handlerBased) {
            return await inner.RouteQueryAsync(query, cancellationToken).ConfigureAwait(false);
        }

        logger.LogDebug(
            "Routing query to domain handler endpoint: CorrelationId={CorrelationId}, Tenant={TenantId}, Domain={Domain}, QueryType={QueryType}, Stage=QueryHandlerRouting",
            query.CorrelationId,
            query.Tenant,
            query.Domain,
            query.QueryType);

        var envelope = new QueryEnvelope(
            query.Tenant,
            query.Domain,
            query.AggregateId,
            query.QueryType,
            query.Payload,
            query.CorrelationId,
            query.UserId,
            query.EntityId,
            query.IsGlobalAdmin,
            query.Paging,
            query.OriginalActorId,
            query.AuthenticatedWorkloadId,
            query.IsDelegated,
            query.Scopes,
            query.Audience,
            query.DelegationId) { IdentityAdmissionProof = query.IdentityAdmissionProof, CallerAuthority = query.CallerAuthority };

        QueryResult result = await invoker.InvokeAsync(envelope, cancellationToken).ConfigureAwait(false);
        if (selected is { } declared && result.ProjectionType is not null && result.ProjectionType != declared.Declaration.ProjectionType)
            throw new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException("The handler response names an undeclared projection output.");

        if (!result.Success) {
            return result.ErrorMessage == "safe-denial"
                ? new QueryRouterResult(Success: false, Payload: null, NotFound: true)
                : new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: result.ErrorMessage, Metadata: result.Metadata);
        }

        if (result.PayloadBytes is not { Length: > 0 }) {
            return new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: "Domain query handler returned an empty payload.");
        }

        try {
            JsonElement payload = result.GetPayload();
            bool isProjectionBacked = !string.IsNullOrWhiteSpace(result.ProjectionType)
                && result.Metadata?.Provenance is QueryResponseProvenance.ProjectionBacked
                && (string.IsNullOrWhiteSpace(query.ProjectionType)
                    || string.Equals(query.ProjectionType, result.ProjectionType, StringComparison.Ordinal));
            return new QueryRouterResult(
                Success: true,
                Payload: payload,
                NotFound: false,
                ProjectionType: isProjectionBacked ? result.ProjectionType : null,
                Metadata: isProjectionBacked ? result.Metadata : StampHandlerComputed(result.Metadata));
        }
        catch (JsonException) {
            return new QueryRouterResult(Success: false, Payload: null, NotFound: false, ErrorMessage: "Domain query handler returned a malformed payload.");
        }
    }

    private static QueryResponseMetadata StampHandlerComputed(QueryResponseMetadata? metadata) {
        QueryResponseMetadata source = metadata ?? new QueryResponseMetadata();
        ProjectionLifecycleState lifecycle = ProjectionLifecyclePolicy.Normalize(
            source.Lifecycle,
            QueryResponseProvenance.HandlerComputed);
        return source with {
            Provenance = QueryResponseProvenance.HandlerComputed,
            Lifecycle = lifecycle,
            IsStale = ProjectionLifecyclePolicy.ProjectIsStale(lifecycle, source.IsStale),
            IsDegraded = ProjectionLifecyclePolicy.ProjectIsDegraded(lifecycle, source.IsDegraded),
        };
    }
}
