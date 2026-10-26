using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Sample.Counter.Commands;
using Hexalith.EventStore.Sample.Counter.Queries;

namespace Hexalith.EventStore.Sample.Counter;

/// <summary>Owner-approved v1 intent policies and v2 route declarations; no runtime activation.</summary>
public static class CounterCatalogDeclarations
{
    /// <summary>Gets all four owner-approved MessageId v1, Mutation/24-hour policies.</summary>
    public static IReadOnlyList<CommandIntentDeclaration> Commands { get; } = [
        Command<IncrementCounter>(), Command<DecrementCounter>(), Command<ResetCounter>(), Command<CloseCounter>()];

    /// <summary>Gets the query that uses EventStore's ProjectionActor, not a Sample HTTP handler.</summary>
    public static RouteContractDeclaration Query { get; } = new("counter-status-query", "query", GetCounterStatusQuery.Domain,
        GetCounterStatusQuery.QueryType, GetCounterStatusQuery.ProjectionType, "v1", null, "workload", EventStoreOperationIdentifiers.ProjectionQuery, null, [],
        new(typeof(GetCounterStatusQuery).FullName!, "exact", [GetCounterStatusQuery.QueryType], "projection-actor", "projection", [], ActorType: "ProjectionActor"));

    private static CommandIntentDeclaration Command<T>() where T : ICommandContract
    {
        string operation = T.CommandType;
        return new(new("counter-" + operation, "command", T.Domain, T.CommandType, null, "v1", 1,
            "workload", EventStoreOperationIdentifiers.DomainServiceProcess, null, [],
            new(typeof(T).FullName!, "clr-short-name-v1", [T.CommandType, typeof(T).Name], "domain-service", null, [])),
            $"hexalith:counter:{operation}:intent-v1", $"counter:{operation}", $"counter:{operation}:policy-v1",
            new($"hexalith:counter:{operation}:descriptor-v1", 1, [new("target", "string")]), IdempotencyReplayRetentionTier.Mutation, false);
    }
}
