using Dapr.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.CatalogConstruction;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.Tenants.Server.Routing;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

public class DevelopmentRouteCatalogParityTests
{
    [Fact]
    public void MissingOrAmbiguousActualCommandInventoryRejectsConstruction()
    {
        Type[] aggregates = [typeof(CounterAggregate), typeof(Hexalith.EventStore.Sample.Greeting.GreetingAggregate),
            typeof(Hexalith.Tenants.Server.Aggregates.TenantAggregate), typeof(Hexalith.Tenants.Server.Aggregates.GlobalAdministratorsAggregate)];
        IDeclaredIdempotencyIntentAdapter[] adapters = [.. SampleCatalog.CreateAdapters(), .. TenantIntentAdapters.Create()];
        DevelopmentCatalogProfile.ValidateCommands(adapters, aggregates);
        Should.Throw<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.ValidateCommands(adapters, aggregates.Skip(1).ToArray()));
        Should.Throw<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.ValidateCommands(adapters, [.. aggregates, aggregates[0]]));
        Should.Throw<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.ValidateCommands(adapters.Skip(1).ToArray(), aggregates));
    }

    [Fact]
    public async Task CompiledOwnersGenerateStableCompleteCatalogAndCheckedInBytes()
    {
        string root = Root();
        RouteCatalogEnvelope first = await DevelopmentCatalogProfile.BuildAsync(root, TestContext.Current.CancellationToken);
        RouteCatalogEnvelope second = await DevelopmentCatalogProfile.BuildAsync(root, TestContext.Current.CancellationToken);
        RouteCatalogCodec.Encode(first).ShouldBe(RouteCatalogCodec.Encode(second));
        first.SchemaVersion.ShouldBe(2);
        first.Idempotency.Count.ShouldBe(17);
        first.Declarations.Count(d => d.Kind == "query").ShouldBe(9);
        first.Declarations.Count(d => d.Kind == "projection").ShouldBe(3);
        first.Routes.Single(r => r.RouteId == "counter-status-query").AppId.ShouldBe("eventstore");
        first.Routes.Single(r => r.RouteId == "counter-status-query").Method.ShouldBe("QueryAsync");
        first.Idempotency.All(i => i.KeyMap.All(k => k.MapKey == k.Generation)).ShouldBeTrue();
        RouteCatalogCodec.Encode(first).ShouldBe(File.ReadAllBytes(Path.Combine(root, "deploy/dapr/eventstore-routing-catalog.json")));
    }

    [Theory]
    [InlineData("missing-query")]
    [InlineData("duplicate-query")]
    [InlineData("missing-projection")]
    [InlineData("changed-output")]
    public async Task MissingAmbiguousOrChangedActualCapabilitiesRejectConstruction(string change)
    {
        DevelopmentCatalogCapabilities valid = DevelopmentCatalogProfile.ObserveCapabilities();
        DevelopmentCatalogProfile.ValidateCapabilities(DevelopmentCatalogProfile.Declarations, valid);
        DevelopmentCatalogCapabilities invalid = change switch
        {
            "missing-query" => valid with { HandlerQueries = valid.HandlerQueries.Skip(1).ToArray() },
            "duplicate-query" => valid with { HandlerQueries = [.. valid.HandlerQueries, valid.HandlerQueries[0]] },
            "missing-projection" => valid with { Projections = valid.Projections.Skip(1).ToArray() },
            _ => valid with { Projections = [valid.Projections[0] with { Execution = valid.Projections[0].Execution! with { ProjectionOutputs = ["changed"] } }, .. valid.Projections.Skip(1)] },
        };
        await Should.ThrowAsync<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.BuildAsync(Root(), TestContext.Current.CancellationToken, invalid));
    }

    [Theory]
    [InlineData("counter", "increment-counter")]
    [InlineData("greeting", "SendGreeting")]
    [InlineData("global-administrators", "bootstrap-global-admin")]
    public void RegistryResolvesLegacyQualifiedAliasesWithinTheirDeclaredDomain(string domain, string commandType)
    {
        IDeclaredIdempotencyIntentAdapter[] adapters = [.. SampleCatalog.CreateAdapters(), .. TenantIntentAdapters.Create()];
        var registry = new IdempotencyIntentAdapterRegistry(adapters, new CanonicalIdempotencyIntentEncoder());
        IDeclaredIdempotencyIntentAdapter adapter = adapters.Single(a => a.Declaration.Route.Domain == domain && a.CommandType == commandType);
        var command = new SubmitCommand("id-1", "system", domain, "target-1", commandType,
            "{\"userId\":\"actor-1\"}"u8.ToArray(), "correlation", "untrusted-wire-user") { AuthenticatedSubject = "actor-1" };
        byte[] expected = registry.Resolve(command).CanonicalIntent;
        foreach (string key in adapter.Declaration.Route.Execution!.Aliases)
        {
            registry.Resolve(command with { CommandType = "Namespace." + key + ", Assembly" }).CanonicalIntent.ShouldBe(expected);
        }
        Should.Throw<InvalidOperationException>(() => registry.Resolve(command with { Domain = "other" }));
        if (adapter.Declaration.RequiresAuthenticatedSubject)
        {
            Should.Throw<InvalidOperationException>(() => registry.Resolve(command with { AuthenticatedSubject = null, Extensions = new() { ["subject"] = "actor-1" } }));
            registry.Resolve(command with { AuthenticatedSubject = "actor-2" }).CanonicalIntent.ShouldNotBe(expected);
        }
    }

    [Fact]
    public void LegacyRegistrationCannotAmbiguouslyOverlapDeclaredAliases()
    {
        IDeclaredIdempotencyIntentAdapter adapter = SampleCatalog.CreateAdapters()[0];
        IIdempotencyIntentAdapter legacy = Substitute.For<IIdempotencyIntentAdapter>();
        legacy.CommandType.Returns(adapter.Declaration.Route.Execution!.Aliases.Last());
        legacy.AdapterId.Returns("legacy"); legacy.OperationId.Returns("legacy"); legacy.DescriptorVersion.Returns(1);
        legacy.RetentionTier.Returns(Contracts.Commands.IdempotencyReplayRetentionTier.Mutation);
        Should.Throw<InvalidOperationException>(() => new IdempotencyIntentAdapterRegistry([adapter, legacy], new CanonicalIdempotencyIntentEncoder()));
    }

    [Fact]
    public async Task CounterQueryUsesActualActorRoutingAndPreservesIdentity()
    {
        var declaration = CounterCatalogDeclarations.Query;
        var actor = Substitute.For<Hexalith.EventStore.Server.Queries.IProjectionActorInvoker>();
        actor.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Contracts.Queries.QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(Contracts.Queries.QueryResult.FromPayload(System.Text.Json.JsonSerializer.SerializeToElement(new { count = 1 }), "counter"));
        var inner = new Hexalith.EventStore.Server.Queries.QueryRouter(actor, Microsoft.Extensions.Logging.Abstractions.NullLogger<Hexalith.EventStore.Server.Queries.QueryRouter>.Instance);
        var capabilities = Substitute.For<Hexalith.EventStore.Queries.IDomainQueryHandlerRegistry>();
        var http = Substitute.For<Hexalith.EventStore.Queries.IDomainQueryInvoker>();
        var router = new Hexalith.EventStore.Queries.HandlerAwareQueryRouter(inner, capabilities, http,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Hexalith.EventStore.Queries.HandlerAwareQueryRouter>.Instance);
        var query = new Hexalith.EventStore.Server.Pipeline.Queries.SubmitQuery("tenant-a", "counter", "counter-1", declaration.MessageType!, "{}"u8.ToArray(), "correlation", "actor-1",
            EntityId: "counter-1", ProjectionType: declaration.ProjectionType);
        await router.RouteQueryAsync(query, TestContext.Current.CancellationToken);
        await actor.Received(1).InvokeAsync("counter:tenant-a:counter-1", declaration.Execution!.ActorType!,
            Arg.Is<Contracts.Queries.QueryEnvelope>(envelope => envelope.TenantId == "tenant-a" && envelope.AggregateId == "counter-1" && envelope.QueryType == declaration.MessageType), Arg.Any<CancellationToken>());
        await http.DidNotReceiveWithAnyArgs().InvokeAsync(default!, default);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("dynamic")]
    [InlineData("extra-scope")]
    [InlineData("extra-exact-scope")]
    [InlineData("extra-version")]
    public async Task EffectiveConfigurationDriftRejectsProfile(string change)
    {
        string temporary = Path.Combine(Path.GetTempPath(), "catalog-profile-" + Guid.NewGuid().ToString("N"));
        string directory = Path.Combine(temporary, "src/Hexalith.EventStore");
        Directory.CreateDirectory(directory);
        try
        {
            File.Copy(Path.Combine(Root(), "src/Hexalith.EventStore/appsettings.json"), Path.Combine(directory, "appsettings.json"));
            var development = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(Root(), "src/Hexalith.EventStore/appsettings.Development.json")))!;
            var services = development["EventStore"]!["DomainServices"]!;
            if (change == "dynamic") services["ConfigStoreName"] = "runtime-config";
            else if (change == "target") services["Registrations"]!["*|counter|v1"]!["AppId"] = "different-app";
            else if (change == "extra-version")
            {
                services["Registrations"]!["*|counter|v2"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["AppId"] = "sample", ["MethodName"] = "process", ["TenantId"] = "*", ["Domain"] = "counter", ["Version"] = "v2",
                };
            }
            else
            {
                string tenantPattern = change == "extra-exact-scope" ? "tenant-b" : "*";
                services["Registrations"]![tenantPattern + "|global-administrators|v1"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["AppId"] = "tenants", ["MethodName"] = "process", ["TenantId"] = tenantPattern,
                    ["Domain"] = "global-administrators", ["Version"] = "v1",
                };
            }
            File.WriteAllText(Path.Combine(directory, "appsettings.Development.json"), development.ToJsonString());
            await Should.ThrowAsync<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.BuildAsync(temporary, TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(temporary, recursive: true); }
    }

    [Fact]
    public async Task HistoricalGlobalAdministratorScopeExcludesOtherTenantConventionTargets()
    {
        RouteCatalogEnvelope catalog = await DevelopmentCatalogProfile.BuildAsync(Root(), TestContext.Current.CancellationToken);
        string[] ids = catalog.Declarations.Where(d => d.Domain == "global-administrators").Select(d => d.RouteId).ToArray();
        catalog.Routes.Where(r => ids.Contains(r.RouteId, StringComparer.Ordinal)).ShouldAllBe(r => r.ResolverTenantPattern == "system" && r.AppId == "tenants");
        IConfiguration configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(Root(), "src/Hexalith.EventStore/appsettings.json"))
            .AddJsonFile(Path.Combine(Root(), "src/Hexalith.EventStore/appsettings.Development.json")).Build();
        using DaprClient dapr = new DaprClientBuilder().Build();
        var resolver = new DomainServiceResolver(dapr, Options.Create(configuration.GetSection("EventStore:DomainServices").Get<DomainServiceOptions>()!), NullLogger<DomainServiceResolver>.Instance);
        (await resolver.ResolveAsync("unbound-tenant", "global-administrators", cancellationToken: TestContext.Current.CancellationToken))!.AppId.ShouldBe("global-administrators");
    }

    private static string Root()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root unavailable.");
    }
}
