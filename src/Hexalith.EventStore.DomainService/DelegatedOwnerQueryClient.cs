using System.Net;
using System.Net.Http.Json;
using Dapr.Client;
using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.DomainService;

/// <summary>Requests narrowly configured owner evidence while preserving the original gateway-verified actor authority.</summary>
public sealed class DelegatedOwnerQueryClient(DaprClient dapr, IHttpClientFactory clients, IWorkloadAssertionIssuer issuer,
    IHttpContextAccessor accessor, IConfiguration configuration, TimeProvider clock)
{
    /// <summary>Sends an exact owner request through the existing signed bound-owner gateway route.</summary>
    public async Task<QueryResult> InvokeAsync(SubmitQueryRequest query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        HttpContext? context = accessor.HttpContext;
        VerifiedQueryCaller? caller = VerifiedQueryCaller.Get(context);
        string? workload = configuration["Authentication:WorkloadIssuer:Workload"];
        string? key = configuration["Authentication:BoundStreamRead:PrivateKey"];
        if (caller?.Query.CallerAuthority?.Origin != "authenticated-caller" || query.Tenant != caller.Query.TenantId
            || context!.Request.Headers[EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName].Count != 1)
            return QueryResult.Failure("safe-denial");
        if (string.IsNullOrWhiteSpace(workload) || string.IsNullOrWhiteSpace(key))
            return QueryResult.Failure(QueryAdapterFailureReason.ActorException);
        var request = new BoundOwnerQueryRequest(query, caller.Query.UserId)
        {
            OriginatingQuery = caller.OriginalQuery,
            OriginatingAssertion = context.Request.Headers[EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName][0],
        };
        string gateway = configuration["Authentication:QueryAuthority:GatewayWorkloadId"] ?? "eventstore";
        string? assertion = await issuer.IssueAsync(new(gateway, EventStoreWorkloadOperations.GatewayOwnerAuthorityQuery), cancellationToken).ConfigureAwait(false);
        if (assertion is null) return QueryResult.Failure(QueryAdapterFailureReason.ActorException);
        using HttpRequestMessage message = dapr.CreateInvokeMethodRequest(gateway, "api/v1/queries/owner-authority/workload", request);
        message.Headers.TryAddWithoutValidation(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
        message.Headers.TryAddWithoutValidation(BoundOwnerQueryProof.HeaderName, BoundOwnerQueryProof.Issue(request, workload, key, clock.GetUtcNow()));
        using HttpResponseMessage response = await clients.CreateClient().SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return QueryResult.Failure("safe-denial");
        if (!response.IsSuccessStatusCode) return QueryResult.Failure(QueryAdapterFailureReason.ActorException);
        SubmitQueryResponse? result = await response.Content.ReadFromJsonAsync<SubmitQueryResponse>(cancellationToken).ConfigureAwait(false);
        return result?.Success == true ? QueryResult.FromPayload(result.Payload, query.QueryType)
            : QueryResult.Failure(QueryAdapterFailureReason.ActorResponseMismatch);
    }
}
