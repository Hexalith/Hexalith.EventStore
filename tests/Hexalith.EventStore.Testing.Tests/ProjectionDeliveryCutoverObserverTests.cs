using System.Text.Json;
using System.Text.Json.Nodes;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Testing.Tests;

/// <summary>Exercises fresh cutover observations with real SDK marker, ledger, reconciliation and lifecycle producers.</summary>
public sealed class ProjectionDeliveryCutoverObserverTests
{
    /// <summary>The actual cutover service produces the marker; wrong identity, time, schema, missing fields or duplicate fields refuse.</summary>
    [Fact]
    public async Task ActualCutoverMarkerRequiresExactCommitAndActivationWindow()
    {
        const string commit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        using DaprClient client = new DaprClientBuilder().Build();
        var serializer = client.JsonSerializerOptions;
        var store = Substitute.For<IProjectionDeliveryStateStore>();
        ProjectionDeliveryWriterProtocol? actual = null;
        store.TryActivateWriterProtocolAsync(Arg.Any<ProjectionDeliveryWriterProtocol>(), Arg.Any<CancellationToken>())
            .Returns(call => { actual = call.Arg<ProjectionDeliveryWriterProtocol>(); return true; });
        var cutover = new ProjectionDeliveryCutover(store, TimeProvider.System);
        DateTimeOffset started = DateTimeOffset.UtcNow;
        (await cutover.ActivateAsync(new(commit, "sha256:actual-native-backup", false, true, true), TestContext.Current.CancellationToken))
            .ShouldBe(ProjectionDeliveryCutoverStatus.PreconditionsFailed);
        actual.ShouldBeNull();
        (await cutover.ActivateAsync(new(commit, "sha256:actual-native-backup", true, true, true), TestContext.Current.CancellationToken))
            .ShouldBe(ProjectionDeliveryCutoverStatus.Activated);
        actual.ShouldNotBeNull();
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(actual, serializer);
        bool Matches(byte[]? candidate = null, string expected = commit, DateTimeOffset? begin = null, DateTimeOffset? end = null)
            => ProjectionDeliveryCutoverObserver.WriterProtocolMatches(candidate ?? bytes, expected, begin ?? started,
                end ?? DateTimeOffset.UtcNow, serializer);
        Matches().ShouldBeTrue();
        Matches(expected: new string('b', 40)).ShouldBeFalse(); Matches(expected: "not-a-commit").ShouldBeFalse();
        Matches(begin: DateTimeOffset.UtcNow.AddSeconds(1)).ShouldBeFalse(); Matches(end: started.AddSeconds(-1)).ShouldBeFalse();
        Matches([]).ShouldBeFalse();
        var original = JsonSerializer.SerializeToNode(actual, serializer)!.AsObject();
        foreach (string field in new[] { "schemaVersion", "writerProtocolVersion", "cutoverCommit", "activatedAt", "isCurrent" })
        {
            var missing = (JsonObject)original.DeepClone(); missing.Remove(field);
            Matches(JsonSerializer.SerializeToUtf8Bytes(missing)).ShouldBeFalse(field);
        }
        foreach ((string field, JsonNode? value) in new (string, JsonNode?)[]
        {
            ("schemaVersion", JsonValue.Create(2)), ("writerProtocolVersion", JsonValue.Create(1)),
            ("cutoverCommit", JsonValue.Create(new string('b', 40))), ("isCurrent", JsonValue.Create(false)),
        })
        {
            var changed = (JsonObject)original.DeepClone(); changed[field] = value;
            Matches(JsonSerializer.SerializeToUtf8Bytes(changed)).ShouldBeFalse(field);
        }
        Matches(System.Text.Encoding.UTF8.GetBytes("{\"schemaVersion\":1," + original.ToJsonString()[1..])).ShouldBeFalse();
    }

