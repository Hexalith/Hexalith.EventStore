using Dapr.Client;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using System.Net;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.DomainServices;

namespace Hexalith.EventStore.Queries;

/// <summary>
/// DAPR-based <see cref="IDomainQueryInvoker"/>. Resolves the domain service registration (for its DAPR
/// app id) and invokes its <c>/query</c> endpoint via DAPR service invocation — the query-side counterpart
/// of <c>DaprDomainServiceInvoker</c>. No custom retry; DAPR resiliency owns transient failures.
/// </summary>
/// <remarks>
/// The caller's human bearer is never forwarded to a domain service (FR28). The platform outbound handler attaches
/// EventStore's own short-lived workload assertion for the <c>query</c> operation; the acting user travels only as
/// <see cref="QueryEnvelope.UserId"/>, and any administrator flag is re-verified by the domain-service boundary.
/// </remarks>
public sealed class DaprDomainQueryInvoker : IDomainQueryInvoker {
    private DaprClient daprClient { get; }
    private IHttpClientFactory httpClientFactory { get; }
    private IDomainServiceResolver resolver { get; }
    private ILogger<DaprDomainQueryInvoker> logger { get; }
    private Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority? catalogAuthority { get; }

    /// <summary>Creates a catalog-enforced runtime entry point.</summary>
    public DaprDomainQueryInvoker(
        Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority authority,
        DaprClient daprClient,
        IHttpClientFactory httpClientFactory,
        IDomainServiceResolver resolver,
        ILogger<DaprDomainQueryInvoker> logger)
        : this(daprClient, httpClientFactory, resolver, logger, authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
    }

    /// <summary>Explicit friend-only composition seam for existing isolated protocol tests.</summary>
    internal DaprDomainQueryInvoker(
        DaprClient daprClient,
        IHttpClientFactory httpClientFactory,
        IDomainServiceResolver resolver,
        ILogger<DaprDomainQueryInvoker> logger,
        Hexalith.EventStore.ServiceDefaults.Routing.IRouteCatalogAuthority? catalogAuthority = null)
    {
        this.daprClient = daprClient;
        this.httpClientFactory = httpClientFactory;
        this.resolver = resolver;
        this.logger = logger;
        this.catalogAuthority = catalogAuthority;

    }

    /// <summary>The domain-service method name for handler-based queries (the SDK's <c>/query</c> endpoint).</summary>
    public const string QueryMethodName = "query";

    /// <inheritdoc/>
    public async Task<QueryResult> InvokeAsync(QueryEnvelope query, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        var catalogLease = catalogAuthority is null ? null : await catalogAuthority.AcquireAsync(cancellationToken).ConfigureAwait(false);
        await using var catalogRelease = new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogLeaseReleaser(catalogLease).ConfigureAwait(false);
        var committed = catalogLease?.Snapshot.Resolve("query", query.TenantId, query.Domain, query.QueryType);
        if (committed is { } route)
        {
            if (route.Declaration.Execution is not { } execution)
                throw new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException("The committed declaration has no execution metadata.");
            if (execution.TargetKind != "domain-service" || execution.RequiresDelegatedContext && query.CallerAuthority is null)
                throw new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException("Direct query invocation disagrees with the committed handler target or authority binding.");
        }

        DomainServiceRegistration? registration = committed is { } target
            ? new DomainServiceRegistration(target.Target.AppId, target.Target.Method, query.TenantId, query.Domain, target.Target.ContractVersion)
            : await resolver
            .ResolveAsync(query.TenantId, query.Domain, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (registration is null) {
            return QueryResult.Failure($"No domain service is registered for tenant '{query.TenantId}', domain '{query.Domain}'.");
        }

        try {
            // Invoke the domain service's "query" method (not registration.MethodName, which is the command
            // "process" method). DAPR resiliency policies own retries/circuit-breaker/timeout.
            using HttpRequestMessage httpRequest = daprClient.CreateInvokeMethodRequest(
                registration.AppId,
                committed?.Target.Method ?? QueryMethodName,
                query);
            if (query.CallerAuthority is not null) httpRequest.Options.Set(QueryCallerAuthorityBinding.RequestBindings, QueryCallerAuthorityBinding.Bind(query));
            HttpClient httpClient = httpClientFactory.CreateClient();
            using HttpResponseMessage httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            if (httpResponse.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) return QueryResult.Failure("safe-denial");
            if (committed is not null && httpResponse.StatusCode == HttpStatusCode.ServiceUnavailable)
                throw new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException("The committed query receiver refused catalog authority.");
            if (!httpResponse.IsSuccessStatusCode && httpResponse.StatusCode != HttpStatusCode.BadRequest)
                return QueryResult.Failure(QueryAdapterFailureReason.QueryExecutionFailure);
            QueryResult? result = await httpResponse.Content
                .ReadFromJsonAsync<QueryResult>(cancellationToken)
                .ConfigureAwait(false);
            if (httpResponse.StatusCode == HttpStatusCode.BadRequest)
                return QueryResult.Failure(result?.ErrorMessage is QueryAdapterFailureReason.InvalidEnvelope or QueryAdapterFailureReason.InvalidCursor
                    ? result.ErrorMessage : QueryAdapterFailureReason.QueryExecutionFailure);
            if (committed is { } captured && result is { Success: true } && result.ProjectionType != captured.Declaration.ProjectionType)
                throw new Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException("The query response disagrees with its committed projection identity.");
            return result ?? QueryResult.Failure(QueryAdapterFailureReason.ResponseMismatch);
        }
        catch (Hexalith.EventStore.ServiceDefaults.Routing.RouteCatalogUnavailableException) {
            throw;
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            logger.LogError(
                "Domain query invocation failed: ExceptionType={ExceptionType}, AppId={AppId}, Tenant={TenantId}, Domain={Domain}, QueryType={QueryType}, CorrelationId={CorrelationId}",
                ex.GetType().Name,
                registration.AppId,
                query.TenantId,
                query.Domain,
                query.QueryType,
                query.CorrelationId);
            return QueryResult.Failure(QueryAdapterFailureReason.QueryExecutionFailure);
        }
    }
}
