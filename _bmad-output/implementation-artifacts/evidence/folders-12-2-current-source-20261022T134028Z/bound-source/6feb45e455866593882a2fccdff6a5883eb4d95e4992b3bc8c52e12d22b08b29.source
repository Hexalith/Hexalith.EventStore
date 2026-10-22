using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Serialization;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.Actors;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Checks canonical event and folded-state parity across the production storage seams.</summary>
public sealed class FoldedSnapshotParityTests
{
    [Fact]
    public async Task CustomAdmittedEventSnapshotAndTailEqualFullPrefixReplay()
    {
        var identity = new AggregateIdentity("tenant", "parity", "aggregate");
        var processor = new ParityProcessor();
        using ServiceProvider services = new ServiceCollection()
            .AddKeyedSingleton<IAsyncDomainProcessor>(identity.Domain, processor)
            .AddEventStoreBoundedV1DomainSerialization(identity.Domain, profile => profile.Add<ParityEvent>(
                typeof(ParityEvent).FullName!, "json", 128,
                static (_, sink, _) => { sink.Write("{\"amount\":2}"u8); return Task.CompletedTask; }))
            .AddEventStoreFoldedSnapshotQualification<ParityState>(identity.Domain, "aggregate", "parity-profile",
                "persisted-byte-and-json-restore-parity", SnapshotTestLimits.ForState())
            .BuildServiceProvider();
        var command = new CommandEnvelope("01HZX6FKA8B3YTX6MBQ3AYT3PT", identity.TenantId, identity.Domain,
            identity.AggregateId, "Create", [], "01HZX6FKA8B3YTX6MBQ3AYT3PT", null, "user", null);

        DomainServiceWireResult wire = await DomainServiceRequestRouter.ProcessAsync(services,
            new DomainServiceRequest(command, null)
            {
                IncludePostCommandState = true,
                SnapshotCaptureProfileId = "parity-profile",
            });
        wire.Events.ShouldHaveSingleItem().Payload.ShouldBe("{\"amount\":2}"u8.ToArray());
        processor.ReturnedEvent.ShouldNotBeNull().Amount.ShouldBe(99);
        processor.ReturnedEvent.Defaulted.ShouldBe(7);
        processor.ReturnedEvent.Ignored.ShouldBe(11);

        using var response = new MemoryStream();
        await BoundedV1WireResultResponse.WriteAsync(response, wire, CancellationToken.None);
        response.Position = 0;
        using var parser = new BoundedV1DomainResponseParser(response, CancellationToken.None);
        DomainServiceWireResult admitted = await parser.ParseAsync();
        DomainResult admittedResult = DaprDomainServiceInvoker.ToDomainResult(admitted);
        admittedResult.PostCommandStateJson.ShouldBe(wire.PostCommandStateJson);

        var store = new InMemoryStateManager();
        var protection = new NoOpEventPayloadProtectionService();
        var persister = new EventPersister(store, NullLogger<EventPersister>.Instance, protection);
        EventPersistResult prefix = await persister.PersistEventsAsync(identity, "aggregate", command, admittedResult, "v1");
        prefix.PersistedEnvelopes.ShouldHaveSingleItem().Payload.ShouldBe(wire.Events[0].Payload);

        AggregateReconstructionResult manualPrefix = Replay(identity, prefix.PersistedEnvelopes);
        manualPrefix.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        manualPrefix.StateJson.ShouldBe(wire.PostCommandStateJson);
        using JsonDocument stateDocument = JsonDocument.Parse(admittedResult.PostCommandStateJson.ShouldNotBeNull());
        var manager = new SnapshotManager(Options.Create(new SnapshotOptions()),
            NullLogger<SnapshotManager>.Instance, protection);
        await manager.CreateSnapshotAsync(identity, 1, stateDocument.RootElement.Clone(), store,
            JsonSerializerOptions.Web, SnapshotTestLimits.ForState(), throwOnFailure: true);
        await store.SaveStateAsync();

        SnapshotRecord stored = (await manager.LoadSnapshotAsync(identity, store)).ShouldNotBeNull();
        stored.SnapshotEnvelopeVersion.ShouldBe(1);
        Encoding.UTF8.GetBytes(stored.State.ShouldBeOfType<JsonElement>().GetRawText())
            .ShouldBe(Encoding.UTF8.GetBytes(manualPrefix.StateJson.ShouldNotBeNull()));
        byte[] autoBytes = JsonSerializer.SerializeToUtf8Bytes(stored.State, EventStorePayloadSerialization.Options);
        using JsonDocument manualDocument = JsonDocument.Parse(manualPrefix.StateJson.ShouldNotBeNull());
        byte[] manualBytes = JsonSerializer.SerializeToUtf8Bytes(
            manualDocument.RootElement, EventStorePayloadSerialization.Options);
        autoBytes.ShouldBe(manualBytes);

        // A later event proves that JSON-property restoration of the persisted fold survives Apply.
        var tailCommand = command with { MessageId = "01HZX6FKA8B3YTX6MBQ3AYT3PV" };
        var tailResult = new DomainResult([new SerializedDomainEventPayload(
            typeof(ParityEvent).FullName!, "{\"amount\":3}"u8.ToArray(), "json", 1, null, null)]);
        EventPersistResult tail = await persister.PersistEventsAsync(identity, "aggregate", tailCommand, tailResult, "v1");
        await store.SaveStateAsync();
        RehydrationResult snapshotHistory = (await new EventStreamReader(store,
            NullLogger<EventStreamReader>.Instance).RehydrateAsync(identity, stored)).ShouldNotBeNull();
        snapshotHistory.LastSnapshotSequence.ShouldBe(1);
        snapshotHistory.Events.ShouldHaveSingleItem().Payload.ShouldBe(tail.PersistedEnvelopes[0].Payload);

        var snapshotCurrent = new DomainServiceCurrentState(snapshotHistory.SnapshotState,
            snapshotHistory.Events.Select(AggregateActor.ToContractEventEnvelope).ToArray(), 1, 2);
        ParityState restoredWithTail = DomainProcessorStateRehydrator.RehydrateState<ParityState>(
            snapshotCurrent, DomainProcessorStateRehydrator.DiscoverApplyMethods(typeof(ParityState))).ShouldNotBeNull();
        AggregateReconstructionResult full = Replay(identity, [.. prefix.PersistedEnvelopes, .. tail.PersistedEnvelopes]);
        full.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        JsonSerializer.Serialize(restoredWithTail, EventStorePayloadSerialization.Options).ShouldBe(full.StateJson);
        restoredWithTail.Total.ShouldBe(5);
        restoredWithTail.EventCount.ShouldBe(2);
    }

    private static AggregateReconstructionResult Replay(AggregateIdentity identity, IReadOnlyList<EventEnvelope> events)
        => AggregateReplayer.Replay<ParityState>(new AggregateReconstructionRequest(
            identity.TenantId, identity.Domain, "aggregate", identity.AggregateId, events.Count,
            events.Select(static e => new ReplayEventEnvelope(e.SequenceNumber, e.EventTypeName, e.Payload,
                e.SerializationFormat, e.MetadataVersion, e.MessageId, e.CorrelationId, e.CausationId)).ToArray(),
            false, null));

}
