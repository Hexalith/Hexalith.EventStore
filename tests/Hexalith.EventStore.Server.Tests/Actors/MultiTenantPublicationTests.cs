namespace Hexalith.EventStore.Server.Tests.Actors;

using System.Reflection;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Fakes;

using Hexalith.EventStore.Server.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

/// <summary>
/// Story 4.2 Task 5: AggregateActor multi-tenant topic isolation integration tests.
/// Verifies that the full actor pipeline publishes events to the correct per-tenant-per-domain topic
/// (AC: #2, #3, #4, #9).
/// </summary>
public class MultiTenantPublicationTests
{
    private static CommandEnvelope CreateCommand(string tenantId, string domain, string aggregateId, string? correlationId = null) => new(
        TenantId: tenantId,
        Domain: domain,
        AggregateId: aggregateId,
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId ?? Guid.NewGuid().ToString(),
        CausationId: null,
        UserId: "system",
        Extensions: null);

    private static (AggregateActor Actor, IActorStateManager StateManager, FakeEventPublisher Publisher) CreateActorForTenant(
        string tenantId, string domain, string aggregateId)
    {
        var stateManager = Substitute.For<IActorStateManager>();
        var logger = Substitute.For<ILogger<AggregateActor>>();
        var invoker = Substitute.For<IDomainServiceInvoker>();
        var snapshotManager = Substitute.For<ISnapshotManager>();
        var commandStatusStore = Substitute.For<ICommandStatusStore>();
        var fakePublisher = new FakeEventPublisher();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId($"{tenantId}:{domain}:{aggregateId}") });
        var actor = new AggregateActor(host, logger, invoker, snapshotManager, commandStatusStore, fakePublisher, Options.Create(new EventDrainOptions()), new Hexalith.EventStore.Testing.Fakes.FakeDeadLetterPublisher());

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        // Domain service returns a success event
        invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(DomainResult.Success([new TestEvent()]));

        // No pipeline state (fresh command)
        stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));

        // No duplicate
        stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

        // New aggregate (no metadata)
        stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

        return (actor, stateManager, fakePublisher);
    }

    // --- Task 5.2: AC #2 ---

    [Fact]
    public async Task ProcessCommand_TenantA_PublishesToTenantATopic()
    {
        // Arrange
        (AggregateActor actor, _, FakeEventPublisher publisher) = CreateActorForTenant("acme", "orders", "order-1");
        CommandEnvelope command = CreateCommand("acme", "orders", "order-1");

        // Act
        await actor.ProcessCommandAsync(command);

        // Assert
        publisher.GetPublishedTopics().ShouldBe(["acme.orders.events"]);
        publisher.GetEventsForTopic("acme.orders.events").ShouldNotBeEmpty();
    }

    // --- Task 5.3: AC #2 ---

    [Fact]
    public async Task ProcessCommand_TenantB_PublishesToTenantBTopic()
    {
        // Arrange
        (AggregateActor actor, _, FakeEventPublisher publisher) = CreateActorForTenant("globex", "orders", "order-1");
        CommandEnvelope command = CreateCommand("globex", "orders", "order-1");

        // Act
        await actor.ProcessCommandAsync(command);

        // Assert
        publisher.GetPublishedTopics().ShouldBe(["globex.orders.events"]);
        publisher.GetEventsForTopic("globex.orders.events").ShouldNotBeEmpty();
    }

    // --- Task 5.4: AC #2, #4 ---

    [Fact]
    public async Task ProcessCommand_MultipleTenants_Sequential_CorrectTopicsPerTenant()
    {
        // Arrange - shared publisher to verify topic isolation across sequential calls
        var fakePublisher = new FakeEventPublisher();
        var tenants = new[] { "acme", "globex", "initech" };

        foreach (string tenant in tenants)
        {
            var stateManager = Substitute.For<IActorStateManager>();
            var logger = Substitute.For<ILogger<AggregateActor>>();
            var invoker = Substitute.For<IDomainServiceInvoker>();
            var snapshotManager = Substitute.For<ISnapshotManager>();
            var commandStatusStore = Substitute.For<ICommandStatusStore>();
            var host = ActorHost.CreateForTest<AggregateActor>(
                new ActorTestOptions { ActorId = new ActorId($"{tenant}:orders:order-1") });
            var actor = new AggregateActor(host, logger, invoker, snapshotManager, commandStatusStore, fakePublisher, Options.Create(new EventDrainOptions()), new Hexalith.EventStore.Testing.Fakes.FakeDeadLetterPublisher());

            PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
            prop?.SetValue(actor, stateManager);

            invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
                .Returns(DomainResult.Success([new TestEvent()]));
            stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<PipelineState>(false, default!));
            stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
            stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

            // Act
            await actor.ProcessCommandAsync(CreateCommand(tenant, "orders", "order-1"));
        }

        // Assert
        IReadOnlyList<string> topics = fakePublisher.GetPublishedTopics();
        topics.Count.ShouldBe(3);
        topics.ShouldContain("acme.orders.events");
        topics.ShouldContain("globex.orders.events");
        topics.ShouldContain("initech.orders.events");
    }

    // --- Task 5.5: AC #3 ---

    [Fact]
    public async Task ProcessCommand_MultipleDomains_Sequential_CorrectTopicsPerDomain()
    {
        // Arrange - shared publisher for domain isolation
        var fakePublisher = new FakeEventPublisher();
        var domains = new[] { "orders", "inventory", "shipping" };

        foreach (string domain in domains)
        {
            var stateManager = Substitute.For<IActorStateManager>();
            var logger = Substitute.For<ILogger<AggregateActor>>();
            var invoker = Substitute.For<IDomainServiceInvoker>();
            var snapshotManager = Substitute.For<ISnapshotManager>();
            var commandStatusStore = Substitute.For<ICommandStatusStore>();
            var host = ActorHost.CreateForTest<AggregateActor>(
                new ActorTestOptions { ActorId = new ActorId($"acme:{domain}:agg-1") });
            var actor = new AggregateActor(host, logger, invoker, snapshotManager, commandStatusStore, fakePublisher, Options.Create(new EventDrainOptions()), new Hexalith.EventStore.Testing.Fakes.FakeDeadLetterPublisher());

            PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
            prop?.SetValue(actor, stateManager);

            invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
                .Returns(DomainResult.Success([new TestEvent()]));
            stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<PipelineState>(false, default!));
            stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
            stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

            // Act
            await actor.ProcessCommandAsync(CreateCommand("acme", domain, "agg-1"));
        }

        // Assert
        IReadOnlyList<string> topics = fakePublisher.GetPublishedTopics();
        topics.Count.ShouldBe(3);
        topics.ShouldContain("acme.orders.events");
        topics.ShouldContain("acme.inventory.events");
        topics.ShouldContain("acme.shipping.events");
    }

    // --- Task 5.6: AC #2, #3, #4 ---

    [Fact]
    public async Task ProcessCommand_TenantDomainMatrix_AllTopicsDistinct()
    {
        // Arrange - 2x2 matrix: (acme, globex) x (orders, inventory)
        var fakePublisher = new FakeEventPublisher();
        var matrix = new[]
        {
            (Tenant: "acme", Domain: "orders"),
            (Tenant: "acme", Domain: "inventory"),
            (Tenant: "globex", Domain: "orders"),
            (Tenant: "globex", Domain: "inventory"),
        };

        foreach ((string tenant, string domain) in matrix)
        {
            var stateManager = Substitute.For<IActorStateManager>();
            var logger = Substitute.For<ILogger<AggregateActor>>();
            var invoker = Substitute.For<IDomainServiceInvoker>();
            var snapshotManager = Substitute.For<ISnapshotManager>();
            var commandStatusStore = Substitute.For<ICommandStatusStore>();
            var host = ActorHost.CreateForTest<AggregateActor>(
                new ActorTestOptions { ActorId = new ActorId($"{tenant}:{domain}:agg-1") });
            var actor = new AggregateActor(host, logger, invoker, snapshotManager, commandStatusStore, fakePublisher, Options.Create(new EventDrainOptions()), new Hexalith.EventStore.Testing.Fakes.FakeDeadLetterPublisher());

            PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
            prop?.SetValue(actor, stateManager);

            invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
                .Returns(DomainResult.Success([new TestEvent()]));
            stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<PipelineState>(false, default!));
            stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
            stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

            // Act
            await actor.ProcessCommandAsync(CreateCommand(tenant, domain, "agg-1"));
        }

        // Assert - all 4 topics are distinct
        IReadOnlyList<string> topics = fakePublisher.GetPublishedTopics();
        topics.Count.ShouldBe(4);
        topics.ShouldContain("acme.orders.events");
        topics.ShouldContain("acme.inventory.events");
        topics.ShouldContain("globex.orders.events");
        topics.ShouldContain("globex.inventory.events");

        // Each topic has exactly 1 event
        foreach (string topic in topics)
        {
            fakePublisher.GetEventsForTopic(topic).Count.ShouldBe(1);
        }
    }

    private sealed record TestEvent : IEventPayload;
}
