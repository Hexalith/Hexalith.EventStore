#if SELECTED
using System.Text.Json;
using Dapr.Actors;
using Dapr.Actors.Client;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Queries;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Server.Identity;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Queries;
using Microsoft.AspNetCore.DataProtection;

namespace PublishedConsumer;

/// <summary>Drives real query router/actor/handler routes and cursors bound to a durably applied read model.</summary>
public static class QueryBoundaryEndpoints
{
    /// <summary>Maps the signed query and persisted cursor fixture endpoints.</summary>
    public static void Map(WebApplication app)
    {
        app.MapPost("/project-model/{aggregate}", async (string aggregate, JsonElement input, IReadModelStore store) =>
        {
            long position = input.GetProperty("position").GetInt64();
            long sequence = input.GetProperty("sequence").GetInt64();
            if (position <= 0) return Results.BadRequest();
            var evt = new ProjectionEventDto("CounterIncremented", "{}"u8.ToArray(), "json", sequence, DateTimeOffset.UnixEpoch, "corr", GlobalPosition: position);
            CounterReadModel result = await ReadModelWritePolicy.ApplyEventsAsync(store, "statestore", "p1r:read-model:tenant-a:" + aggregate,
                new ProjectionEventDto?[] { evt }, static () => new CounterReadModel(), static (model, item) =>
                {
                    if (item.GlobalPosition > model.Watermark) { model.Count++; model.Sequence = item.SequenceNumber; model.Watermark = item.GlobalPosition; }
                });
            return Results.Json(result);
        });
        app.MapPost("/query-route/{aggregate}", async (string aggregate, JsonElement input, IIdentityAdmissionSigner signer,
            IActorProxyFactory actors, ILogger<QueryRouter> logger) =>
        {
            var scope = new IdentityAdmissionScope("tenant-a", "counter", aggregate, "get-counter", "corr", "corr", IdentityAdmissionProof.Digest([]));
            DateTimeOffset now = DateTimeOffset.UtcNow;
            string signed = signer.Sign(new IdentityAdmissionEvidence(scope, "workload", "actor", "actor", 1, true, now, now.AddMinutes(4), 1));
            string mode = input.GetProperty("mode").GetString()!;
            var query = new QueryEnvelope("tenant-a", "counter", aggregate, "get-counter", [], "corr", "user", null, false, null,
                "actor", "workload", true, ["counter.read"], ["eventstore"], "delegation") { IdentityAdmissionProof = signed };
            if (mode == "missing") query = query with { IdentityAdmissionProof = null };
            if (mode == "forged") query = query with { IdentityAdmissionProof = signed + "forged" };
            if (mode == "foreign-tenant") query = query with { TenantId = "tenant-b" };
            if (mode == "foreign-actor") query = query with { OriginalActorId = "other" };
            if (input.GetProperty("serializer").GetString() == "xml")
            {
                Hexalith.EventStore.Server.Actors.IDaprProjectionActor actor = actors.CreateActorProxy<Hexalith.EventStore.Server.Actors.IDaprProjectionActor>(new ActorId("counter:" + query.TenantId), "P1RQueryActor");
                QueryResult response = await actor.QueryAsync(query);
                return Results.Json(new { response.Success, payload = response.Success ? response.GetPayload() : (JsonElement?)null, response.ErrorMessage });
            }
            var request = new SubmitQuery(query.TenantId, query.Domain, query.AggregateId, query.QueryType, query.Payload, query.CorrelationId,
                query.UserId, ProjectionType: "counter", ProjectionActorType: "P1RQueryActor", OriginalActorId: query.OriginalActorId,
                AuthenticatedWorkloadId: query.AuthenticatedWorkloadId, IsDelegated: query.IsDelegated, Scopes: query.Scopes,
                Audience: query.Audience, DelegationId: query.DelegationId) { IdentityAdmissionProof = query.IdentityAdmissionProof };
            QueryRouterResult routed = await new QueryRouter(actors, logger).RouteQueryAsync(request);
            return Results.Json(new { routed.Success, payload = routed.Payload, routed.ErrorMessage });
        });
        app.MapPost("/cursor-model/{aggregate}", async (string aggregate, JsonElement input, IReadModelStore store, IDataProtectionProvider keys) =>
        {
            CounterReadModel model = (await store.GetAsync<CounterReadModel>("statestore", "p1r:read-model:tenant-a:" + aggregate)).Value!;
            string mode = input.GetProperty("mode").GetString()!;
            string caller = mode == "foreign-actor" ? "other" : "actor";
            string tenant = mode == "foreign-tenant" ? "tenant-b" : "tenant-a";
            string domain = mode == "foreign-domain" ? "other" : "counter";
            string filter = mode == "changed-filter" ? "other" : "active";
            string scope = QueryCursorScope.Create().Add("tenant", tenant).Add("domain", domain).Add("caller", caller)
                .Add("filter", filter).Add("query", "get-counter").AddProjectionWatermark(model.Watermark).Build();
            var codec = new QueryCursorCodec(keys, "p1r-counter");
            string cursor = input.TryGetProperty("cursor", out JsonElement supplied) ? supplied.GetString()! : codec.Encode("get-counter", scope, model.Watermark.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (mode == "tampered") cursor = cursor[..10] + (cursor[10] == 'A' ? 'B' : 'A') + cursor[11..];
            bool decoded = codec.TryDecode(cursor, mode == "changed-query" ? "other-query" : "get-counter", scope, out string? position, out string? failure);
            return Results.Json(new { cursor, decoded, position, failure, model });
        });
    }
}
#endif
