using System.Text.Json;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.DomainService;

namespace Hexalith.EventStore.Server.Tests.Queries;
/// <summary>Exercises reads through the actual admitted private handler scope.</summary>
/// <param name = "store">The platform-owned scoped read store.</param>
public sealed class DaprLogicalQueryHandler(IReadModelStore store) : IDomainQueryHandler
{
    public string Domain
    {
        get
        {
            DaprLogicalQueryFixture.Current.Callback("domain");
            return "d";
        }
    }

    public string QueryType
    {
        get
        {
            DaprLogicalQueryFixture.Current.Callback("query-type");
            return "get-total";
        }
    }

    public async Task<QueryResult> ExecuteAsync(QueryEnvelope query, CancellationToken cancellationToken)
    {
        DaprLogicalQueryFixture fixture = DaprLogicalQueryFixture.Current;
        fixture.HandlerCalls++;
        fixture.Store = store;
        fixture.Callback("handler");
        fixture.PrivateQuery = query;
        if (fixture.DualCancellation)
        {
            using var foreign = new CancellationTokenSource();
            foreign.Cancel();
            fixture.Cancellation.Cancel();
            try
            {
                _ = await store.GetAsync<DaprLogicalQueryValue>("store", "item:a", foreign.Token);
            }
            catch (OperationCanceledException exception)
            {
                fixture.StoreRefusalToken = exception.CancellationToken;
            }

            return new QueryResult(true, []);
        }

        if (fixture.WrongClr)
        {
            _ = await store.GetAsync<object>("store", "item:a", cancellationToken);
        }

        if (fixture.WrongKey)
        {
            _ = await store.GetAsync<DaprLogicalQueryValue>("store", "physical:row", cancellationToken);
        }

        if (fixture.Bulk)
        {
            _ = await ((IReadModelBulkStore)store).GetManyAsync<DaprLogicalQueryValue>("store", new DaprLogicalQueryKeys(fixture), 1, cancellationToken);
        }

        if (fixture.BulkBad)
        {
            _ = await ((IReadModelBulkStore)store).GetManyAsync<DaprLogicalQueryValue>("store", ["item:a", "physical:row"], 1, cancellationToken);
        }

        if (fixture.Write)
        {
            await store.SaveAsync("store", "item:a", new DaprLogicalQueryValue(9), cancellationToken);
        }

        ReadModelEntry<DaprLogicalQueryValue> first = await store.GetAsync<DaprLogicalQueryValue>("store", "item:a", cancellationToken);
        DaprLogicalQueryValue? a = first.Value;
        fixture.ObservedEtags.Add(first.ETag);
        fixture.Observed.Add(a?.Value ?? 0);
        fixture.Callback("between-reads");
        ReadModelEntry<DaprLogicalQueryValue> second = await store.GetAsync<DaprLogicalQueryValue>("store", "item:b", cancellationToken);
        DaprLogicalQueryValue? b = second.Value;
        fixture.ObservedEtags.Add(second.ETag);
        fixture.Observed.Add(b?.Value ?? 0);
        fixture.ProducedBytes = JsonSerializer.SerializeToUtf8Bytes(new { total = (a?.Value ?? 0) + (b?.Value ?? 0) });
        fixture.Returned = true;
        return new QueryResult(true, fixture.ProducedBytes);
    }
}
