using Dapr.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.CatalogConstruction;
using Hexalith.EventStore.Client.Routing;
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
    public void OpenGenericHandleRejectsOfflineInvocability()
    {
        IDeclaredIdempotencyIntentAdapter adapter = SampleCatalog.CreateAdapters().Single(a => a.CommandType == "increment-counter");
        DevelopmentCatalogProfile.ValidateCommands([adapter], [typeof(CancellationCatalogAggregate)]);
        Should.Throw<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.ValidateCommands([adapter], [typeof(OpenGenericCatalogAggregate)]))
            .Diagnostics.Single().Code.ShouldBe("non-invocable-command-handler");
    }

    [Fact]
    public void PartialAssemblyTypeLoadingCannotCertifyCommandOrProjectionInventory()
    {
        System.Reflection.Assembly partial = Substitute.For<System.Reflection.Assembly>();
        partial.GetTypes().Returns(_ => throw new System.Reflection.ReflectionTypeLoadException(
            [typeof(CounterAggregate), null], [new TypeLoadException("never-print-loader-details")]));
        IDeclaredIdempotencyIntentAdapter[] adapters = SampleCatalog.CreateAdapters().Where(a => a.Declaration.Route.Domain == "counter").ToArray();
        foreach (Action check in new Action[]
        {
            () => DevelopmentCatalogProfile.ValidateHostedCommands(adapters, [partial]),
            () => DevelopmentCatalogProfile.ValidateHostedProjections([SampleCatalog.Projection], [partial]),
        })
        {
            RouteCatalogValidationException exception = Should.Throw<RouteCatalogValidationException>(check);
            exception.Diagnostics.Single().Code.ShouldBe("unavailable-complete-assembly-inventory");
            exception.InnerException.ShouldBeNull();
            exception.Message.ShouldNotContain("never-print-loader-details");
        }
    }

    [Fact]
    public void CompiledAggregateInventoryKeepsTheRuntimeExportedTypeBoundary()
    {
        System.Reflection.Assembly owner = Substitute.For<System.Reflection.Assembly>();
        owner.GetTypes().Returns([typeof(CounterAggregate), typeof(OpenGenericCatalogAggregate)]);
        typeof(CounterAggregate).IsVisible.ShouldBeTrue();
        typeof(OpenGenericCatalogAggregate).IsVisible.ShouldBeFalse();
        DevelopmentCatalogProfile.ValidateHostedCommands(SampleCatalog.CreateAdapters().Where(adapter => adapter.Declaration.Route.Domain == "counter").ToArray(), [owner]);
    }

    [Theory]
    [InlineData(typeof(ExtraLegacyCatalogProjection))]
    [InlineData(typeof(ExtraAsyncCatalogProjection))]
    [InlineData(typeof(ExtraSdkCatalogProjection))]
    public void EveryHostedProjectionShapeMustHaveADeclaration(Type extra)
    {
        ArgumentNullException.ThrowIfNull(extra);
        System.Reflection.Assembly owner = typeof(CounterAggregate).Assembly;
        DevelopmentCatalogProfile.ValidateHostedProjections([SampleCatalog.Projection], [owner]);
        System.Reflection.Assembly added = Substitute.For<System.Reflection.Assembly>();
        added.GetTypes().Returns([extra]);
        Should.Throw<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.ValidateHostedProjections([SampleCatalog.Projection], [owner, added]))
            .Diagnostics.Single().Code.ShouldBe("actual-projection-handler-inventory-drift");
        RouteContractDeclaration declaredExtra = SampleCatalog.Projection with
        {
            Domain = "extra", ProjectionType = "extra",
            Execution = SampleCatalog.Projection.Execution! with { HandlerType = extra.FullName!, Aliases = ["extra"], ProjectionOutputs = ["extra"] },
        };
        DevelopmentCatalogProfile.ValidateHostedProjections([declaredExtra], [added]);
        Should.Throw<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.ValidateHostedProjections([], [owner]));
        Should.Throw<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.ValidateHostedProjections([SampleCatalog.Projection], []));
    }

    [Fact]
    public void CustomDispatcherSdkOutputInventoryRejectsMissingExtraAndWrongDomains()
    {
        var declarations = TenantCatalogCapabilities.Projections.Declarations;
        (Type Type, string Output)[] outputs = [(typeof(Hexalith.Tenants.Server.Projections.TenantProjection), "tenants"),
            (typeof(Hexalith.Tenants.Server.Projections.TenantIndexProjection), "tenant-index"),
            (typeof(Hexalith.Tenants.Server.Projections.GlobalAdministratorProjection), "global-administrators")];
        System.Reflection.Assembly[] owners = [typeof(Hexalith.Tenants.Server.Aggregates.TenantAggregate).Assembly];
        DevelopmentCatalogProfile.ValidateHostedSdkProjectionOutputs(declarations, outputs, owners);
        Should.Throw<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.ValidateHostedSdkProjectionOutputs(declarations, outputs.Skip(1).ToArray(), owners));
        Should.Throw<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.ValidateHostedSdkProjectionOutputs(declarations, [.. outputs, outputs[0]], owners));
        Should.Throw<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.ValidateHostedSdkProjectionOutputs(declarations, [(outputs[0].Type, "wrong-domain"), .. outputs.Skip(1)], owners));
        Should.Throw<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.ValidateHostedSdkProjectionOutputs([], outputs, owners));
        foreach (Type extra in new[] { typeof(ExtraLegacyCatalogProjection), typeof(ExtraAsyncCatalogProjection), typeof(ExtraSdkCatalogProjection) })
        {
            System.Reflection.Assembly added = Substitute.For<System.Reflection.Assembly>();
            added.GetTypes().Returns([extra]);
            Should.Throw<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.ValidateHostedSdkProjectionOutputs(declarations, outputs, [.. owners, added]));
        }
    }

    [Theory]
    [InlineData("increment-counter")]
    [InlineData("decrement-counter")]
    [InlineData("reset-counter")]
    [InlineData("close-counter")]
    public async Task InvalidHandleSignaturesFailCatalogAndActualDispatch(string commandType)
    {
        IDeclaredIdempotencyIntentAdapter adapter = SampleCatalog.CreateAdapters().Single(a => a.CommandType == commandType);
        DevelopmentCatalogProfile.ValidateCommands(SampleCatalog.CreateAdapters().Where(a => a.Declaration.Route.Domain == "counter").ToArray(), [typeof(CounterAggregate)]);
        Should.Throw<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.ValidateCommands([adapter], [typeof(InvalidCatalogAggregate)]));
        var command = new Contracts.Commands.CommandEnvelope("id-1", "tenant-a", "counter", "counter-1", commandType, "{}"u8.ToArray(), "corr-1", null, "actor-1", null);
        await Should.ThrowAsync<InvalidOperationException>(() => new InvalidCatalogAggregate().ProcessAsync(command, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancellationOverloadAndEveryAliasAgreeWithActualDispatch()
    {
        IDeclaredIdempotencyIntentAdapter adapter = SampleCatalog.CreateAdapters().Single(a => a.CommandType == "increment-counter");
        DevelopmentCatalogProfile.ValidateCommands([adapter], [typeof(CancellationCatalogAggregate)]);
        var aggregate = new CancellationCatalogAggregate();
        foreach (string alias in adapter.Declaration.Route.Execution!.Aliases)
        {
            foreach (string wire in new[] { alias, "Namespace." + alias, "Namespace." + alias + ", Assembly" })
            {
                var command = new Contracts.Commands.CommandEnvelope("id-1", "tenant-a", "counter", "counter-1", wire, "{}"u8.ToArray(), "corr-1", null, "actor-1", null);
                (await aggregate.ProcessAsync(command, null, TestContext.Current.CancellationToken)).IsNoOp.ShouldBeTrue();
                aggregate.ObservedToken.ShouldBe(TestContext.Current.CancellationToken);
                aggregate.ObservedWireType.ShouldBe(wire);
            }
        }
        var unsupported = new Contracts.Commands.CommandEnvelope("id-1", "tenant-a", "counter", "counter-1", "Bogus", "{}"u8.ToArray(), "corr-1", null, "actor-1", null);
        await Should.ThrowAsync<InvalidOperationException>(() => aggregate.ProcessAsync(unsupported, null, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("extra-alias")]
    [InlineData("missing-alias")]
    [InlineData("changed-alias")]
    [InlineData("domain")]
    public void DeclaredAliasesAndDomainMustMatchTheEffectiveDispatcher(string change)
    {
        IDeclaredIdempotencyIntentAdapter original = SampleCatalog.CreateAdapters().Single(a => a.CommandType == "increment-counter");
        DevelopmentCatalogProfile.ValidateCommands([original], [typeof(CancellationCatalogAggregate)]);
        RouteContractDeclaration route = original.Declaration.Route;
        RouteExecutionDeclaration execution = route.Execution!;
        route = change == "domain" ? route with { Domain = "other" } : route with
        {
            Execution = execution with { Aliases = change switch
            {
                "extra-alias" => [.. execution.Aliases, "Bogus"],
                "missing-alias" => [route.MessageType!],
                _ => [route.MessageType!, "Bogus"],
            } },
        };
        IDeclaredIdempotencyIntentAdapter altered = Substitute.For<IDeclaredIdempotencyIntentAdapter>();
        altered.Declaration.Returns(original.Declaration with { Route = route });
        altered.CommandType.Returns(original.CommandType); altered.AdapterId.Returns(original.AdapterId);
        altered.OperationId.Returns(original.OperationId); altered.DescriptorVersion.Returns(original.DescriptorVersion); altered.RetentionTier.Returns(original.RetentionTier);
        Should.Throw<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.ValidateCommands([altered], [typeof(CancellationCatalogAggregate)]));
    }

    [Fact]
    public void NewlyHostedAggregateCannotEscapeCompleteAssemblyDiscovery()
    {
        IDeclaredIdempotencyIntentAdapter[] adapters = [.. SampleCatalog.CreateAdapters(), .. TenantIntentAdapters.Create()];
        System.Reflection.Assembly[] owners = [typeof(CounterAggregate).Assembly, typeof(Hexalith.Tenants.Server.Aggregates.TenantAggregate).Assembly];
        DevelopmentCatalogProfile.ValidateHostedCommands(adapters, owners);
        var extra = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(new("CatalogInventoryProbe"), System.Reflection.Emit.AssemblyBuilderAccess.Run);
        var type = extra.DefineDynamicModule("probe").DefineType("UndeclaredAggregate", System.Reflection.TypeAttributes.Public,
            typeof(Hexalith.EventStore.Client.Aggregates.EventStoreAggregate<Hexalith.EventStore.Sample.Counter.State.CounterState>));
        var method = type.DefineMethod("Handle", System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.Static,
            typeof(Contracts.Results.DomainResult), [typeof(Hexalith.EventStore.Sample.Counter.Commands.IncrementCounter), typeof(Hexalith.EventStore.Sample.Counter.State.CounterState)]);
        var code = method.GetILGenerator();
        code.Emit(System.Reflection.Emit.OpCodes.Call, typeof(Contracts.Results.DomainResult).GetMethod("NoOp")!);
        code.Emit(System.Reflection.Emit.OpCodes.Ret);
        _ = type.CreateType();
        Should.Throw<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.ValidateHostedCommands(adapters, [.. owners, extra]));
    }

    [Fact]
    public void RegistrySnapshotsMatchingBeforeCallerMetadataMutations()
    {
        CommandIntentDeclaration original = SampleCatalog.Commands.Single(a => a.Route.MessageType == "increment-counter");
        var aliases = original.Route.Execution!.Aliases.ToList();
        CommandIntentDeclaration mutable = original with { Route = original.Route with { Execution = original.Route.Execution with { Aliases = aliases } } };
        IDeclaredIdempotencyIntentAdapter adapter = Substitute.For<IDeclaredIdempotencyIntentAdapter>();
        adapter.Declaration.Returns(mutable); adapter.CommandType.Returns(original.Route.MessageType!);
        adapter.AdapterId.Returns(original.AdapterId); adapter.OperationId.Returns(original.OperationId);
        adapter.DescriptorVersion.Returns(original.DescriptorSchema.Version); adapter.RetentionTier.Returns(original.RetentionTier);
        adapter.CreateIntent(Arg.Any<IdempotencyIntentCommand>()).Returns(new IdempotencyCanonicalIntent("tenant-a:counter:counter-1", "{}"u8.ToArray(), null, original.PolicyVersion, null, null));
        IDeclaredIdempotencyIntentAdapter other = SampleCatalog.CreateAdapters().Single(a => a.CommandType == "decrement-counter");
        var registry = new IdempotencyIntentAdapterRegistry([adapter, other], new CanonicalIdempotencyIntentEncoder());
        var command = new SubmitCommand("id-1", "tenant-a", "counter", "counter-1", "IncrementCounter", "{}"u8.ToArray(), "corr-1", "actor-1");
        byte[] canonical = registry.Resolve(command).CanonicalIntent;
        aliases.Clear(); aliases.Add("Bogus"); aliases.Add("DecrementCounter");
        registry.Resolve(command).CanonicalIntent.ShouldBe(canonical);
        registry.Resolve(command with { CommandType = "DecrementCounter" }).OperationId.ShouldBe(other.OperationId);
        Should.Throw<InvalidOperationException>(() => registry.Resolve(command with { CommandType = "Bogus" }));
        adapter.Declaration.Returns(mutable with { Route = mutable.Route with { Domain = "other" } });
        registry.Resolve(command).CanonicalIntent.ShouldBe(canonical);
        Should.Throw<InvalidOperationException>(() => registry.Resolve(command with { CommandType = "Bogus" }));
        Should.Throw<InvalidOperationException>(() => registry.Resolve(command with { Domain = "other" }));
    }

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
    public async Task CompiledOwnersRemainAnIncompleteUnactivatedDeclarationCandidate()
    {
        string root = Root();
        byte[] retained = File.ReadAllBytes(Path.Combine(root, "deploy/dapr/eventstore-routing-catalog.json"));
        RouteCatalogEnvelope first = RouteCatalogCodec.Decode(retained);
        for (int attempt = 0; attempt < 2; attempt++)
            (await Should.ThrowAsync<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.BuildAsync(root, TestContext.Current.CancellationToken)))
                .Diagnostics.Single().Code.ShouldBe("unrepresented-unbounded-query-fallback");
        RouteCatalogCodec.ComputeRootDigest(first).ShouldBe("c156ce0b68bab43f561805db9cd02714d123026d388f65c764d7b6fa4e3b7308");
        first.Declarations.Select(RouteCatalogCodec.ComputeDeclarationDigest).Order(StringComparer.Ordinal)
            .ShouldBe(DevelopmentCatalogProfile.Declarations.Select(RouteCatalogCodec.ComputeDeclarationDigest).Order(StringComparer.Ordinal));
        IDeclaredIdempotencyIntentAdapter[] owners = [.. SampleCatalog.CreateAdapters(), .. TenantIntentAdapters.Create()];
        first.Idempotency.Select(RouteCatalogCodec.ComputeIdempotencyEntryDigest).Order(StringComparer.Ordinal)
            .ShouldBe(owners.Select(adapter => DevelopmentIdempotencyBindings.Bind(adapter.Declaration, first.Generation))
                .Select(RouteCatalogCodec.ComputeIdempotencyEntryDigest).Order(StringComparer.Ordinal));
        first.SchemaVersion.ShouldBe(2);
        first.Idempotency.Count.ShouldBe(17);
        first.Declarations.Count(d => d.Kind == "query").ShouldBe(9);
        first.Declarations.Count(d => d.Kind == "projection").ShouldBe(3);
        first.Routes.Single(r => r.RouteId == "counter-status-query").AppId.ShouldBe("eventstore");
        first.Routes.Single(r => r.RouteId == "counter-status-query").Method.ShouldBe("QueryAsync");
        first.Idempotency.All(i => i.KeyMap.All(k => k.MapKey == k.Generation)).ShouldBeTrue();
        RouteCatalogCodec.Encode(first).ShouldBe(retained);
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
        (await Should.ThrowAsync<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.BuildAsync(Root(), TestContext.Current.CancellationToken, invalid)))
            .Diagnostics.Single().Code.ShouldBe("missing-ambiguous-or-drifted-capability-evidence");
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
            (await Should.ThrowAsync<RouteCatalogValidationException>(() => DevelopmentCatalogProfile.BuildAsync(temporary, TestContext.Current.CancellationToken)))
                .Diagnostics.Single().Code.ShouldBe(change switch
                {
                    "dynamic" => "unbounded-dynamic-configuration",
                    "target" => "configured-binding-target-drift",
                    "extra-version" => "undeclared-configured-contract-version",
                    _ => "undeclared-resolver-tenant-scope",
                });
        }
        finally { Directory.Delete(temporary, recursive: true); }
    }

    [Fact]
    public async Task HistoricalGlobalAdministratorScopeExcludesOtherTenantConventionTargets()
    {
        RouteCatalogEnvelope catalog = RouteCatalogCodec.Decode(File.ReadAllBytes(Path.Combine(Root(), "deploy/dapr/eventstore-routing-catalog.json")));
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