    /// <summary>Real current ledgers retain empty-state semantics; active, missing, malformed, foreign or unknown delivery records fail closed.</summary>
    [Fact]
    public void ActualLedgerSerializationNeverTreatsMissingOrMalformedItemsAsIdle()
    {
        using DaprClient client = new DaprClientBuilder().Build();
        var serializer = client.JsonSerializerOptions;
        var observed = new Dictionary<string, ReadOnlyMemory<byte>>
        {
            ["projection-activation:outbox:v1:0a"] = JsonSerializer.SerializeToUtf8Bytes(new ProjectionActivationLedger([]) { LastRevision = 5 }, serializer),
            ["projection-delivery-retry:ledger:v2:3f"] = JsonSerializer.SerializeToUtf8Bytes(new ProjectionDeliveryRetryLedger([]), serializer),
            ["projection-delivery-retry:protocol"] = JsonSerializer.SerializeToUtf8Bytes("v2-ready:v1-writers-quiesced", serializer),
        };
        bool Matches() => ProjectionDeliveryCutoverObserver.IsFreshStoreQuiescent(observed, serializer);
        Matches().ShouldBeTrue();
        foreach (string malformed in new[] { "{}", "{\"items\":null}", "{\"items\":[]}", "{\"items\":[],\"lastRevision\":-1}",
            "{\"items\":[],\"items\":[],\"lastRevision\":1}", "{\"items\":[],\"lastRevision\":1,\"unexpected\":true}" })
        {
            var old = observed["projection-activation:outbox:v1:0a"];
            observed["projection-activation:outbox:v1:0a"] = System.Text.Encoding.UTF8.GetBytes(malformed);
            Matches().ShouldBeFalse(malformed); observed["projection-activation:outbox:v1:0a"] = old;
        }
        var current = observed["projection-activation:outbox:v1:0a"];
        observed["projection-activation:outbox:v1:0a"] = JsonSerializer.SerializeToUtf8Bytes(new ProjectionActivationLedger(
            [ProjectionActivationWorkItem.Create(new("tenant", "domain", "aggregate"), DateTimeOffset.UtcNow)]) { LastRevision = 1 }, serializer);
        Matches().ShouldBeFalse(); observed["projection-activation:outbox:v1:0a"] = current;
        foreach (string unexpected in new[] { ProjectionDeliveryCutoverObserver.WriterProtocolKey, "projection-checkpoints:foreign",
            "projection-delivery-retry:lease:v2:active", "projection-delivery-retry:ledger:v1", "projection-rebuild:unknown",
            "eventstore||ProjectionLifecycleActor||scope||projection-lifecycle", "projection-activation:outbox:v1:ff", "foreign" })
        {
            observed[unexpected] = "{}"u8.ToArray(); Matches().ShouldBeFalse(unexpected); observed.Remove(unexpected);
        }
        observed["projection-delivery-retry:protocol"] = "\"unknown\""u8.ToArray(); Matches().ShouldBeFalse();
    }

