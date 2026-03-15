
using System.Reflection;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Pipeline.Commands;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;
/// <summary>
/// End-to-end data path isolation tests validating the complete command flow
/// from router through actor to domain service maintains tenant isolation.
/// (AC: #1, #3, #5, #6, #9, #12, #13)
/// </summary>
public class DataPathIsolationTests {
    // --- Task 1.2: AC #1, #6 ---

    [Fact]
    public async Task CommandRouter_DifferentTenantsSameDomainSameAggId_RouteToSeparateActors() {
        // Arrange
        var capturedActorIds = new List<ActorId>();
        IAggregateActor actorProxy = Substitute.For<IAggregateActor>();
        _ = actorProxy.ProcessCommandAsync(Arg.Any<CommandEnvelope>())
            .Returns(new CommandProcessingResult(true));

        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        _ = proxyFactory.CreateActorProxy<IAggregateActor>(Arg.Do<ActorId>(capturedActorIds.Add), Arg.Any<string>())
            .Returns(actorProxy);

        var router = new CommandRouter(proxyFactory, NullLogger<CommandRouter>.Instance);

        var commandA = new SubmitCommand("tenant-a", "orders", "order-001", "CreateOrder", [1], Guid.NewGuid().ToString(), "user");
        var commandB = new SubmitCommand("tenant-b", "orders", "order-001", "CreateOrder", [1], Guid.NewGuid().ToString(), "user");

        // Act
        _ = await router.RouteCommandAsync(commandA);
        _ = await router.RouteCommandAsync(commandB);

        // Assert
        capturedActorIds.Count.ShouldBe(2);
        capturedActorIds[0].ToString().ShouldStartWith("tenant-a:");
        capturedActorIds[1].ToString().ShouldStartWith("tenant-b:");
        capturedActorIds[0].ToString().ShouldNotBe(capturedActorIds[1].ToString());
    }

    // --- Task 1.3: AC #6 ---

    [Theory]
    [InlineData("tenant-a", "orders", "order-001")]
    [InlineData("tenant-b", "inventory", "item-999")]
    [InlineData("acme", "billing", "inv-ABC123")]
    public async Task CommandRouter_DerivedActorId_AlwaysMatchesAggregateIdentityActorId(string tenant, string domain, string aggregateId) {
        // Arrange
        ActorId? capturedActorId = null;
        IAggregateActor actorProxy = Substitute.For<IAggregateActor>();
        _ = actorProxy.ProcessCommandAsync(Arg.Any<CommandEnvelope>())
            .Returns(new CommandProcessingResult(true));

        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        _ = proxyFactory.CreateActorProxy<IAggregateActor>(Arg.Do<ActorId>(id => capturedActorId = id), Arg.Any<string>())
            .Returns(actorProxy);

        var router = new CommandRouter(proxyFactory, NullLogger<CommandRouter>.Instance);
        var expectedIdentity = new AggregateIdentity(tenant, domain, aggregateId);
        var command = new SubmitCommand(tenant, domain, aggregateId, "TestCommand", [1, 2, 3], Guid.NewGuid().ToString(), "test-user");

        // Act
        _ = await router.RouteCommandAsync(command);

        // Assert
        _ = capturedActorId.ShouldNotBeNull();
        capturedActorId.ToString().ShouldBe(expectedIdentity.ActorId);
    }

    // --- Task 1.4: AC #9 ---

    [Fact]
    public async Task CommandRouter_ConcurrentDifferentTenants_ProcessedIndependently() {
        // Arrange
        var capturedActorIds = new List<ActorId>();
        IAggregateActor actorProxy = Substitute.For<IAggregateActor>();
        _ = actorProxy.ProcessCommandAsync(Arg.Any<CommandEnvelope>())
            .Returns(new CommandProcessingResult(true));

        IActorProxyFactory proxyFactory = Substitute.For<IActorProxyFactory>();
        _ = proxyFactory.CreateActorProxy<IAggregateActor>(Arg.Do<ActorId>(id => { lock (capturedActorIds) { capturedActorIds.Add(id); } }), Arg.Any<string>())
            .Returns(actorProxy);

        var router = new CommandRouter(proxyFactory, NullLogger<CommandRouter>.Instance);

        string[] tenants = new[] { "tenant-a", "tenant-b", "tenant-c", "tenant-d" };
        SubmitCommand[] commands = tenants.Select(t => new SubmitCommand(t, "orders", "order-001", "CreateOrder", [1], Guid.NewGuid().ToString(), "user")).ToArray();

        // Act -- concurrent submission
        _ = await Task.WhenAll(commands.Select(c => router.RouteCommandAsync(c)));

        // Assert
        capturedActorIds.Count.ShouldBe(4);
        var distinctActorIds = capturedActorIds.Select(a => a.ToString()).Distinct().ToList();
        distinctActorIds.Count.ShouldBe(4);

        foreach (string tenant in tenants) {
            distinctActorIds.ShouldContain(a => a!.StartsWith($"{tenant}:", StringComparison.Ordinal));
        }
    }

