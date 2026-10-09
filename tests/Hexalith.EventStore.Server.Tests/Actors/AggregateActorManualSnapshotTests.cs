using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.Events;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        SnapshotRecord? stagedSnapshot = null;
        _ = snapshotManager.CreateSnapshotAsync(
                identity,
                2,
                Arg.Any<object>(),
                stateManager,
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>(),
                true)
            .Returns(callInfo => {
                capturedState = callInfo.ArgAt<object>(2);
                stagedSnapshot = new SnapshotRecord(
                    2,
                    capturedState,
                    DateTimeOffset.UnixEpoch,
                    identity.Domain,
                    identity.AggregateId,
                    identity.TenantId);
                return Task.CompletedTask;
            });
        _ = stateManager.TryGetStateAsync<SnapshotRecord>(
                identity.SnapshotKey,
                Arg.Any<CancellationToken>())
            .Returns(_ => stagedSnapshot is null
                ? new ConditionalValue<SnapshotRecord>(false, default!)
                : new ConditionalValue<SnapshotRecord>(true, stagedSnapshot));

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

    [Fact]
    public async Task CreateManualSnapshotAsync_SaveCommitsThenThrows_ReturnsCreatedFromFreshDurableWitness() {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        ISnapshotManager snapshotManager = CreateStagingSnapshotManager(identity, stateManager);
        IAggregateStateReconstructor reconstructor = CreateReconstructor(identity);
        stateManager.FaultAfterCall("SaveState", 1, new InvalidOperationException("commit uncertain"));
        AggregateActor actor = CreateActor(identity, stateManager, snapshotManager, reconstructor);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-ambiguous");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.Created);
        ((SnapshotRecord)stateManager.CommittedState[identity.SnapshotKey]).SequenceNumber.ShouldBe(2);
        stateManager.Trace.Count(operation => operation == "SaveState").ShouldBe(1);
    }

    [Fact]
    public async Task CreateManualSnapshotAsync_SaveFailsBeforeCommit_ReturnsBoundedFailureAndDiscardsSnapshot() {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        ISnapshotManager snapshotManager = CreateStagingSnapshotManager(identity, stateManager);
        IAggregateStateReconstructor reconstructor = CreateReconstructor(identity);
        stateManager.FaultOnCall("SaveState", 1, new InvalidOperationException("pre-commit failure"));
        AggregateActor actor = CreateActor(identity, stateManager, snapshotManager, reconstructor);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-ambiguous");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.InfrastructureFailure);
        stateManager.CommittedState.ShouldNotContainKey(identity.SnapshotKey);
        stateManager.Trace.ShouldContain("ClearCache");
    }

    [Fact]
    public async Task CreateManualSnapshotAsync_AmbiguousSaveWithConcurrentSameSequenceReplacement_DoesNotClaimCreated() {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        ISnapshotManager snapshotManager = CreateStagingSnapshotManager(identity, stateManager);
        IAggregateStateReconstructor reconstructor = CreateReconstructor(identity);
        var winner = new SnapshotRecord(
            2,
            JsonSerializer.Deserialize<JsonElement>("""{"status":"concurrent-winner"}"""),
            DateTimeOffset.UnixEpoch.AddMinutes(1),
            identity.Domain,
            identity.AggregateId,
            identity.TenantId);
        stateManager.FaultAfterCall("SaveState", 1, new InvalidOperationException("commit uncertain"));
        stateManager.ActBeforeCall(
            $"TryGetState:{identity.SnapshotKey}",
            2,
            manager => manager.InjectConcurrentWinnerAsync(new Dictionary<string, object>
            {
                [identity.SnapshotKey] = winner,
            }));
        AggregateActor actor = CreateActor(identity, stateManager, snapshotManager, reconstructor);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-ambiguous");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.InfrastructureFailure);
        ((SnapshotRecord)stateManager.CommittedState[identity.SnapshotKey]).ShouldBe(winner);
        stateManager.Trace.ShouldContain("ConcurrentWinner");
    }

    [Fact]
    public async Task CreateManualSnapshotAsync_DurableComparisonUsesConfiguredActorSerializerOptions() {
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        var stateManager = new FaultInjectingActorStateManager();
        await SeedStreamAsync(stateManager, identity);
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        _ = snapshotManager.InspectSnapshotForManualOverwriteAsync(
                identity,
                stateManager,
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(SnapshotLoadResult.Absent());
        SnapshotRecord? attempted = null;
        _ = snapshotManager.CreateSnapshotAsync(
                identity,
                2,
                Arg.Any<object>(),
                stateManager,
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>(),
                true)
            .Returns(async _ => {
                attempted = new SnapshotRecord(
                    2,
                    new { CurrentStatus = "ready" },
                    DateTimeOffset.UnixEpoch,
                    identity.Domain,
                    identity.AggregateId,
                    identity.TenantId);
                await stateManager.SetStateAsync(identity.SnapshotKey, attempted).ConfigureAwait(false);
            });
        stateManager.FaultAfterCall("SaveState", 1, new InvalidOperationException("commit uncertain"));
        stateManager.ActBeforeCall(
            $"TryGetState:{identity.SnapshotKey}",
            2,
            manager => manager.InjectConcurrentWinnerAsync(new Dictionary<string, object> {
                [identity.SnapshotKey] = attempted! with {
                    State = JsonSerializer.Deserialize<JsonElement>(
                        """{"current_status":"ready"}"""),
                },
            }));
        var actorStateOptions = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };
        AggregateActor actor = CreateActor(
            identity,
            stateManager,
            snapshotManager,
            CreateReconstructor(identity),
            actorStateOptions);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-ambiguous");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.Created);
    }

    private static ISnapshotManager CreateStagingSnapshotManager(
        AggregateIdentity identity,
        FaultInjectingActorStateManager stateManager) {
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        _ = snapshotManager.InspectSnapshotForManualOverwriteAsync(
                identity,
                stateManager,
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(SnapshotLoadResult.Absent());
        _ = snapshotManager.CreateSnapshotAsync(
                identity,
                2,
                Arg.Any<object>(),
                stateManager,
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>(),
                true)
            .Returns(callInfo => stateManager.SetStateAsync(
                identity.SnapshotKey,
                new SnapshotRecord(
                    2,
                    callInfo.ArgAt<object>(2),
                    DateTimeOffset.UnixEpoch,
                    identity.Domain,
                    identity.AggregateId,
                    identity.TenantId)));
        return snapshotManager;
    }

    private static IAggregateStateReconstructor CreateReconstructor(AggregateIdentity identity) {
        IAggregateStateReconstructor reconstructor = Substitute.For<IAggregateStateReconstructor>();
        _ = reconstructor.ReconstructAsync(
                identity,
                "OrderAggregate",
                Arg.Any<IReadOnlyList<EventEnvelope>>(),
                2,
                false,
                "corr-ambiguous",
                Arg.Any<CancellationToken>())
            .Returns(AggregateReconstructionResult.Succeeded("""{"status":"ready"}""", 2));
        return reconstructor;
    }

    /// <summary>Verifies keyed logical history refusal produces a safe reason without staging a snapshot.</summary>
    [Fact]
    public async Task CreateManualSnapshotAsync_RejectedLogicalHistoryDoesNotSnapshotStoredPayloads() {
        var identity = new AggregateIdentity("tenant", "d", "aggregate");
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        EventEnvelope stored = new(
            MessageId: UniqueIdHelper.GenerateSortableUniqueStringId(),
            AggregateId: identity.AggregateId,
            AggregateType: "r",
            TenantId: identity.TenantId,
            Domain: identity.Domain,
            SequenceNumber: 1,
            GlobalPosition: 0,
            Timestamp: DateTimeOffset.UnixEpoch,
            CorrelationId: "correlation",
            CausationId: "causation",
            UserId: "user",
            DomainServiceVersion: "v1",
            EventTypeName: "Legacy.Event",
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: [1, 2],
            Extensions: null);
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(1, DateTimeOffset.UnixEpoch, "etag")));
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, stored));

        using EventEvolutionManifestCandidate candidate = DaprProductionLogicalEventReaderTests.CreateUpcastingCandidate();
        using var handler = new CanonicalLogicalReplayHandler();
        using var httpClient = new HttpClient(handler);
        using var dapr = new Dapr.Client.DaprClientBuilder().Build();
        var reconstructor = DaprProductionLogicalEventReaderTests.CreateCanonicalReconstructor(dapr, httpClient, out _);
        var snapshotManager = new SnapshotManager(
            Options.Create(new SnapshotOptions()),
            Substitute.For<ILogger<SnapshotManager>>(),
            new NoOpEventPayloadProtectionService());
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
            new KeyedEvolutionProvider(candidate, reconstructor));
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-rejected");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.InfrastructureFailure);
        result.ReasonCode.ShouldBe("logical-event-read-rejected");
        stored.Payload.ShouldBe([1, 2]);
        stateManager.ReceivedCalls().Any(call =>
            call.GetMethodInfo().Name is "SaveStateAsync" or "SetStateAsync").ShouldBeFalse();
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>Verifies keyed manual snapshots persist canonical plaintext state after one addressed range read.</summary>
    [Fact]
    public async Task CreateManualSnapshotAsync_KeyedV1HistoryReconstructsCanonicalStateWithOneRangeRead() {
        var identity = new AggregateIdentity("tenant", "d", "aggregate");
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        EventEnvelope[] stored = DaprProductionLogicalEventReaderTests.StoreCanonicalHistory(stateManager);
        using EventEvolutionManifestCandidate candidate = DaprProductionLogicalEventReaderTests.CreateCandidate();
        IEventPayloadProtectionService protection = DaprProductionLogicalEventReaderTests.CreateCanonicalProtection();
        using var handler = new CanonicalLogicalReplayHandler();
        using var httpClient = new HttpClient(handler);
        using var dapr = new Dapr.Client.DaprClientBuilder().Build();
        var reconstructor = DaprProductionLogicalEventReaderTests.CreateCanonicalReconstructor(dapr, httpClient, out _);
        var snapshots = new SnapshotManager(Options.Create(new SnapshotOptions()), Substitute.For<ILogger<SnapshotManager>>(),
            new NoOpEventPayloadProtectionService());
        SnapshotRecord? snapshot = null;
        _ = stateManager.SetStateAsync(identity.SnapshotKey, Arg.Any<SnapshotRecord>(), Arg.Any<CancellationToken>())
            .Returns(call => { snapshot = call.ArgAt<SnapshotRecord>(1); return Task.CompletedTask; });
        _ = stateManager.TryGetStateAsync<SnapshotRecord>(identity.SnapshotKey, Arg.Any<CancellationToken>())
            .Returns(_ => snapshot is null ? new ConditionalValue<SnapshotRecord>(false, default!) : new ConditionalValue<SnapshotRecord>(true, snapshot));
        var host = ActorHost.CreateForTest<AggregateActor>(new ActorTestOptions { ActorId = new ActorId(identity.ActorId) });
        var actor = new AggregateActor(host, Substitute.For<ILogger<AggregateActor>>(), Substitute.For<IDomainServiceInvoker>(),
            snapshots, protection, Substitute.For<ICommandStatusStore>(), Substitute.For<IEventPublisher>(),
            Options.Create(new EventDrainOptions()), Options.Create(new BackpressureOptions()),
            Substitute.For<IDeadLetterPublisher>(), new KeyedEvolutionProvider(candidate, reconstructor));
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);

        ManualSnapshotResult result = await actor.CreateManualSnapshotAsync("corr-canonical");

        result.Outcome.ShouldBe(ManualSnapshotOutcome.Created);
        result.ReasonCode.ShouldBeNull();
        SnapshotRecord committed = snapshot.ShouldNotBeNull();
        committed.SequenceNumber.ShouldBe(3);
        committed.State.ShouldBeOfType<JsonElement>().GetProperty("count").GetInt32().ShouldBe(6);
        handler.Requests.ShouldHaveSingleItem().Events.Count.ShouldBe(3);
        _ = await stateManager.Received(2).TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>());
        _ = await stateManager.Received(1).TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}2", Arg.Any<CancellationToken>());
        _ = await stateManager.Received(1).TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}3", Arg.Any<CancellationToken>());
        _ = await protection.Received(3).TryUnprotectEventPayloadAsync(identity, Arg.Any<string>(), Arg.Any<byte[]>(), "json",
            Arg.Any<EventStorePayloadProtectionMetadata>(), Arg.Any<CancellationToken>());
        stored.All(e => e.Payload.SequenceEqual(new byte[] { 9, (byte)e.SequenceNumber })).ShouldBeTrue();
        await stateManager.Received(1).SaveStateAsync();
    }

    private static Task SeedStreamAsync(
        FaultInjectingActorStateManager stateManager,
        AggregateIdentity identity)
        => stateManager.SeedCommittedStateAsync(new Dictionary<string, object> {
            [identity.MetadataKey] = new AggregateMetadata(2, DateTimeOffset.UtcNow, null),
            [$"{identity.EventStreamKeyPrefix}1"] = CreateEvent(identity, 1),
            [$"{identity.EventStreamKeyPrefix}2"] = CreateEvent(identity, 2),
        });

    private static AggregateActor CreateActor(
        AggregateIdentity identity,
        IActorStateManager stateManager,
        ISnapshotManager snapshotManager,
        IAggregateStateReconstructor reconstructor,
        JsonSerializerOptions? actorStateJsonSerializerOptions = null) {
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
            new TestServiceProvider(reconstructor, actorStateJsonSerializerOptions));

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
            MessageId: UniqueIdHelper.GenerateSortableUniqueStringId(),
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

    private sealed class TestServiceProvider(
        IAggregateStateReconstructor reconstructor,
        JsonSerializerOptions? actorStateJsonSerializerOptions) : IServiceProvider {
        private readonly IOptions<ActorRuntimeOptions>? _actorOptions = actorStateJsonSerializerOptions is null
            ? null
            : Options.Create(new ActorRuntimeOptions {
                JsonSerializerOptions = actorStateJsonSerializerOptions,
            });

        public object? GetService(Type serviceType)
            => serviceType == typeof(IAggregateStateReconstructor)
                ? reconstructor
                : serviceType == typeof(IOptions<ActorRuntimeOptions>)
                    ? _actorOptions
                    : null;
    }

}
