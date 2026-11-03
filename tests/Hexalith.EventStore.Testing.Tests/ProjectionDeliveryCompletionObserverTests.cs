using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

using Dapr.Actors;
using Dapr.Actors.Runtime;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Testing.Tests;

/// <summary>Pins completion observation to the real SDK wire builder, delivery coordinator, lifecycle actor and Dapr serialization.</summary>
public sealed class ProjectionDeliveryCompletionObserverTests
{
    /// <summary>Actual v1 and guarded v2 producers match only after durable completion and explicit idle observation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealProducerRejectsChangedMissingOrInflightEvidence(bool guarded)
    {
        var (source, state, epoch, serializer) = await ProduceAsync(guarded);
        byte[] checkpoint = ProjectionDeliveryStateSerialization.Serialize(state, serializer, 256);
        bool Matches(byte[]? body = null, byte[]? before = null, bool absent = true, byte[]? after = null, StreamReadPage? prefix = null)
            => ProjectionDeliveryCompletionObserver.IsComplete(prefix ?? source, "operational-status", body ?? checkpoint,
                before ?? epoch, absent, after ?? epoch, serializer);
        Matches().ShouldBeTrue();
        Matches(body: []).ShouldBeFalse(); Matches(before: []).ShouldBeFalse(); Matches(after: []).ShouldBeFalse();
        Matches(before: "0"u8.ToArray(), after: "0"u8.ToArray()).ShouldBeFalse();
        Matches(after: "4"u8.ToArray()).ShouldBeFalse(); Matches(absent: false).ShouldBeFalse();
        Matches(before: "null"u8.ToArray(), after: "null"u8.ToArray()).ShouldBeFalse();
        Matches(prefix: source with { Tenant = "other" }).ShouldBeFalse();
        Matches(prefix: source with { Metadata = source.Metadata with { LatestSequence = 3 } }).ShouldBeFalse();
        Matches(prefix: source with { Metadata = source.Metadata with { IsTruncated = true } }).ShouldBeFalse();
        Matches(prefix: source with { Events = [source.Events[0] with { MetadataVersion = 0 }, source.Events[1]] }).ShouldBeFalse();
        Matches(prefix: source with { Events = [source.Events[0] with { MetadataVersion = 2 }, source.Events[1]] }).ShouldBeFalse();
        Matches(prefix: source with { Events = [source.Events[0] with { CorrelationId = null }, source.Events[1]] }).ShouldBeFalse();
        Matches(prefix: source with { Events = [source.Events[0] with { GlobalPosition = 999 }, source.Events[1]] }).ShouldBe(!guarded);
        Matches(prefix: source with { Events = [source.Events[0] with { PayloadVersion = 3 }, source.Events[1]] }).ShouldBe(!guarded);
        Matches(prefix: source with { Events = [source.Events[0] with { Payload = "changed"u8.ToArray() }, source.Events[1]] }).ShouldBeFalse();
        Matches(prefix: source with { Events = [source.Events[0] with { MessageId = "changed" }, source.Events[1]] }).ShouldBeFalse();
        Matches(prefix: source with { Events = [source.Events[0] with { UserId = "changed" }, source.Events[1]] }).ShouldBeFalse();
        Matches(prefix: source with { Events = [source.Events[0] with { ProtectionMetadata = EventStorePayloadProtectionMetadata.ProviderOpaque() }, source.Events[1]] }).ShouldBeFalse();
        JsonObject original = JsonSerializer.SerializeToNode(state, serializer)!.AsObject();
        foreach (string field in new[] { "schemaVersion", "writerProtocolVersion", "tenantId", "domain", "aggregateId", "projectionName",
            "lastDeliveredSequence", "lastCompletedMessageId", "completedPrefixFingerprint", "completedReceipts", "firstRetainedSequence",
            "activeReservation", "updatedAt", "completedMessageIdDigests", "idleBindingPending" })
        {
            var missing = (JsonObject)original.DeepClone(); missing.Remove(field);
            Matches(JsonSerializer.SerializeToUtf8Bytes(missing)).ShouldBeFalse(field);
        }
        foreach ((string field, JsonNode? value) in new (string, JsonNode?)[]
        {
            ("tenantId", JsonValue.Create("other")), ("domain", JsonValue.Create("other")),
            ("aggregateId", JsonValue.Create("other")), ("projectionName", JsonValue.Create("other")),
            ("lastDeliveredSequence", JsonValue.Create(1)), ("lastCompletedMessageId", JsonValue.Create("changed")),
            ("completedPrefixFingerprint", JsonValue.Create("v1:" + Convert.ToBase64String(new byte[32]).TrimEnd('=').Replace('+', '-').Replace('/', '_'))),
        })
        {
            var changed = (JsonObject)original.DeepClone(); changed[field] = value;
            Matches(JsonSerializer.SerializeToUtf8Bytes(changed)).ShouldBeFalse(field);
        }
        foreach (string field in new[] { "messageId", "eventFingerprint", "prefixFingerprint" })
        {
            var changed = (JsonObject)original.DeepClone(); changed["completedReceipts"]![0]![field] = field == "messageId"
                ? "changed" : state.CompletedReceipts![1].EventFingerprint;
            Matches(JsonSerializer.SerializeToUtf8Bytes(changed)).ShouldBeFalse(field);
        }
        var corrupt = (JsonObject)original.DeepClone(); corrupt["completedMessageIdDigests"]![0] = Convert.ToBase64String(new byte[32]);
        Matches(JsonSerializer.SerializeToUtf8Bytes(corrupt)).ShouldBeFalse();
        byte[] duplicate = System.Text.Encoding.UTF8.GetBytes("{\"lastDeliveredSequence\":2," + original.ToJsonString()[1..]);
        Matches(duplicate).ShouldBeFalse();
        var reservation = new ProjectionDeliveryReservation(1, 2, "01ARZ3NDEKTSV4RRFFQ69G5FA2", "dispatch", "manifest", 1,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(5), 1);
        Matches(JsonSerializer.SerializeToUtf8Bytes(state with { ActiveReservation = reservation }, serializer)).ShouldBeFalse();
        if (guarded)
        {
            Matches(JsonSerializer.SerializeToUtf8Bytes(state with { IdleBindingPending = new(1, 2, "operation", state.GuardEvidence!.OwnerProof) }, serializer)).ShouldBeFalse();
            Matches(JsonSerializer.SerializeToUtf8Bytes(state with { GuardEvidence = state.GuardEvidence! with { IdleLifecycleRevision = 4 } }, serializer)).ShouldBeFalse();
            Matches(JsonSerializer.SerializeToUtf8Bytes(state with { GuardEvidence = null }, serializer)).ShouldBeFalse();
        }
    }

