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
    [InlineData(false, "SnapshotEnvelopeVersionUnknown")]
    [InlineData(true, "SnapshotProviderOpaque")]
    [InlineData(null, "SnapshotUnreadableProtected")]
    public async Task RetainedUnknownOpaqueOrUnreadableSnapshotIsReplacedWithSafeReasonAndCommittedFold(
        bool? opaque, string expectedReasonClass)
    {
        CommandEnvelope command = CreateTestEnvelope(correlationId: "folded-replacement");
        AggregateIdentity identity = command.AggregateIdentity;
        JsonElement retained = RetainedSnapshot(identity, opaque);
        FaultInjectingActorStateManager stateManager = await SeedStreamAsync(identity, retained);
        ILogger<SnapshotManager> snapshotLogger = Substitute.For<ILogger<SnapshotManager>>();
        var protection = new NoOpEventPayloadProtectionService();
        var snapshots = new SnapshotManager(Options.Create(new SnapshotOptions { DefaultInterval = 10 }),
            snapshotLogger, protection);
        using ServiceProvider services = QualificationProvider(identity);
        ActorTestContext actor = CreateActor(stateManager: stateManager,
            serviceProvider: services, payloadProtectionService: protection, snapshotManager: snapshots);
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

        string[] messages = actor.Logger.ReceivedCalls()
            .Select(call => call.GetArguments().Length > 2 ? call.GetArguments()[2]?.ToString() ?? "" : "")
            .ToArray();
        messages.Any(message => message.Contains($"ReplacementReasonClass={expectedReasonClass}",
            StringComparison.Ordinal)).ShouldBeTrue();
        messages.Any(message => message.Contains("retained-sensitive-value", StringComparison.Ordinal))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task LegacyDscsIsBypassedAndNextQualifiedAutomaticBatchOverwritesIt()
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
        _ = actor.Invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), true,
                "test-profile", Arg.Any<CancellationToken>())
            .Returns(DomainResult.Success([new TestEvent()])
                with { PostCommandStateJson = """{"count":11}""" });

        CommandProcessingResult processed = await actor.Actor.ProcessCommandAsync(command);

        processed.Accepted.ShouldBeTrue();
        await AssertFullPrefixInvocationAsync(actor, command);
        IReadOnlyDictionary<string, object> committedBatch = stateManager.CommittedSnapshots
            .First(view => view.ContainsKey($"{identity.EventStreamKeyPrefix}11"));
        SnapshotRecord folded = committedBatch[identity.SnapshotKey].ShouldBeOfType<SnapshotRecord>();
        folded.SnapshotEnvelopeVersion.ShouldBe(1);
        folded.SequenceNumber.ShouldBe(11);
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

    private static async Task AssertFullPrefixInvocationAsync(ActorTestContext actor, CommandEnvelope command)
    {
        _ = await actor.Invoker.Received(1).InvokeAsync(command, Arg.Any<object?>(), true,
            "test-profile", Arg.Any<CancellationToken>());
        object? currentState = actor.Invoker.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == "InvokeAsync" && call.GetArguments().Length == 5)
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
