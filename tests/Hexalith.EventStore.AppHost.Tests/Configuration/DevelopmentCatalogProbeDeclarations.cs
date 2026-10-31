using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;

[assembly: RouteCatalogDeclarationSource(typeof(Hexalith.EventStore.AppHost.Tests.Configuration.DevelopmentCatalogProbeDeclarations))]
[assembly: RouteCatalogAdapterSource(typeof(Hexalith.EventStore.AppHost.Tests.Configuration.DevelopmentCatalogProbeDeclarations))]

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

/// <summary>Complete compiled declarations, schemas, policies, receiver methods and adapters for a model proof.</summary>
public static class DevelopmentCatalogProbeDeclarations
{
    /// <summary>The explicit owner command and descriptor policy.</summary>
    public static IReadOnlyList<CommandIntentDeclaration> Commands { get; } = [new(new("catalog-probe-record", "command", DevelopmentCatalogProbeCommand.Domain,
        DevelopmentCatalogProbeCommand.CommandType, null, "v1", 1, "workload", EventStoreOperationIdentifiers.DomainServiceProcess, null, [],
        new(typeof(DevelopmentCatalogProbeCommand).FullName!, "exact", [DevelopmentCatalogProbeCommand.CommandType], "domain-service", null, [])),
        "catalog-probe-adapter", "record-probe", "catalog-probe-policy-v1", new("catalog-probe-descriptor", 1, [new("target", "string")]), IdempotencyReplayRetentionTier.Mutation, false)];
    /// <summary>Every callable owner operation.</summary>
    public static IReadOnlyList<RouteContractDeclaration> Routes => Commands.Select(command => command.Route).ToArray();
    /// <summary>Actual typed schemas and public eligibility.</summary>
    public static IReadOnlyList<RouteOperationContract> Operations { get; } = [RouteOperationSchema.Create(Routes[0], typeof(DevelopmentCatalogProbeCommand),
        typeof(string), "public", "agent-eligible", new(System.Text.Json.JsonSerializerDefaults.Web))];
    /// <summary>Complete actual receiver method inventory.</summary>
    public static IReadOnlyDictionary<string, string> ReceiverMethods { get; } = new Dictionary<string, string> { [Routes[0].RouteId] = "process" };
    /// <summary>The same real owner policy exposed through the normal SDK adapter factory.</summary>
    public static IReadOnlyList<IDeclaredIdempotencyIntentAdapter> Create() => [new DeclaredIdempotencyIntentAdapter<DevelopmentCatalogProbeCommand>(Commands[0],
        command => new Dictionary<string, object?> { ["target"] = command.AggregateId })];
}
