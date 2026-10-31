using System.Text.Json;
using Dapr.Actors.Runtime;
using Hexalith.EventStore.Client.Routing;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Independently observes the existing SDK actor registration and exercises the real router against complete Sample schemas.</summary>
public sealed class DevelopmentProjectionActorObservationTests
{
    /// <summary>A name alone cannot substitute a missing or different SDK actor implementation.</summary>
    [Theory]
    [InlineData("valid")]
    [InlineData("missing")]
    [InlineData("different-implementation")]
    public void ActorTargetsRequireTheActualSdkRegistration(string state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var services = new ServiceCollection(); services.AddLogging(); services.AddEventStoreServer(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ActorRuntimeOptions>>().Value;
        if (state != "valid") options.Actors.Remove(options.Actors.Single(registration => registration.Type.ActorTypeName == RouteCatalogProjectionActorPolicy.ActorType).Type);
        if (state == "different-implementation") options.Actors.RegisterActor<GlobalPositionActor>(RouteCatalogProjectionActorPolicy.ActorType);
        if (state == "valid")
        {
            var actual = RunScopedRouteCatalogGateway.ActorQueryTargets(provider, [typeof(SampleCatalog).Assembly], "gateway-owned").ShouldHaveSingleItem();
            actual.AppId.ShouldBe("gateway-owned"); actual.Method.ShouldBe(nameof(IProjectionActor.QueryAsync)); actual.RouteId.ShouldBe("counter-status-query");
        }
        else Should.Throw<RouteCatalogUnavailableException>(() => RunScopedRouteCatalogGateway.ActorQueryTargets(provider, [typeof(SampleCatalog).Assembly], "gateway-owned"));
    }

    /// <summary>Complete owner schemas retain actual actor routing and all prior refusal boundaries after genuine authority activation.</summary>
    [Theory]
    [InlineData("valid")]
    [InlineData("unknown-query")]
    [InlineData("projection-override")]
    [InlineData("actor-override")]
    [InlineData("changed-registration")]
    public async Task SampleActorQueryUsesCommittedObservedTargetBeforeEffects(string fault)
    {
        ArgumentNullException.ThrowIfNull(fault);
        const string gatewayApp = "gateway-owned";
        var services = new ServiceCollection(); services.AddLogging(); services.AddEventStoreServer(new ConfigurationBuilder().Build());
        RouteCatalogAdmissionFixture.Configure(services, SampleCatalog.CreateAdapters());
        using var provider = services.BuildServiceProvider();
        var declarations = CompiledRouteCatalogDeclarations.Read([typeof(SampleCatalog).Assembly]);
        var operations = CompiledRouteCatalogDeclarations.ReadOperations([typeof(SampleCatalog).Assembly]);
        var admission = await RouteCatalogAdmissionObservation.CaptureAsync(provider, gatewayApp, TestContext.Current.CancellationToken);
        var actorTargets = RunScopedRouteCatalogGateway.ActorQueryTargets(provider, [typeof(SampleCatalog).Assembly], gatewayApp);
        var domainTargets = declarations.Where(declaration => declaration.Execution!.TargetKind == "domain-service").Select(declaration =>
            new RouteCatalogEntry(declaration.RouteId, 1, RouteCatalogCodec.ComputeDeclarationDigest(declaration), "sample-owned",
                SampleCatalog.ReceiverMethods[declaration.RouteId], declaration.ContractVersion, declaration.CredentialKind, declaration.Operation, false, "*")).ToArray();
        var catalog = VersionedRouteCatalogBuilder.Build(new(1, "Development", declarations, operations,
            actorTargets.Concat(domainTargets).Select(target => new RouteCompositionBinding(target.RouteId, target.AppId, target.Method, target.ContractVersion, false, "*")).ToArray(), admission));
        var store = new RouteCatalogTestStateStore(); string[] hosts = ["gateway", "sample"];
        byte[] component = JsonSerializer.SerializeToUtf8Bytes(new { kind = "Component", metadata = new { name = store.Coordinates.StateStore },
            spec = new { type = "state.redis", metadata = new[] { new { name = "keyPrefix", value = "none" } } }, scopes = new[] { gatewayApp, "sample-owned" } });
        byte[] acl = JsonSerializer.SerializeToUtf8Bytes(new { spec = new { accessControl = new { defaultAction = "deny", trustDomain = "public" } } });
        RouteCatalogHostObservation Gateway() => new("gateway", gatewayApp, declarations,
            RunScopedRouteCatalogGateway.ActorQueryTargets(provider, [typeof(SampleCatalog).Assembly], gatewayApp), hosts, [component, acl],
            "sdk-actor-fixture"u8.ToArray(), admission, [], [], store.Coordinates, Operations: operations);
        var domain = new RouteCatalogHostObservation("sample", "sample-owned", declarations, domainTargets, hosts, [component, acl],
            "compiled-sample-fixture"u8.ToArray(), [], [], [], store.Coordinates, Operations: operations);
        var observed = new[] { Gateway(), domain }; var identities = observed.Select(ObservedRouteCatalogHost.Identity).ToArray();
        var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns("Development");
        var authorities = identities.Select(identity =>
        {
            var options = Options.Create(new RouteCatalogHostOptions { Identity = identity, Profile = "Development", DevelopmentFixture = true });
            return new RouteCatalogAuthority(store, new DevelopmentRouteCatalogTrustValidator(options, environment), options,
                new ObservedRouteCatalogHost(_ => Task.FromResult(identity.HostId == "gateway" ? Gateway() : domain)));
        }).ToArray();
        var candidate = new RouteCatalogActivation("sample-actor", RouteCatalogCodec.Encode(catalog), RouteCatalogCodec.ComputeRootDigest(catalog), null, identities, "Development-fixture", string.Empty);
        candidate = candidate with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(candidate) };
        await authorities[0].PrepareAsync(candidate, TestContext.Current.CancellationToken);
        foreach (var authority in authorities) await authority.ReadyAsync(candidate.AttemptId, TestContext.Current.CancellationToken);
        await authorities[0].CommitAsync(candidate.AttemptId, TestContext.Current.CancellationToken);
        var invoker = Substitute.For<IProjectionActorInvoker>();
        invoker.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { count = 3 }), "counter"));
        var router = new QueryRouter(invoker, NullLogger<QueryRouter>.Instance, Substitute.For<IProjectionLifecycleGateway>(), catalogAuthority: authorities[0]);
        var request = new SubmitQuery("tenant-a", "counter", "counter-1", "get-counter-status", [], "correlation", "fixture-user", EntityId: "counter-1");
        if (fault == "unknown-query") request = request with { QueryType = "not-declared" };
        if (fault == "projection-override") request = request with { ProjectionType = "other" };
        if (fault == "actor-override") request = request with { ProjectionActorType = "other" };
        if (fault == "changed-registration")
        {
            var actors = provider.GetRequiredService<IOptions<ActorRuntimeOptions>>().Value.Actors;
            actors.Remove(actors.Single(registration => registration.Type.ActorTypeName == RouteCatalogProjectionActorPolicy.ActorType).Type);
        }
        if (fault == "valid")
        {
            (await router.RouteQueryAsync(request, TestContext.Current.CancellationToken)).Payload!.Value.GetProperty("count").GetInt32().ShouldBe(3);
            // Absent lifecycle metadata retains the existing single bounded retry;
            // both actual invocations must retain the same committed actor identity.
            await invoker.Received(2).InvokeAsync("counter:tenant-a:counter-1", RouteCatalogProjectionActorPolicy.ActorType,
                Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
        }
        else
        {
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => router.RouteQueryAsync(request, TestContext.Current.CancellationToken));
            invoker.ReceivedCalls().ShouldBeEmpty();
        }
        (await store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
    }
}
