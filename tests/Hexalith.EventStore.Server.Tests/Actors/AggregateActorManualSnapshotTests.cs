using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class AggregateActorManualSnapshotTests {
    [Fact]
    public async Task CreateManualSnapshotAsync_StoresReconstructedState_NotReplayEnvelope() {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        ConfigureStream(stateManager, identity);

        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        _ = snapshotManager.InspectSnapshotForManualOverwriteAsync(
                identity,
                stateManager,
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(SnapshotLoadResult.Absent());

        object? capturedState = null;
        _ = snapshotManager.CreateSnapshotAsync(
                identity,
                2,
                Arg.Do<object>(state => capturedState = state),
                stateManager,
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>(),
                true)
            .Returns(Task.CompletedTask);

        IAggregateStateReconstructor reconstructor = Substitute.For<IAggregateStateReconstructor>();
        _ = reconstructor.ReconstructAsync(
                identity,
                "OrderAggregate",
                Arg.Any<IReadOnlyList<EventEnvelope>>(),
                2,
                false,
                "corr-1",
                Arg.Any<CancellationToken>())
            .Returns(AggregateReconstructionResult.Succeeded("""{"status":"ready","count":2}""", 2));

        AggregateActor actor = CreateActor(identity, stateManager, snapshotManager, reconstructor);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-1");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.Created);
        JsonElement state = capturedState.ShouldBeOfType<JsonElement>();
        state.TryGetProperty("status", out JsonElement status).ShouldBeTrue();
        status.GetString().ShouldBe("ready");
        capturedState.ShouldNotBeOfType<Hexalith.EventStore.Contracts.Commands.DomainServiceCurrentState>();
        await stateManager.Received(1).SaveStateAsync();
    }

    private static AggregateActor CreateActor(
        AggregateIdentity identity,
        IActorStateManager stateManager,
        ISnapshotManager snapshotManager,
        IAggregateStateReconstructor reconstructor) {
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId(identity.ActorId) });
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        var actor = new AggregateActor(
            host,
            logger,
            Substitute.For<IDomainServiceInvoker>(),
            snapshotManager,
            new NoOpEventPayloadProtectionService(),
            Substitute.For<ICommandStatusStore>(),
            Substitute.For<IEventPublisher>(),
            Options.Create(new EventDrainOptions()),
            Options.Create(new BackpressureOptions()),
            Substitute.For<IDeadLetterPublisher>(),
            new TestServiceProvider(reconstructor));

        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);
        return actor;
    }

    private static void ConfigureStream(IActorStateManager stateManager, AggregateIdentity identity) {
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(2, DateTimeOffset.UtcNow, null)));

        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, CreateEvent(identity, 1)));
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}2", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, CreateEvent(identity, 2)));
    }

    private static EventEnvelope CreateEvent(AggregateIdentity identity, int sequence)
        => new(
            MessageId: $"msg-{sequence}",
            AggregateId: identity.AggregateId,
            AggregateType: "OrderAggregate",
            TenantId: identity.TenantId,
            Domain: identity.Domain,
            SequenceNumber: sequence,
            GlobalPosition: sequence,
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: "corr-1",
            CausationId: $"cause-{sequence}",
            UserId: "operator",
            DomainServiceVersion: "v1",
            EventTypeName: "OrderChanged",
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: JsonSerializer.SerializeToUtf8Bytes(new { sequence }),
            Extensions: null);

    private sealed class TestServiceProvider(IAggregateStateReconstructor reconstructor) : IServiceProvider {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IAggregateStateReconstructor) ? reconstructor : null;
    }
}
