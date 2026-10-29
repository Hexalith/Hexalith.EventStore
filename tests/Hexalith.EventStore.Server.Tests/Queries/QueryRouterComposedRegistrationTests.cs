using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Sample.Counter;
using Hexalith.EventStore.Server.Tests.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Server.Tests.Projections;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Queries;

public sealed class QueryRouterComposedRegistrationTests
{
    [Theory]
    [InlineData("current", false, ProjectionLifecycleState.Current, false)]
    [InlineData("growth", false, ProjectionLifecycleState.Stale, false)]
    [InlineData("invalid", false, ProjectionLifecycleState.Unknown, false)]
    [InlineData("guard_off", false, ProjectionLifecycleState.Current, false)]
    [InlineData("growth", true, ProjectionLifecycleState.Unknown, false)]
    [InlineData("current", false, ProjectionLifecycleState.Degraded, true)]
    [InlineData("growth", false, ProjectionLifecycleState.Degraded, true)]
    [InlineData("invalid", false, ProjectionLifecycleState.Degraded, true)]
    [InlineData("guard_off", false, ProjectionLifecycleState.Degraded, true)]
    public async Task ProductionDecorator_UsesOwningGuardedConcreteRouterAndPreservesDeclaredHandlerRouting(
        string state, bool declaredHandler, ProjectionLifecycleState expected, bool degraded)
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(1, 1,
            unguardedLastRoute: state == "guard_off");
        await fixture.UseProductionOwnersAsync();
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        if (state == "growth") await fixture.AppendPersistedSourceEventAsync();
        if (state == "invalid")
        {
            ProjectionDeliveryState row = fixture.Rows[route];
            byte[] body = JsonSerializer.SerializeToUtf8Bytes(row with
            { GuardEvidence = row.GuardEvidence! with { IdleLifecycleRevision = 999 } }, fixture.Backend.JsonSerializerOptions);
            fixture.Backend.SeedByteStore(ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, route), body);
        }
        IProjectionActorInvoker actor = Substitute.For<IProjectionActorInvoker>();
        actor.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(
            QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { count = 1 }), route,
                new QueryResponseMetadata(ProjectionVersion: "version-1", IsStale: false, IsDegraded: degraded ? false : null)
                { Lifecycle = degraded ? ProjectionLifecycleState.Degraded : ProjectionLifecycleState.Current }));
        IDomainQueryHandlerRegistry registry = Substitute.For<IDomainQueryHandlerRegistry>();
        registry.SupportsQueryAsync(fixture.Identity.Domain, "query-counter", Arg.Any<CancellationToken>()).Returns(declaredHandler);
        IDomainQueryInvoker handler = Substitute.For<IDomainQueryInvoker>();
        handler.InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(
            QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { count = 2 }), projectionType: null, metadata: new QueryResponseMetadata()));
        IProjectionLifecycleGateway lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        lifecycle.ReadSnapshotAsync(fixture.Identity, route, Arg.Any<CancellationToken>()).Returns(new ProjectionLifecycleSnapshot(ProjectionLifecyclePhase.Idle, 1));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(actor);
        services.AddSingleton(lifecycle);
        services.AddSingleton<IProjectionSourceGuardDelivery>(fixture.Probe);
        services.AddSingleton(registry);
        services.AddSingleton(handler);
        // Isolate persisted source/lifecycle protocol assertions with an explicit, canonical catalog snapshot.
        // Activation/CAS and actual-host guards are exercised independently in Routing and LiveSidecar tests.
        var envelope = new RouteCatalogTestFixture().Envelope;
        var declaration = CounterCatalogDeclarations.Query with
        {
            Domain = fixture.Identity.Domain, MessageType = "query-counter", ProjectionType = declaredHandler ? null : route,
            Operation = declaredHandler ? EventStoreOperationIdentifiers.DomainServiceQuery : EventStoreOperationIdentifiers.ProjectionQuery,
            Execution = CounterCatalogDeclarations.Query.Execution! with
            {
                Aliases = ["query-counter"], TargetKind = declaredHandler ? "domain-service" : "projection-actor",
                QueryKind = declaredHandler ? "computed" : "projection", ActorType = declaredHandler ? null : "ProjectionActor",
            },
        };
        envelope = RouteCatalogCodec.Seal(envelope with
        {
            Declarations = [.. envelope.Declarations.Select(d => d.RouteId == declaration.RouteId ? declaration : d)],
            Routes = [.. envelope.Routes.Select(r => r.RouteId == declaration.RouteId ? r with
            {
                DeclarationDigest = RouteCatalogCodec.ComputeDeclarationDigest(declaration), Operation = declaration.Operation,
                AppId = declaredHandler ? "sample" : "eventstore", Method = declaredHandler ? "query" : "QueryAsync",
            } : r)],
        });
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1, localAppId: "eventstore");
        IRouteCatalogAuthority catalog = Substitute.For<IRouteCatalogAuthority>();
        catalog.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(snapshot, () => Task.CompletedTask));
        services.AddSingleton(catalog);
        services.AddEventStoreServer(new ConfigurationBuilder().Build());
        services.AddEventStoreDomainQueryRouting();
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IQueryRouter router = scope.ServiceProvider.GetRequiredService<IQueryRouter>();
        router.ShouldBeOfType<HandlerAwareQueryRouter>();
        scope.ServiceProvider.GetRequiredService<QueryRouter>().ShouldBeSameAs(scope.ServiceProvider.GetRequiredService<QueryRouter>());
        using ProjectionDeliveryCostScope costs = ProjectionDeliveryCostDiagnostics.BeginScope();
        QueryRouterResult result = await router.RouteQueryAsync(new SubmitQuery(fixture.Identity.TenantId, fixture.Identity.Domain,
            fixture.Identity.AggregateId, "query-counter", [], "correlation-1", "user-1", ProjectionType: declaredHandler ? null : route));
        result.Success.ShouldBeTrue();
        result.Metadata.ShouldNotBeNull().Lifecycle.ShouldBe(expected);
        result.Metadata.Provenance.ShouldBe(declaredHandler ? QueryResponseProvenance.HandlerComputed : QueryResponseProvenance.ProjectionBacked);
        result.Metadata.IsStale.ShouldBe(ProjectionLifecyclePolicy.ProjectIsStale(expected));
        result.Metadata.IsDegraded.ShouldBe(degraded ? true : null);
        ProjectionLifecyclePolicy.CanMutate(true, result.Metadata.Provenance, result.Metadata.Lifecycle).ShouldBe(expected == ProjectionLifecycleState.Current);
        if (declaredHandler)
        {
            await handler.Received(1).InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
            await actor.DidNotReceive().InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
            costs[ProjectionDeliveryCost.SourceInspections].ShouldBe(0);
        }
        else
        {
            await actor.Received(1).InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
            await handler.DidNotReceive().InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
            costs[ProjectionDeliveryCost.MarkerReads].ShouldBeGreaterThanOrEqualTo(2);
        }
        costs[ProjectionDeliveryCost.SourceRanges].ShouldBe(0);
        costs[ProjectionDeliveryCost.MutationAttempts].ShouldBe(0);
        costs[ProjectionDeliveryCost.ProofWriteAttempts].ShouldBe(0);
    }
}
