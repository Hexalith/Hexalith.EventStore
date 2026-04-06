
using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>
/// Story 11-4 Task 4: Tests for refresh interval gating in ProjectionUpdateOrchestrator.
/// Verifies ACs 2, 3, and 4.
/// </summary>
public class ProjectionUpdateOrchestratorRefreshIntervalTests {
    private static readonly AggregateIdentity TestIdentity = new("test-tenant", "test-domain", "agg-001");

    // --- AC 2: DefaultRefreshIntervalMs = 0 proceeds with update ---

    [Fact]
    public async Task UpdateProjectionAsync_DefaultRefreshIntervalZero_ProceedsWithUpdate() {
        // Arrange
        var options = new ProjectionOptions { DefaultRefreshIntervalMs = 0 };
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver) = CreateSut(options);
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns(Array.Empty<EventEnvelope>());
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(aggregateActor);

        // Act
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert - Resolver was called (orchestrator proceeded past the refresh interval check)
        _ = await resolver.Received(1).ResolveAsync("test-tenant", "test-domain", "v1", Arg.Any<CancellationToken>());
        _ = actorProxyFactory.Received(1).CreateActorProxy<IAggregateActor>(
            Arg.Is<ActorId>(id => id.GetId() == TestIdentity.ActorId),
            Arg.Is("AggregateActor"));
    }

    // --- AC 2: DefaultRefreshIntervalMs > 0 skips update ---

    [Fact]
    public async Task UpdateProjectionAsync_DefaultRefreshIntervalPositive_SkipsUpdate() {
        // Arrange
        var options = new ProjectionOptions { DefaultRefreshIntervalMs = 5000 };
        (ProjectionUpdateOrchestrator sut, _, _, IDomainServiceResolver resolver) = CreateSut(options);

        // Act
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert - Resolver was NOT called (orchestrator returned early)
        _ = await resolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default!, default!, default);
    }

    // --- AC 3: Per-domain override 0 proceeds for that domain ---

    [Fact]
    public async Task UpdateProjectionAsync_PerDomainOverrideZero_ProceedsForThatDomain() {
        // Arrange
        var options = new ProjectionOptions {
            DefaultRefreshIntervalMs = 5000,
            Domains = new Dictionary<string, DomainProjectionOptions> {
                ["test-domain"] = new DomainProjectionOptions { RefreshIntervalMs = 0 },
            },
        };
        (ProjectionUpdateOrchestrator sut, _, _, IDomainServiceResolver resolver) = CreateSut(options);
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        // Act
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert - Resolver was called (per-domain override 0 takes precedence)
        _ = await resolver.Received(1).ResolveAsync("test-tenant", "test-domain", "v1", Arg.Any<CancellationToken>());
    }

    // --- AC 3: Per-domain override positive skips for that domain ---

    [Fact]
    public async Task UpdateProjectionAsync_PerDomainOverridePositive_SkipsForThatDomain() {
        // Arrange
        var options = new ProjectionOptions {
            DefaultRefreshIntervalMs = 0,
            Domains = new Dictionary<string, DomainProjectionOptions> {
                ["test-domain"] = new DomainProjectionOptions { RefreshIntervalMs = 3000 },
            },
        };
        (ProjectionUpdateOrchestrator sut, _, _, IDomainServiceResolver resolver) = CreateSut(options);

        // Act
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert - Resolver was NOT called (per-domain override skips)
        _ = await resolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default!, default!, default);
    }

    // --- AC 3: Per-domain override positive proceeds for OTHER domains ---

    [Fact]
    public async Task UpdateProjectionAsync_PerDomainOverridePositive_ProceedsForOtherDomains() {
        // Arrange
        var options = new ProjectionOptions {
            DefaultRefreshIntervalMs = 0,
            Domains = new Dictionary<string, DomainProjectionOptions> {
                ["order"] = new DomainProjectionOptions { RefreshIntervalMs = 3000 },
            },
        };
        (ProjectionUpdateOrchestrator sut, _, _, IDomainServiceResolver resolver) = CreateSut(options);
        var registration = new DomainServiceRegistration("counter-service", "project", "test-tenant", "test-domain", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        // Act - Call with "test-domain" which is NOT in the Domains dict, uses default 0
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert - Resolver was called (test-domain uses default 0)
        _ = await resolver.Received(1).ResolveAsync("test-tenant", "test-domain", "v1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateProjectionAsync_PerDomainOverrideMixedCase_SkipsForMatchingDomain() {
        // Arrange
        var options = new ProjectionOptions {
            DefaultRefreshIntervalMs = 0,
            Domains = new Dictionary<string, DomainProjectionOptions> {
                ["TEST-DOMAIN"] = new DomainProjectionOptions { RefreshIntervalMs = 3000 },
            },
        };
        (ProjectionUpdateOrchestrator sut, _, _, IDomainServiceResolver resolver) = CreateSut(options);

        // Act
        await sut.UpdateProjectionAsync(TestIdentity);

        // Assert - Mixed-case override key still applies
        _ = await resolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default!, default!, default);
    }

    // --- AC 4: Tenant isolation - passes tenant ID through pipeline ---

    [Fact]
    public async Task UpdateProjectionAsync_TenantIsolation_PassesTenantIdThroughPipeline() {
        // Arrange
        var options = new ProjectionOptions { DefaultRefreshIntervalMs = 0 };
        (ProjectionUpdateOrchestrator sut, IActorProxyFactory actorProxyFactory, _, IDomainServiceResolver resolver) = CreateSut(options);
        var acmeIdentity = new AggregateIdentity("acme", "counter", "123");
        var registration = new DomainServiceRegistration("counter-service", "project", "acme", "counter", "v1");
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(registration);

        IAggregateActor aggregateActor = Substitute.For<IAggregateActor>();
        _ = aggregateActor.GetEventsAsync(0).Returns(Array.Empty<EventEnvelope>());
        _ = actorProxyFactory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(aggregateActor);

        // Act
        await sut.UpdateProjectionAsync(acmeIdentity);

        // Assert - Resolver was called with tenant "acme"
        _ = await resolver.Received(1).ResolveAsync("acme", "counter", "v1", Arg.Any<CancellationToken>());

        // Assert - Aggregate actor proxy created with ActorId containing "acme"
        _ = actorProxyFactory.Received(1).CreateActorProxy<IAggregateActor>(
            Arg.Is<ActorId>(id => id.GetId() == acmeIdentity.ActorId),
            Arg.Is("AggregateActor"));
    }

    private static (ProjectionUpdateOrchestrator Sut, IActorProxyFactory ActorProxyFactory, DaprClient DaprClient, IDomainServiceResolver Resolver) CreateSut(ProjectionOptions? options = null) {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        DaprClient daprClient = Substitute.For<DaprClient>();
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        IOptions<ProjectionOptions> projectionOptions = Options.Create(options ?? new ProjectionOptions());
        var sut = new ProjectionUpdateOrchestrator(actorProxyFactory, daprClient, Substitute.For<IHttpClientFactory>(), resolver, projectionOptions, NullLogger<ProjectionUpdateOrchestrator>.Instance);
        return (sut, actorProxyFactory, daprClient, resolver);
    }
}
