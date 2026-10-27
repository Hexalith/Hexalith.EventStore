#if SELECTED
using System.Text.Json;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.DomainService;

namespace PublishedConsumer;

/// <summary>Verifies exact signed actor/workload scope before touching the persisted read model.</summary>
public sealed class AuthorizedCounterQueryHandler(IIdentityAdmissionProof proof, IReadModelStore store) : IDomainQueryHandler
{
    public static int Reads { get; private set; }
    public string Domain => "counter";
    public string QueryType => "get-counter";

    public async Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        var scope = new IdentityAdmissionScope(query.TenantId, query.Domain, query.AggregateId, query.QueryType,
            query.CorrelationId, query.CorrelationId, IdentityAdmissionProof.Digest(query.Payload));
        IdentityAdmissionEvidence? authority = proof.Verify(query.IdentityAdmissionProof, scope);
        if (authority is null || authority.OperatorActorId != query.OriginalActorId || authority.SourceId != query.AuthenticatedWorkloadId
            || !query.IsDelegated || query.DelegationId != "delegation" || query.Scopes?.Contains("counter.read") != true
            || query.Audience?.Contains("eventstore") != true)
            return QueryResult.Failure("protected-admission-refused");
        Reads++;
        CounterReadModel? value = (await store.GetAsync<CounterReadModel>("statestore", "p1r:read-model:" + query.TenantId + ":" + query.AggregateId, cancellationToken)).Value;
        return QueryResult.FromPayload(JsonSerializer.SerializeToElement(new
        {
            value, query.OriginalActorId, query.AuthenticatedWorkloadId, query.IsDelegated, query.DelegationId,
            query.Scopes, query.Audience, query.TenantId, query.CorrelationId, query.IdentityAdmissionProof,
        }), projectionType: "counter");
    }
}
#endif
