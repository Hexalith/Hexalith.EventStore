
using System.Reflection;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>
/// Test context record containing the actor and all mocks used during test setup.
/// </summary>
internal sealed record ActorTestContext(
    AggregateActor Actor,
    IActorStateManager StateManager,
    ILogger<AggregateActor> Logger,
    IDomainServiceInvoker Invoker,
    ISnapshotManager SnapshotManager,
    ICommandStatusStore StatusStore,
    IEventPublisher EventPublisher,
    IDeadLetterPublisher DeadLetterPublisher);

/// <summary>
/// Shared test infrastructure for AggregateActor tests.
/// </summary>
internal static class AggregateActorTestHelper {
    internal static CommandEnvelope CreateTestEnvelope(
        string tenantId = "test-tenant",
        string? correlationId = null,
        string? causationId = null) => new(
        MessageId: Guid.NewGuid().ToString(),
        TenantId: tenantId,
        Domain: "test-domain",
        AggregateId: "agg-001",
        CommandType: "CreateOrder",
        Payload: [1, 2, 3],
        CorrelationId: correlationId ?? Guid.NewGuid().ToString(),
        CausationId: causationId,
        UserId: "system",
        Extensions: null);

    internal static ActorTestContext CreateActor() {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId("test-tenant:test-domain:agg-001") });
        IDeadLetterPublisher deadLetterPublisher = Substitute.For<IDeadLetterPublisher>();
        _ = deadLetterPublisher.PublishDeadLetterAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<DeadLetterMessage>(),
            Arg.Any<CancellationToken>())
            .Returns(true);
        var actor = new AggregateActor(host, logger, invoker, snapshotManager, new NoOpEventPayloadProtectionService(), commandStatusStore, eventPublisher, Options.Create(new EventDrainOptions()), Options.Create(new BackpressureOptions()), deadLetterPublisher);

        // Set the mock state manager via reflection (Dapr runtime normally sets this)
        PropertyInfo? prop = typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance);
        prop?.SetValue(actor, stateManager);

        // Default: domain service returns NoOp
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>())
            .Returns(DomainResult.NoOp());

        // Default: no pipeline state (fresh command, not a resume)
        _ = stateManager.TryGetStateAsync<PipelineState>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<PipelineState>(false, default!));

        // Default: event publisher succeeds
        _ = eventPublisher.PublishEventsAsync(
            Arg.Any<Hexalith.EventStore.Contracts.Identity.AggregateIdentity>(),
            Arg.Any<IReadOnlyList<EventEnvelope>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo => new EventPublishResult(true, callInfo.ArgAt<IReadOnlyList<EventEnvelope>>(1).Count, null));

        return new ActorTestContext(actor, stateManager, logger, invoker, snapshotManager, commandStatusStore, eventPublisher, deadLetterPublisher);
    }

    internal static void ConfigureNoDuplicate(IActorStateManager stateManager) {
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));

        // Default: new aggregate (no metadata) -- Step 3 returns null state
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));
    }

    internal static void ConfigureExistingAggregate(IActorStateManager stateManager, int eventCount) {
        var metadata = new AggregateMetadata(eventCount, DateTimeOffset.UtcNow, null);
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(
            "test-tenant:test-domain:agg-001:metadata", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        string keyPrefix = "test-tenant:test-domain:agg-001:events:";
        for (int i = 1; i <= eventCount; i++) {
            int seq = i;
            var evt = new EventEnvelope(
                "msg-1", "agg-001", "test-aggregate", "test-tenant", "test-domain", seq, 0, DateTimeOffset.UtcNow,
                $"corr-{seq}", $"cause-{seq}", "user-1", "1.0.0", "OrderCreated", 1, "json",
                [1, 2, 3], null);
            _ = stateManager.TryGetStateAsync<EventEnvelope>($"{keyPrefix}{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }
    }

    internal static void ConfigureDuplicate(IActorStateManager stateManager, string causationId, string correlationId) {
        var record = new IdempotencyRecord(causationId, correlationId, true, null, DateTimeOffset.UtcNow);
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>($"idempotency:{causationId}", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(true, record));

        // Default for other keys
        _ = stateManager.TryGetStateAsync<IdempotencyRecord>(
            Arg.Is<string>(s => s != $"idempotency:{causationId}"), Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<IdempotencyRecord>(false, default!));
    }

    internal static bool IsSnapshotAwareCurrentState(object? state) =>
        state is DomainServiceCurrentState snapshotState
        && snapshotState.UsedSnapshot
        && snapshotState.LastSnapshotSequence == 1
        && snapshotState.CurrentSequence == 2
        && snapshotState.Events.Count == 1;

    // Test event types for domain invocation tests
    internal sealed record TestEvent : Hexalith.EventStore.Contracts.Events.IEventPayload;

    internal sealed record TestRejectionEvent : Hexalith.EventStore.Contracts.Events.IRejectionEvent;
}
