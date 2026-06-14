using Dapr.Client;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Server.DomainServices;

namespace Hexalith.EventStore.Queries;

/// <summary>
/// DAPR-based <see cref="IDomainQueryInvoker"/>. Resolves the domain service registration (for its DAPR
/// app id) and invokes its <c>/query</c> endpoint via DAPR service invocation — the query-side counterpart
/// of <c>DaprDomainServiceInvoker</c>. No custom retry; DAPR resiliency owns transient failures.
/// </summary>
public sealed class DaprDomainQueryInvoker(
    DaprClient daprClient,
    IHttpClientFactory httpClientFactory,
    IDomainServiceResolver resolver,
    ILogger<DaprDomainQueryInvoker> logger) : IDomainQueryInvoker {
    /// <summary>The domain-service method name for handler-based queries (the SDK's <c>/query</c> endpoint).</summary>
    public const string QueryMethodName = "query";

    /// <inheritdoc/>
    public async Task<QueryResult> InvokeAsync(QueryEnvelope query, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);

        DomainServiceRegistration? registration = await resolver
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
                QueryMethodName,
                query);
            HttpClient httpClient = httpClientFactory.CreateClient();
            using HttpResponseMessage httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            _ = httpResponse.EnsureSuccessStatusCode();
            QueryResult? result = await httpResponse.Content
                .ReadFromJsonAsync<QueryResult>(cancellationToken)
                .ConfigureAwait(false);
            return result ?? QueryResult.Failure($"Null /query response from domain service '{registration.AppId}'.");
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            logger.LogError(
                ex,
                "Domain query invocation failed: AppId={AppId}, Tenant={TenantId}, Domain={Domain}, QueryType={QueryType}, CorrelationId={CorrelationId}",
                registration.AppId,
                query.TenantId,
                query.Domain,
                query.QueryType,
                query.CorrelationId);
            return QueryResult.Failure($"Domain query invocation failed for domain '{query.Domain}': {ex.InnerException?.Message ?? ex.Message}");
        }
    }
}
