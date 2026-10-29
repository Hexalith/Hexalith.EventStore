using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogProjectionGuardTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("stale-capability")]
    [InlineData("undeclared-target")]
    [InlineData("undeclared-output")]
    public async Task NamedCapabilityCacheMustMatchTheCapturedGeneration(string scenario)
    {
        var fixture = new RouteCatalogTestFixture();
        RouteContractDeclaration original = fixture.Envelope.Declarations.Single(d => d.RouteId == "counter-projection");
        RouteCatalogEnvelope envelope = RouteCatalogCodec.Seal(fixture.Envelope with
        {
            Routes = fixture.Envelope.Routes.Select(r => r.RouteId == original.RouteId ? r with { Method = "project/v2" } : r).ToArray(),
        });
        string fingerprint = RouteCatalogTestFixture.Hash("named-capability-v1");
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1,
            projectionCapabilities: [new("sample", "counter", "v1", fingerprint, ["counter"])], localAppId: "eventstore");
        IRouteCatalogAuthority authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(snapshot, () => Task.CompletedTask));
        string app = scenario == "undeclared-target" ? "attacker" : "sample";
        string cachedFingerprint = scenario == "stale-capability" ? RouteCatalogTestFixture.Hash("old-generation") : fingerprint;
        string[] types = scenario == "undeclared-output" ? ["counter", "undeclared-output"] : ["counter"];
        var entry = new NamedProjectionRouteCatalogEntry(app, "v1", "counter", 2, "fixture-capability", cachedFingerprint,
            types, ["counter"], ["counter"]);
        var cache = new NamedProjectionRouteCatalog();
        cache.Replace(new([entry]));
        IProjectionDeliveryCheckpointStore checkpoints = Substitute.For<IProjectionDeliveryCheckpointStore>();
        IProjectionDeliveryHistoryReader history = Substitute.For<IProjectionDeliveryHistoryReader>();
        var coordinator = new NamedProjectionDispatchCoordinator(authority, cache, checkpoints, Substitute.For<IProjectionLifecycleGateway>(), Substitute.For<IActorProxyFactory>(), Substitute.For<DaprClient>(), Substitute.For<IHttpClientFactory>(), Options.Create(new ProjectionDispatchOptions()), NullLogger<NamedProjectionDispatchCoordinator>.Instance, retryScheduler: Substitute.For<IProjectionDeliveryRetryScheduler>(), idempotencyCoordinator: Substitute.For<IProjectionDeliveryIdempotencyCoordinator>(), historyReader: history);
        var identity = new AggregateIdentity("tenant-a", "counter", "counter-1");
        var registration = new DomainServiceRegistration(app, "process", identity.TenantId, identity.Domain, "v1");
        if (scenario == "valid") (await coordinator.SupportsMappedOnlyStreamedDeliveryAsync(identity, registration, default)).ShouldBeTrue();
        else await Should.ThrowAsync<RouteCatalogUnavailableException>(() => coordinator.SupportsMappedOnlyStreamedDeliveryAsync(identity, registration, default));
        checkpoints.ReceivedCalls().ShouldBeEmpty(); history.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("named")]
    [InlineData("legacy")]
    [InlineData("undeclared")]
    public async Task CacheMissValidatesCommittedMethodAndTargetBeforeAnyMetadataRefresh(string mode)
    {
        var fixture = new RouteCatalogTestFixture();
        var envelope = RouteCatalogCodec.Seal(fixture.Envelope with
        {
            Routes = fixture.Envelope.Routes.Select(r => r.RouteId == "counter-projection" && mode != "legacy" ? r with { Method = "project/v2" } : r).ToArray(),
        });
        string fingerprint = RouteCatalogTestFixture.Hash("named-capability-v1");
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1,
            projectionCapabilities: mode == "legacy" ? [] : [new("sample", "counter", "v1", fingerprint, ["counter"])], localAppId: "eventstore");
        IRouteCatalogAuthority authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(snapshot, () => Task.CompletedTask));
        var cache = new NamedProjectionRouteCatalog();
        INamedProjectionCatalogRefresher refresher = Substitute.For<INamedProjectionCatalogRefresher>();
        refresher.RefreshAsync(Arg.Any<DomainServiceRegistration>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            cache.Replace(new([new NamedProjectionRouteCatalogEntry("sample", "v1", "counter", 2, "fixture-capability", fingerprint,
                ["counter"], ["counter"], ["counter"])]));
            return true;
        });
        var coordinator = new NamedProjectionDispatchCoordinator(authority, cache, Substitute.For<IProjectionDeliveryCheckpointStore>(), Substitute.For<IProjectionLifecycleGateway>(), Substitute.For<IActorProxyFactory>(), Substitute.For<DaprClient>(), Substitute.For<IHttpClientFactory>(), Options.Create(new ProjectionDispatchOptions()), NullLogger<NamedProjectionDispatchCoordinator>.Instance, catalogRefresher: refresher, retryScheduler: Substitute.For<IProjectionDeliveryRetryScheduler>(), idempotencyCoordinator: Substitute.For<IProjectionDeliveryIdempotencyCoordinator>(), historyReader: Substitute.For<IProjectionDeliveryHistoryReader>());
        var identity = new AggregateIdentity("tenant-a", "counter", "counter-1");
        var registration = new DomainServiceRegistration(mode == "undeclared" ? "attacker" : "sample", "process", identity.TenantId, identity.Domain, "v1");
        if (mode == "undeclared")
        {
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => coordinator.SupportsMappedOnlyStreamedDeliveryAsync(identity, registration, default));
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => coordinator.TryDispatchMappedOnlyAsync(identity, registration, 1, "head", default));
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => coordinator.TryRebuildAsync(identity, registration, [], 0, "rebuild", default));
        }
        else (await coordinator.SupportsMappedOnlyStreamedDeliveryAsync(identity, registration, default)).ShouldBe(mode == "named");
        if (mode == "named") await refresher.Received(1).RefreshAsync(registration, Arg.Any<CancellationToken>());
        else refresher.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyProjectionAuthorityRejectsUnknownDomainsBeforeSourceOrCheckpointAccess(bool declared)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        IActorProxyFactory actors = Substitute.For<IActorProxyFactory>();
        IAggregateActor aggregate = Substitute.For<IAggregateActor>();
        aggregate.GetEventsAsync(0).Returns(Array.Empty<EventEnvelope>());
        actors.CreateActorProxy<IAggregateActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(aggregate);
        IDomainServiceResolver independentResolver = Substitute.For<IDomainServiceResolver>();
        IProjectionCheckpointTracker checkpoints = Substitute.For<IProjectionCheckpointTracker>();
        var orchestrator = new ProjectionUpdateOrchestrator(fixture.Authorities[0], actors, Substitute.For<DaprClient>(), Substitute.For<IHttpClientFactory>(), independentResolver, checkpoints, Options.Create(new ProjectionOptions()), NullLogger<ProjectionUpdateOrchestrator>.Instance);
        if (declared) await orchestrator.DeliverProjectionAsync(new("tenant-a", "counter", "counter-1"));
        else await Should.ThrowAsync<RouteCatalogUnavailableException>(() => orchestrator.DeliverProjectionAsync(new("tenant-a", "undeclared", "counter-1")));
        independentResolver.ReceivedCalls().ShouldBeEmpty();
        if (declared) await aggregate.Received(1).GetEventsAsync(0);
        else { aggregate.ReceivedCalls().ShouldBeEmpty(); actors.ReceivedCalls().ShouldBeEmpty(); checkpoints.ReceivedCalls().ShouldBeEmpty(); }
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }
    [Fact]
    public async Task CancellationDuringLocalCapabilityCacheInspectionReleasesEveryLease()
    {
        var fixture = new RouteCatalogTestFixture();
        var envelope = RouteCatalogCodec.Seal(fixture.Envelope with
        {
            Routes = fixture.Envelope.Routes.Select(r => r.RouteId == "counter-projection" ? r with { Method = "project/v2" } : r).ToArray(),
        });
        string fingerprint = RouteCatalogTestFixture.Hash("named-capability-v1");
        var snapshot = new CommittedRouteCatalogSnapshot(RouteCatalogCodec.Encode(envelope), RouteCatalogCodec.ComputeRootDigest(envelope), 1,
            projectionCapabilities: [new("sample", "counter", "v1", fingerprint, ["counter"])], localAppId: "eventstore");
        int acquired = 0, released = 0;
        IRouteCatalogAuthority authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            acquired++;
            return new RouteCatalogDispatchLease(snapshot, () => { released++; return Task.CompletedTask; });
        });
        using var cancellation = new CancellationTokenSource();
        INamedProjectionRouteCatalog cache = Substitute.For<INamedProjectionRouteCatalog>();
        cache.Current.Returns(_ =>
        {
            cancellation.Cancel();
            return new NamedProjectionRouteCatalogSnapshot([new("sample", "v1", "counter", 2, "fixture-capability", fingerprint,
                ["counter"], ["counter"], ["counter"])]);
        });
        INamedProjectionCatalogRefresher refresher = Substitute.For<INamedProjectionCatalogRefresher>();
        var coordinator = new NamedProjectionDispatchCoordinator(authority, cache, Substitute.For<IProjectionDeliveryCheckpointStore>(), Substitute.For<IProjectionLifecycleGateway>(), Substitute.For<IActorProxyFactory>(), Substitute.For<DaprClient>(), Substitute.For<IHttpClientFactory>(), Options.Create(new ProjectionDispatchOptions()), NullLogger<NamedProjectionDispatchCoordinator>.Instance, catalogRefresher: refresher, retryScheduler: Substitute.For<IProjectionDeliveryRetryScheduler>(), idempotencyCoordinator: Substitute.For<IProjectionDeliveryIdempotencyCoordinator>(), historyReader: Substitute.For<IProjectionDeliveryHistoryReader>());
        (await coordinator.SupportsMappedOnlyStreamedDeliveryAsync(new("tenant-a", "counter", "aggregate"), new("sample", "process", "tenant-a", "counter", "v1"), cancellation.Token)).ShouldBeTrue();
        cancellation.IsCancellationRequested.ShouldBeTrue();
        acquired.ShouldBeGreaterThan(0);
        released.ShouldBe(acquired);
        refresher.ReceivedCalls().ShouldBeEmpty();
    }
    [Fact]
    public async Task ActualProjectionActorTransportAmbiguityRetainsTheDurableFence()
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate(); await fixture.ActivateAsync(first);
        IActorProxyFactory actors = Substitute.For<IActorProxyFactory>();
        IAggregateActor aggregate = Substitute.For<IAggregateActor>();
        aggregate.GetEventsAsync(0).Returns(Task.FromException<EventEnvelope[]>(new IOException("Actor transport lost the execution response.")));
        actors.CreateActorProxy<IAggregateActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(aggregate);
        var orchestrator = new ProjectionUpdateOrchestrator(fixture.Authorities[0], actors, Substitute.For<DaprClient>(), Substitute.For<IHttpClientFactory>(), Substitute.For<IDomainServiceResolver>(), Substitute.For<IProjectionCheckpointTracker>(), Options.Create(new ProjectionOptions()), NullLogger<ProjectionUpdateOrchestrator>.Instance);
        await orchestrator.DeliverProjectionAsync(new("tenant-a", "counter", "aggregate"));
        await aggregate.Received(1).GetEventsAsync(0);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
        var second = fixture.Candidate(2, first.RootDigest); await fixture.Authorities[0].PrepareAsync(second);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].ReadyAsync(second.AttemptId));
    }
}
