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
            if (query.CallerAuthority is not null) httpRequest.Options.Set(QueryCallerAuthorityBinding.RequestBindings, QueryCallerAuthorityBinding.Bind(query));
            HttpClient httpClient = httpClientFactory.CreateClient();
            using HttpResponseMessage httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            if (httpResponse.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) return QueryResult.Failure("safe-denial");
            if (!httpResponse.IsSuccessStatusCode && httpResponse.StatusCode != HttpStatusCode.BadRequest)
                return QueryResult.Failure(QueryAdapterFailureReason.ActorException);
            QueryResult? result = await httpResponse.Content
                .ReadFromJsonAsync<QueryResult>(cancellationToken)
                .ConfigureAwait(false);
            if (httpResponse.StatusCode == HttpStatusCode.BadRequest)
                return QueryResult.Failure(result?.ErrorMessage is QueryAdapterFailureReason.InvalidEnvelope or QueryAdapterFailureReason.InvalidCursor
                    ? result.ErrorMessage : QueryAdapterFailureReason.ActorException);
            return result ?? QueryResult.Failure(QueryAdapterFailureReason.ActorResponseMismatch);
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
            return QueryResult.Failure(QueryAdapterFailureReason.ActorException);
        }
    }
}