    /// <summary>Actual legacy hydration preserves source head and requires exact complete receipts, real idle epoch, scope and operator afterwards.</summary>
    [Fact]
    public async Task ActualReconcilerHydratesOnlyTheKnownCompletedBootstrapScope()
    {
        var (source, _, epoch, serializer) = await ProjectionDeliveryCompletionObserverTests.ProduceAsync(false);
        var identity = new AggregateIdentity(source.Tenant, source.Domain, source.AggregateId!);
        const string projection = "operational-status";
        string key = ProjectionDeliveryCompletionObserver.CheckpointKey(source.Tenant, source.Domain, source.AggregateId!, projection);
        string actor = "eventstore||ProjectionLifecycleActor||" + ProjectionDeliveryCompletionObserver.LifecycleActorId(source.Tenant, source.Domain, source.AggregateId!, projection);
        var legacy = new ProjectionCheckpoint(source.Tenant, source.Domain, source.AggregateId!, source.Metadata.LatestSequence, DateTimeOffset.UtcNow);
        var observed = new Dictionary<string, ReadOnlyMemory<byte>>
        {
            [key] = JsonSerializer.SerializeToUtf8Bytes(legacy, serializer),
            [actor + "||projection-lifecycle-epoch"] = epoch,
            [ProjectionCheckpointTracker.GetMigratedMarkerKey(identity, projection)] = "{\"migrated\":true}"u8.ToArray(),
        };
        bool Matches(bool allowLegacy = false, string user = "operator") => ProjectionDeliveryCutoverObserver.IsQuiescentWithCompletedSource(observed,
            source, projection, user, allowLegacy, serializer);
        Matches(true).ShouldBeTrue(); Matches().ShouldBeFalse();
        var store = Substitute.For<IProjectionDeliveryStateStore>();
        var prior = JsonSerializer.Deserialize<ProjectionDeliveryState>(observed[key].Span, serializer)!;
        store.ReadAsync(identity, projection, Arg.Any<CancellationToken>()).Returns(new ProjectionDeliveryStateReadResult(prior, "1",
            ProjectionDeliveryStateClassification.LegacyNonZero, false));
        store.TrySaveWithReconciliationAsync(identity, projection, Arg.Any<ProjectionDeliveryState>(), Arg.Any<ProjectionDeliveryReconciliationWork>(),
            "1", Arg.Any<CancellationToken>()).Returns(call =>
        {
            observed[key] = ProjectionDeliveryStateSerialization.Serialize(call.Arg<ProjectionDeliveryState>(), serializer, 256);
            observed[ProjectionDeliveryStateKeys.GetReconciliationKey(identity, projection)] = JsonSerializer.SerializeToUtf8Bytes(call.Arg<ProjectionDeliveryReconciliationWork>(), serializer);
            return true;
        });
        var reader = Substitute.For<IProjectionDeliveryHistoryReader>();
        reader.ReadAsync(identity, source.Metadata.LatestSequence, Arg.Any<CancellationToken>()).Returns(source.Events.Select(item =>
            new ProjectionEventDto(item.EventTypeName, item.Payload, item.SerializationFormat, item.SequenceNumber, item.Timestamp,
                item.CorrelationId!, item.MessageId, item.UserId, item.GlobalPosition)
            { MetadataVersion = item.MetadataVersion, StoredPayloadVersion = item.PayloadVersion }).ToArray());
        var reconciler = new ProjectionDeliveryReconciler(store, reader, Options.Create(new ProjectionDeliveryIdempotencyOptions()), TimeProvider.System);
        var result = await reconciler.ReconcileFromEventStoreAsync(identity, projection, "operator", TestContext.Current.CancellationToken);
        result.Status.ShouldBe(ProjectionDeliveryReconciliationStatus.Completed);
        result.PreservedSequence.ShouldBe(source.Metadata.LatestSequence);
        Matches().ShouldBeTrue(); Matches(true).ShouldBeFalse(); Matches(user: "foreign").ShouldBeFalse();
        string workKey = ProjectionDeliveryStateKeys.GetReconciliationKey(identity, projection);
        byte[] workBytes = observed[workKey].ToArray();
        foreach (string field in new[] { "recordedAt", "operatorId", "reasonCode", "observedSequence" })
        {
            var missing = JsonNode.Parse(workBytes)!.AsObject(); missing.Remove(field);
            observed[workKey] = JsonSerializer.SerializeToUtf8Bytes(missing); Matches().ShouldBeFalse(field);
        }
        observed[workKey] = workBytes;
        observed[actor + "||projection-lifecycle-epoch"] = "0"u8.ToArray(); Matches().ShouldBeFalse();
        observed[actor + "||projection-lifecycle-epoch"] = epoch;
        observed[actor + "||projection-lifecycle"] = "{}"u8.ToArray(); Matches().ShouldBeFalse(); observed.Remove(actor + "||projection-lifecycle");
        observed.Remove(actor + "||projection-lifecycle-epoch"); Matches().ShouldBeFalse(); observed[actor + "||projection-lifecycle-epoch"] = epoch;
        observed["projection-checkpoints:unknown"] = "{}"u8.ToArray(); Matches().ShouldBeFalse(); observed.Remove("projection-checkpoints:unknown");
        byte[] valid = observed[key].ToArray();
        var changed = JsonNode.Parse(valid)!.AsObject(); changed["lastCompletedMessageId"] = "changed";
        observed[key] = JsonSerializer.SerializeToUtf8Bytes(changed); Matches().ShouldBeFalse(); observed[key] = valid;
        ProjectionDeliveryCutoverObserver.IsQuiescentWithCompletedSource(observed, source with { Tenant = "foreign" }, projection,
            "operator", false, serializer).ShouldBeFalse();
    }
}