    /// <summary>Produces actual SDK wire, delivery, lifecycle and serialized epoch evidence for observer controls.</summary>
    internal static async Task<(StreamReadPage, ProjectionDeliveryState, byte[], JsonSerializerOptions)> ProduceAsync(bool guarded)
    {
        var identity = new AggregateIdentity("tenant", "folders", "folder");
        const string projection = "operational-status";
        DateTimeOffset now = DateTimeOffset.UtcNow;
        EventEnvelope[] events = Enumerable.Range(1, 2).Select(sequence => new EventEnvelope("01ARZ3NDEKTSV4RRFFQ69G5FA" + sequence, "folder", "Aggregate",
            "tenant", "folders", sequence, sequence + 100, now.AddSeconds(sequence), "01ARZ3NDEKTSV4RRFFQ69G5FAT", "01ARZ3NDEKTSV4RRFFQ69G5FAT", "actor", "v1",
            "ExampleEvent", 1, "json", System.Text.Encoding.UTF8.GetBytes("{\"value\":" + sequence + "}"), null) { PayloadVersion = 2 }).ToArray();
        var readable = await ProjectionEventWireBuilder.BuildAsync(new NoOpEventPayloadProtectionService(), identity, events, TestContext.Current.CancellationToken);
        readable.UnreadableReason.ShouldBeNull();
        readable.Events.ShouldNotBeNull();
        var history = ProjectionDeliveryFingerprint.ComputeHistory(identity, projection, readable.Events!, guarded);
        var source = new StreamReadPage("tenant", "folders", "folder", events.Select(item => new StreamReadEvent(item.SequenceNumber,
            item.EventTypeName, item.Payload, item.SerializationFormat, item.MetadataVersion, item.MessageId, item.CorrelationId, item.CausationId,
            item.Timestamp, item.UserId) { GlobalPosition = item.GlobalPosition, PayloadVersion = item.PayloadVersion }).ToArray(), new(0, null, 2, 2, 2, false, null));
        var sourceSeed = SourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        var sourceHead = events.Aggregate(sourceSeed, SourceProofCodec.Extend);
        var marker = guarded ? new ProjectionSourceGuard(1, 1, 2, 2, 1, "tenant", "folders", "folder", projection,
            sourceSeed.SourceIncarnation, new string('a', 64), "shared-epoch-v1", ProjectionSourceGuard.ApprovedSpecSha256, new string('b', 40), now) : null;
        ProjectionDeliveryState current = ProjectionDeliveryState.CreateEmpty(identity, projection, history.InitialFingerprint, now);
        if (guarded) current = current with { SchemaVersion = 2, GuardEvidenceVersion = 1 };
        long version = 1;
        var store = Substitute.For<IProjectionDeliveryStateStore>();
        store.ReadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ =>
            Task.FromResult(new ProjectionDeliveryStateReadResult(current, version.ToString(), ProjectionDeliveryStateClassification.Current, true) { SourceGuard = marker }));
        store.TrySaveAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<ProjectionDeliveryState>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => { if (call.ArgAt<string>(3) != version.ToString()) return Task.FromResult(false); current = call.ArgAt<ProjectionDeliveryState>(2); version++; return Task.FromResult(true); });
        var coordinator = new ProjectionDeliveryIdempotencyCoordinator(store, Options.Create(new ProjectionDeliveryIdempotencyOptions()), TimeProvider.System);
        ProjectionDeliveryGuardReservation? binding = guarded ? new(sourceSeed, sourceHead, marker!.CatalogFingerprint, marker.HandlerSemantics, "generation", null) : null;
        var admission = await coordinator.TryAdmitHistoryAsync(identity, projection, history, false, TestContext.Current.CancellationToken, guardBinding: binding);
        admission.Reservation.ShouldNotBeNull();
        var host = ActorHost.CreateForTest<ProjectionLifecycleActor>(new ActorTestOptions { ActorId = new ActorId(identity.ActorId + ":" + projection) });
        var manager = new InMemoryStateManager();
        var actor = new ProjectionLifecycleActor(host, NullLogger<ProjectionLifecycleActor>.Instance);
        typeof(Actor).GetProperty("StateManager", BindingFlags.Public | BindingFlags.Instance)!.SetValue(actor, manager);
        (await actor.BeginDeliveryWriteAsync(new("operation", now.AddMinutes(5)))).ShouldBeTrue();
        ProjectionOwnerProof owner = new(new(1, "tenant", "folders", "folder", projection, "owner", "generation", 2,
            "01ARZ3NDEKTSV4RRFFQ69G5FA2", "batch", "freshness"), "receipt", "v1:" + Convert.ToBase64String(new byte[32]).TrimEnd('='));
        if (guarded) (await coordinator.PrepareIdleBindingAsync(identity, projection, admission.Reservation!, "operation", owner,
            TestContext.Current.CancellationToken)).ShouldBeTrue();
        (await actor.CompleteDeliveryWriteAsync(new("operation"))).ShouldBeTrue();
        manager.CommittedState.ContainsKey("projection-lifecycle").ShouldBeFalse();
        long epoch = (long)manager.CommittedState["projection-lifecycle-epoch"];
        var evidence = guarded ? new ProjectionDeliveryGuardEvidence(sourceHead, marker!.CatalogFingerprint, marker.HandlerSemantics, owner, epoch) : null;
        (await coordinator.CompleteHistoryAsync(identity, projection, history, admission.Reservation!, TestContext.Current.CancellationToken, evidence))
            .ShouldBe(ProjectionDeliveryCompletion.Completed);
        using DaprClient client = new DaprClientBuilder().Build();
        return (source, current, JsonSerializer.SerializeToUtf8Bytes(epoch, client.JsonSerializerOptions), client.JsonSerializerOptions);
    }
}