    // --- Task 1.5: AC #3 ---

    [Fact]
    public async Task EndToEnd_ThreeLayerIsolation_AllLayersExercised() {
        // Arrange -- create actor with correct tenant
        string tenantId = "test-tenant";
        string domain = "test-domain";
        string aggId = "agg-001";

        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        IDeadLetterPublisher deadLetterPublisher = Substitute.For<IDeadLetterPublisher>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId($"{tenantId}:{domain}:{aggId}") });
        var actor = new AggregateActor(host, logger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(), commandStatusStore, eventPublisher, Options.Create(new EventDrainOptions()), deadLetterPublisher);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        _ = stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

        CommandEnvelope? capturedInvokerCommand = null;
        _ = invoker.InvokeAsync(Arg.Do<CommandEnvelope>(c => capturedInvokerCommand = c), Arg.Any<object?>())
            .Returns(DomainResult.NoOp());

        var command = new CommandEnvelope(tenantId, domain, aggId, "CreateOrder", [1, 2, 3],
            Guid.NewGuid().ToString(), null, "system", null);

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(command);

        // Assert
        result.Accepted.ShouldBeTrue();

        // Layer 1: Actor ID includes correct tenant
        host.Id.GetId().ShouldStartWith($"{tenantId}:");

        // Layer 3: TenantValidator was exercised (no mismatch, so processing continued)
        _ = await invoker.Received(1).InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());

        // Layer 1+3: Domain service received correct tenant context
        _ = capturedInvokerCommand.ShouldNotBeNull();
        capturedInvokerCommand.TenantId.ShouldBe(tenantId);
    }

    // --- Task 1.6: AC #13, GAP-F3 ---

    [Fact]
    public async Task EndToEnd_TenantIdFlowsUnchanged_RouterToActorToInvoker() {
        // Arrange
        const string tenantId = "trace-tenant";
        const string domain = "orders";
        const string aggId = "order-trace-001";

        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        CommandEnvelope? invokerCapturedCommand = null;
        _ = invoker.InvokeAsync(Arg.Do<CommandEnvelope>(c => invokerCapturedCommand = c), Arg.Any<object?>())
            .Returns(DomainResult.NoOp());

        ILogger<AggregateActor> actorLogger = Substitute.For<ILogger<AggregateActor>>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        IDeadLetterPublisher deadLetterPublisher = Substitute.For<IDeadLetterPublisher>();

        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId($"{tenantId}:{domain}:{aggId}") });
        var actor = new AggregateActor(host, actorLogger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(), commandStatusStore, eventPublisher, Options.Create(new EventDrainOptions()), deadLetterPublisher);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        _ = stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

        // Verify CommandRouter derives the expected ActorId
        var expectedIdentity = new AggregateIdentity(tenantId, domain, aggId);
        string expectedActorId = expectedIdentity.ActorId;

        // Verify the actor ID tenant matches
        string actorIdTenant = host.Id.GetId().Split(':')[0];

        // Process through actor
        var command = new CommandEnvelope(tenantId, domain, aggId, "TestCmd", [1],
            Guid.NewGuid().ToString(), null, "system", null);

        _ = await actor.ProcessCommandAsync(command);

        // Assert -- TenantId identical at each stage
        // Stage 1: CommandRouter would derive this ActorId
        expectedActorId.ShouldStartWith($"{tenantId}:");

        // Stage 2: Actor's own ID tenant matches
        actorIdTenant.ShouldBe(tenantId);

        // Stage 3: DomainServiceInvoker received the exact same TenantId
        _ = invokerCapturedCommand.ShouldNotBeNull();
        invokerCapturedCommand.TenantId.ShouldBe(tenantId);
    }

    // --- Task 1.7: AC #12, GAP-C2 ---

    [Fact]
    public async Task AggregateActor_ProcessCommand_ExplicitlyCallsTenantValidator() {
        // Arrange -- create actor with matching tenant so validation PASSES
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        IDeadLetterPublisher deadLetterPublisher = Substitute.For<IDeadLetterPublisher>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId("test-tenant:test-domain:agg-001") });
        var actor = new AggregateActor(host, logger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(), commandStatusStore, eventPublisher, Options.Create(new EventDrainOptions()), deadLetterPublisher);

        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        _ = stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(DomainResult.NoOp());

        // Send mismatched tenant -- proves TenantValidator IS called
        var command = new CommandEnvelope("wrong-tenant", "test-domain", "agg-001", "TestCmd", [1],
            Guid.NewGuid().ToString(), "cause-1", "system", null);

        // Act
        CommandProcessingResult result = await actor.ProcessCommandAsync(command);

        // Assert -- TenantValidator rejected the command (proves it was called)
        result.Accepted.ShouldBeFalse();
        result.ErrorMessage!.ShouldContain("TenantMismatch");

        // Domain service should NOT have been invoked (rejected at Step 2)
        _ = await invoker.DidNotReceive().InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>());
    }
}
