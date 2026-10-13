using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using static Hexalith.EventStore.Server.Tests.Actors.AggregateActorTestHelper;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Actors;

/// <summary>Proves retained snapshot migration occurs only with the event batch.</summary>
public sealed class AggregateActorFoldedSnapshotMigrationTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(3, 0)]
    [InlineData(3, -1)]
    [InlineData(3, 5)]
    public async Task InvalidStreamHeadOrRetainedFloorRetainsCorruptPlaintextBeforeSnapshotLoad(
        long head, long retainedFloor)
    {
        // HX-FS-V1 §§9.1/9.2, V9/V17: an untrusted stream cannot authorize derived-state deletion.
        CommandEnvelope command = CreateTestEnvelope();
        AggregateIdentity identity = command.AggregateIdentity;
        JsonElement corrupt = JsonSerializer.Deserialize<JsonElement>(
            """{"snapshotEnvelopeVersion":1,"sequenceNumber":1,"state":{},"createdAt":"bad","domain":"test-domain","aggregateId":"agg-001","tenantId":"test-tenant","protectionMetadata":null}""");
        var stateManager = new FaultInjectingActorStateManager();
        var metadata = new AggregateMetadata(head, DateTimeOffset.UnixEpoch, null) { RetainedFloor = retainedFloor };
        await stateManager.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [identity.MetadataKey] = metadata,
            [identity.SnapshotKey] = corrupt,
        });
        var snapshots = new SnapshotManager(Options.Create(new SnapshotOptions()),
            Substitute.For<ILogger<SnapshotManager>>(), new NoOpEventPayloadProtectionService());
        ActorTestContext actor = CreateActor(stateManager: stateManager, snapshotManager: snapshots);

        CommandProcessingResult result = await actor.Actor.ProcessCommandAsync(command);

        result.Accepted.ShouldBeFalse();
        stateManager.Trace.ShouldContain($"TryGetState:{identity.MetadataKey}");
        stateManager.Trace.ShouldNotContain($"TryGetState:{identity.SnapshotKey}");
        stateManager.Trace.ShouldNotContain($"RemoveState:{identity.SnapshotKey}");
        stateManager.CommittedState[identity.MetadataKey].ShouldBe(metadata);
        JsonElement.DeepEquals(stateManager.CommittedState[identity.SnapshotKey].ShouldBeOfType<JsonElement>(),
            corrupt).ShouldBeTrue();
        _ = await actor.Invoker.DidNotReceiveWithAnyArgs().InvokeAsync(default!, default, default);
    }

    [Fact]
    public async Task OrphanCorruptPlaintextSnapshotIsRetainedBeforeSnapshotLoad()
    {
        CommandEnvelope command = CreateTestEnvelope(correlationId: "orphan-corrupt-snapshot");
        AggregateIdentity identity = command.AggregateIdentity;
        JsonElement corrupt = JsonSerializer.Deserialize<JsonElement>(
            """{"snapshotEnvelopeVersion":1,"sequenceNumber":1,"state":{},"createdAt":"bad","domain":"test-domain","aggregateId":"agg-001","tenantId":"test-tenant","protectionMetadata":null}""");
        var stateManager = new FaultInjectingActorStateManager();
        await stateManager.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [identity.SnapshotKey] = corrupt,
        });
        var snapshots = new SnapshotManager(Options.Create(new SnapshotOptions()),
            Substitute.For<ILogger<SnapshotManager>>(), new NoOpEventPayloadProtectionService());
        ActorTestContext actor = CreateActor(stateManager: stateManager, snapshotManager: snapshots);

        CommandProcessingResult result = await actor.Actor.ProcessCommandAsync(command);

        result.Accepted.ShouldBeFalse();
        const string safeFailure = "Protected data diagnostic details were redacted. ReasonCode=SnapshotStreamUnavailable; Stage=Processing.";
        result.FailureReason.ShouldBe(safeFailure);
        result.ErrorMessage.ShouldBe(safeFailure);
        stateManager.Trace.ShouldContain($"TryGetState:{identity.MetadataKey}");
        stateManager.Trace.ShouldContain($"ContainsState:{identity.SnapshotKey}");
        stateManager.Trace.ShouldNotContain($"TryGetState:{identity.SnapshotKey}");
        stateManager.Trace.ShouldNotContain($"RemoveState:{identity.SnapshotKey}");
        JsonElement.DeepEquals(stateManager.CommittedState[identity.SnapshotKey].ShouldBeOfType<JsonElement>(),
            corrupt).ShouldBeTrue();
        _ = await actor.Invoker.DidNotReceiveWithAnyArgs().InvokeAsync(default!, default, default);
    }

    [Fact]
    public async Task SnapshotCreationFailureLogsOnlySafeClassWithoutException()
    {
        AggregateIdentity identity = CreateTestEnvelope().AggregateIdentity;
        var snapshotLogger = new SnapshotManagerCaptureLogger();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        _ = protection.ProtectSnapshotAsync(identity, Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<SnapshotProtectionResult>(
                new IOException("provider-secret-detail")));
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()), snapshotLogger, protection);
        var stateManager = new FaultInjectingActorStateManager();

        bool staged = await manager.CreateAutomaticReplacementAsync(identity, 1,
            JsonSerializer.Deserialize<JsonElement>("""{"count":1}"""), stateManager,
            JsonSerializerOptions.Web, SnapshotTestLimits.ForState(),
            "SnapshotUnreadableProtected", "corr-safe-warning", CancellationToken.None);

        staged.ShouldBeFalse();
        snapshotLogger.Messages.ShouldContain(message => message.Contains(
            "FailureClass=SnapshotCreateUnavailable", StringComparison.Ordinal));
        snapshotLogger.Exceptions.ShouldAllBe(exception => exception == null);
        snapshotLogger.Messages.ShouldNotContain(message => message.Contains(
            "provider-secret-detail", StringComparison.Ordinal));
        stateManager.Trace.ShouldNotContain($"SetState:{identity.SnapshotKey}");
    }

    [Theory]
    [InlineData(false, "SnapshotEnvelopeVersionUnknown", "SnapshotEnvelopeVersionUnknown")]
    [InlineData(true, "SnapshotProviderOpaque", "SnapshotProtectionOpaque")]
    [InlineData(null, "SnapshotUnreadableProtected", "SnapshotUnreadableProtected")]
    public async Task RetainedUnknownOpaqueOrUnreadableSnapshotIsReplacedWithSafeReasonAndCommittedFold(
        bool? opaque, string expectedReasonClass, string expectedManualReasonClass)
    {
        CommandEnvelope command = CreateTestEnvelope(correlationId: "folded-replacement");
        AggregateIdentity identity = command.AggregateIdentity;
        JsonElement retained = RetainedSnapshot(identity, opaque);
        FaultInjectingActorStateManager stateManager = await SeedStreamAsync(identity, retained);
        var snapshotLogger = new SnapshotManagerCaptureLogger();
        var protection = new NoOpEventPayloadProtectionService();
        var snapshots = new SnapshotManager(Options.Create(new SnapshotOptions { DefaultInterval = 10 }),
            snapshotLogger, protection);
        using ServiceProvider services = QualificationProvider(identity);
        var actorLogger = new SnapshotCaptureLogger();
        ActorTestContext actor = CreateActor(stateManager: stateManager,
            serviceProvider: services, payloadProtectionService: protection, snapshotManager: snapshots,
            logger: actorLogger);
        ManualSnapshotResult manualResult = await actor.Actor.CreateManualSnapshotAsync("folded-manual-retain");
        manualResult.Outcome.ShouldBe(ManualSnapshotOutcome.UnreadableProtected);
        manualResult.ReasonCode.ShouldBe(expectedManualReasonClass);
        manualResult.Message.ShouldNotBeNull().ShouldContain(expectedManualReasonClass);
        manualResult.Message.ShouldNotContain("retained-sensitive-value");
        JsonElement.DeepEquals(stateManager.CommittedState[identity.SnapshotKey].ShouldBeOfType<JsonElement>(),
            retained).ShouldBeTrue();
        _ = actor.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true,
                "test-profile", Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()])
                with { PostCommandStateJson = """{"count":11}""" });

        CommandProcessingResult processed = await actor.Actor.ProcessCommandAsync(command);

        processed.Accepted.ShouldBeTrue();
        await AssertFullPrefixInvocationAsync(actor, command);
        stateManager.Trace.Count(entry => entry.StartsWith($"TryGetState:{identity.EventStreamKeyPrefix}",
            StringComparison.Ordinal) && !entry.EndsWith(":Delegated", StringComparison.Ordinal)).ShouldBe(10);
        IReadOnlyDictionary<string, object> committedBatch = stateManager.CommittedSnapshots
            .First(view => view.ContainsKey($"{identity.EventStreamKeyPrefix}11"));
        SnapshotRecord folded = committedBatch[identity.SnapshotKey].ShouldBeOfType<SnapshotRecord>();
        folded.SnapshotEnvelopeVersion.ShouldBe(1);
        folded.SequenceNumber.ShouldBe(11);
        folded.State.ShouldBeOfType<JsonElement>().GetProperty("count").GetInt32().ShouldBe(11);
        stateManager.CommittedState[identity.SnapshotKey].ShouldBe(folded);
        stateManager.CommittedSnapshots.TakeWhile(view => !view.ContainsKey($"{identity.EventStreamKeyPrefix}11"))
            .All(view => JsonElement.DeepEquals(view[identity.SnapshotKey].ShouldBeOfType<JsonElement>(), retained))
            .ShouldBeTrue();

        string[] messages = actorLogger.Messages.ToArray();
        messages.Any(message => message.Contains($"ReplacementReasonClass={expectedReasonClass}",
            StringComparison.Ordinal)).ShouldBeTrue();
        messages.Any(message => message.Contains("retained-sensitive-value", StringComparison.Ordinal))
            .ShouldBeFalse();
        snapshotLogger.Messages.ShouldContain(message => message.Contains(
            $"ReplacementReasonClass={expectedReasonClass}", StringComparison.Ordinal));
        if (opaque is not false)
        {
            snapshotLogger.EventIds.ShouldContain(eventId => eventId.Id == 7100);
        }
        actorLogger.EventIds.ShouldContain(eventId => eventId.Id == 2033);
        actorLogger.Exceptions.ShouldAllBe(exception => exception == null);
        snapshotLogger.Exceptions.ShouldAllBe(exception => exception == null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LegacyDscsIsBypassedAndNextQualifiedAutomaticBatchOverwritesIt(bool receiptBearing)
    {
        CommandEnvelope command = CreateTestEnvelope(correlationId: "folded-dscs-migration");
        AggregateIdentity identity = command.AggregateIdentity;
        JsonElement legacy = JsonSerializer.Deserialize<JsonElement>(
            """{"sequenceNumber":10,"state":{"currentSequence":10,"events":[],"snapshotState":{"count":10}},"createdAt":"2026-01-01T00:00:00Z","domain":"test-domain","aggregateId":"agg-001","tenantId":"test-tenant"}""");
        FaultInjectingActorStateManager stateManager = await SeedStreamAsync(identity, legacy);
        var protection = new NoOpEventPayloadProtectionService();
        var snapshots = new SnapshotManager(Options.Create(new SnapshotOptions { DefaultInterval = 10 }),
            Substitute.For<ILogger<SnapshotManager>>(), protection);
        using ServiceProvider services = QualificationProvider(identity);
        ActorTestContext actor = CreateActor(stateManager: stateManager,
            serviceProvider: services, payloadProtectionService: protection, snapshotManager: snapshots);
        const string receipt = "signed-human-authority-receipt";
        DomainResult evidence = DomainResult.Success([new TestEvent()])
            with { PostCommandStateJson = """{"count":11}""" };
        if (receiptBearing)
        {
            _ = actor.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true,
                "test-profile", receipt, Arg.Any<CancellationToken>()).Returns(evidence);
        }
        else
        {
            _ = actor.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true,
                "test-profile", Arg.Any<CancellationToken>()).Returns(evidence);
        }

        CommandProcessingResult processed = receiptBearing
            ? await actor.Actor.ProcessAuthorizedCommandAsync(new AuthorizedCommandEnvelope(command, receipt))
            : await actor.Actor.ProcessCommandAsync(command);

        processed.Accepted.ShouldBeTrue();
        await AssertFullPrefixInvocationAsync(actor, command, receiptBearing ? receipt : null);
        IReadOnlyDictionary<string, object> committedBatch = stateManager.CommittedSnapshots
            .First(view => view.ContainsKey($"{identity.EventStreamKeyPrefix}11"));
        SnapshotRecord folded = committedBatch[identity.SnapshotKey].ShouldBeOfType<SnapshotRecord>();
        folded.SnapshotEnvelopeVersion.ShouldBe(1);
        folded.SequenceNumber.ShouldBe(11);
        committedBatch[identity.MetadataKey].ShouldBeOfType<AggregateMetadata>().CurrentSequence.ShouldBe(11);
        committedBatch[$"{identity.EventStreamKeyPrefix}11"].ShouldBeOfType<EventEnvelope>()
            .SequenceNumber.ShouldBe(11);
        folded.State.ShouldBeOfType<JsonElement>().GetProperty("count").GetInt32().ShouldBe(11);
        stateManager.Trace.ShouldNotContain($"RemoveState:{identity.SnapshotKey}");
        stateManager.CommittedSnapshots.TakeWhile(view => !view.ContainsKey($"{identity.EventStreamKeyPrefix}11"))
            .All(view => JsonElement.DeepEquals(view[identity.SnapshotKey].ShouldBeOfType<JsonElement>(), legacy))
            .ShouldBeTrue();
    }

    private static ServiceProvider QualificationProvider(AggregateIdentity identity)
        => new ServiceCollection()
            .AddSingleton(new FoldedSnapshotQualificationRegistry([
                new FoldedSnapshotQualification(identity.Domain, identity.Domain, typeof(JsonElement),
                    "test-profile", "test-parity", SnapshotTestLimits.ForState()),
            ]))
            .BuildServiceProvider();

    private static async Task AssertFullPrefixInvocationAsync(ActorTestContext actor, CommandEnvelope command,
        string? receipt = null)
    {
        if (receipt is null)
        {
            _ = await actor.Invoker.Received(1).InvokeAsync(command, Arg.Any<object?>(), true,
                "test-profile", Arg.Any<CancellationToken>());
        }
        else
        {
            _ = await actor.Invoker.Received(1).InvokeAsync(command, Arg.Any<object?>(), true,
                "test-profile", receipt, Arg.Any<CancellationToken>());
        }
        object? currentState = actor.Invoker.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == "InvokeAsync" && call.GetArguments().Length == (receipt is null ? 5 : 6))
            .GetArguments()[1];
        DomainServiceCurrentState current = currentState.ShouldBeOfType<DomainServiceCurrentState>();
        current.LastSnapshotSequence.ShouldBe(0);
        current.CurrentSequence.ShouldBe(10);
        current.SnapshotState.ShouldBeNull();
        current.Events.Count.ShouldBe(10);
    }

    private static JsonElement RetainedSnapshot(AggregateIdentity identity, bool? opaque)
        => JsonSerializer.SerializeToElement(new SnapshotRecord(
            10, "retained-sensitive-value", DateTimeOffset.UnixEpoch,
            identity.Domain, identity.AggregateId, identity.TenantId,
            opaque is null
                ? new EventStorePayloadProtectionMetadata(
                    PayloadProtectionState.Protected, 1, "test-provider", "test-key", null, null)
                : opaque.Value
                    ? EventStorePayloadProtectionMetadata.ProviderOpaque("retained-sensitive-value")
                    : null)
        {
            SnapshotEnvelopeVersion = opaque is false ? 77 : 1,
        }, JsonSerializerOptions.Web);

    private static async Task<FaultInjectingActorStateManager> SeedStreamAsync(
        AggregateIdentity identity, JsonElement snapshot)
    {
        var stateManager = new FaultInjectingActorStateManager();
        var seed = new Dictionary<string, object>
        {
            [identity.MetadataKey] = new AggregateMetadata(10, DateTimeOffset.UnixEpoch, null),
            [identity.SnapshotKey] = snapshot,
        };
        for (int sequence = 1; sequence <= 10; sequence++)
        {
            seed[$"{identity.EventStreamKeyPrefix}{sequence}"] = new EventEnvelope(
                Guid.NewGuid().ToString(), identity.AggregateId, identity.Domain,
                identity.TenantId, identity.Domain, sequence, sequence, DateTimeOffset.UnixEpoch,
                "correlation", "causation", "user", "v1", "Changed", 1, "json",
                "{}"u8.ToArray(), null);
        }
        await stateManager.SeedCommittedStateAsync(seed);
        return stateManager;
    }
}
