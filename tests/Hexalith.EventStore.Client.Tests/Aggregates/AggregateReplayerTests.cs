using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Tests.Events;
using Hexalith.EventStore.Contracts.Aggregates;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Replay;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>
/// Tier 1 regression coverage for the canonical aggregate replay path introduced by
/// admin-ui-aggregate-state-replay-correctness. Asserts AC #1 (Apply-driven replay,
/// no payload deep-merge), AC #2 (explicit Failed/Partial), AC #5 (sequence ordering,
/// duplicate guard, all 7 failure categories), and the side-effect-free contract.
/// </summary>
public class AggregateReplayerTests {
    [Fact]
    public void Replay_RenamedV1AndCurrentV2ReachSameStateAsCurrentHistory()
    {
        ReplayEventEnvelope old = BuildEnvelope(1, "Old.Contracts.ValueRaised", "{\"Amount\":2}");
        ReplayEventEnvelope current = BuildEnvelope(2, typeof(VersionTwoTestEvent).FullName!, "{\"Value\":3}")
            with { StoredPayloadVersion = 2 };
        ReplayEventEnvelope equivalent = BuildEnvelope(1, typeof(VersionTwoTestEvent).FullName!, "{\"Value\":2}")
            with { StoredPayloadVersion = 2 };

        AggregateReconstructionResult historical = AggregateReplayer.Replay<VersionTwoTestState>(
            BuildRequest([old, current], 2));
        AggregateReconstructionResult allCurrent = AggregateReplayer.Replay<VersionTwoTestState>(
            BuildRequest([equivalent, current], 2));

        historical.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        historical.StateJson.ShouldBe(allCurrent.StateJson);
        System.Text.Json.JsonDocument.Parse(historical.StateJson!).RootElement.GetProperty("value").GetInt32().ShouldBe(5);
        old.Payload.ShouldBe("{\"Amount\":2}"u8.ToArray());
    }

    [Fact]
    public void Replay_MissingHistoricalRenameStepFailsTypedBeforeApply()
    {
        ReplayEventEnvelope old = BuildEnvelope(1, "Old.Contracts.ValueRaised", "{\"Amount\":2}")
            with { StoredPayloadVersion = 2 };

        AggregateReconstructionResult result = AggregateReplayer.Replay<VersionTwoTestState>(
            BuildRequest([old], 1));

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnsupportedVersion);
        result.LastAppliedSequenceNumber.ShouldBe(0);
        result.StateJson.ShouldBeNull();
        result.Message.ShouldContain("Old.Contracts.ValueRaised version 2");
    }

    [Fact]
    public void Replay_ThrowingUpcasterReportsSafeTypeNames()
    {
        string name = typeof(VersionTwoTestEvent).FullName!;
        var evolution = new EventPayloadEvolutionRegistry([typeof(VersionTwoTestEvent)],
            [new TestPayloadUpcaster(name, 1, null, static _ => throw new InvalidOperationException("secret payload detail"))]);
        ReplayEventEnvelope stored = BuildEnvelope(1, name, "{\"Amount\":2}");

        AggregateReconstructionResult result = AggregateReplayer.Replay<VersionTwoTestState>(
            BuildRequest([stored], 1), CancellationToken.None, evolution);

        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnsupportedVersion);
        result.Message.ShouldContain(typeof(TestPayloadUpcaster).FullName!);
        result.Message.ShouldContain(nameof(InvalidOperationException));
        result.Message.ShouldNotContain("secret payload detail");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rehydration_NotSupportedDeserializerFailsTypedForBothEventShapes(bool inlineJson)
    {
        string name = typeof(UnsupportedDeserializerEvent).FullName!;
        byte[] payload = "{\"value\":\"System.String\"}"u8.ToArray();
        object history = inlineJson
            ? JsonSerializer.SerializeToElement(new[] { new { eventTypeName = name, payload = new { value = "System.String" } } })
            : new DomainServiceCurrentState(null,
                [new EventEnvelope(new EventMetadata("msg-1", "counter-1", "Counter", "tenant-a", "counter",
                    1, 1, DateTimeOffset.UnixEpoch, "corr-1", "cause-1", "user", "v1", name, 1, "json"), payload, null)],
                0, 1);
        var evolution = new EventPayloadEvolutionRegistry([typeof(UnsupportedDeserializerEvent)], []);

        EventPayloadEvolutionException failure = Should.Throw<EventPayloadEvolutionException>(() =>
            DomainProcessorStateRehydrator.RehydrateState<UnsupportedDeserializerState>(history,
                DomainProcessorStateRehydrator.DiscoverApplyMethods(typeof(UnsupportedDeserializerState)), evolution: evolution));

        failure.EventTypeName.ShouldBe(name);
        failure.InnerExceptionTypeName.ShouldBe(nameof(NotSupportedException));
        failure.InnerException.ShouldBeNull();
    }

    /// <summary>Checks a versioned later event refuses the complete eligible batch before an earlier Apply can run.</summary>
    [Fact]
    public void Replay_VersionedLaterEventRefusesBeforeApplyAndRevealsNoPartialTimeline() {
        ReplayEventEnvelope first = BuildEnvelope(1, nameof(CounterIncremented));
        ReplayEventEnvelope second = BuildEnvelope(2, nameof(CounterIncremented)) with { StoredEventContractType = "evt", StoredPayloadVersion = 2 };
        AggregateReconstructionResult result = AggregateReplayer.Replay<FailingState>(BuildRequest([first, second], 2, includeTimeline: true));
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnsupportedVersion);
        result.LastAppliedSequenceNumber.ShouldBe(0);
        result.StateJson.ShouldBeNull();
        result.Timeline.ShouldBeNull();
    }

    // ---------------------------------------------------------------
    // Fixture: marker-event Counter aggregate (mirrors the seeded
    // tenant-a/counter/counter-1 stream). Empty-payload events make
    // payload deep-merge necessarily wrong: only Apply() can produce
    // the expected Count = 10 final state.
    // ---------------------------------------------------------------

    public sealed record CounterIncremented : IEventPayload;
    public sealed record CounterDecremented : IEventPayload;
    public sealed record CounterReset : IEventPayload;
    public sealed record CounterClosed : IEventPayload;
    public sealed record UnsupportedDeserializerEvent(Type Value) : IEventPayload;

    public sealed class CounterState : ITerminatable {
        public int Count { get; private set; }
        public bool IsTerminated { get; private set; }
        public void Apply(CounterIncremented e) => Count++;
        public void Apply(CounterDecremented e) => Count--;
        public void Apply(CounterReset e) => Count = 0;
        public void Apply(CounterClosed e) => IsTerminated = true;
    }

    /// <summary>Throws the first time a payload is applied. Used to drive ApplyFailed paths.</summary>
    public sealed class FailingState {
        public int CallCount { get; private set; }
        public void Apply(CounterIncremented e) {
            CallCount++;
            throw new InvalidOperationException("Apply boom.");
        }
    }

    /// <summary>State with no Apply method to drive ApplyHandlerMissing.</summary>
    public sealed class HandlerlessState {
        public int Unused { get; init; }
    }

    /// <summary>State whose event payload forces a non-JsonException serializer failure.</summary>
    public sealed class UnsupportedDeserializerState {
        public int Applied { get; private set; }

        public void Apply(UnsupportedDeserializerEvent e) => Applied++;
    }

    private static ReplayEventEnvelope BuildEnvelope(long sequence, string eventTypeName, string payloadJson = "{}")
        => new(
            SequenceNumber: sequence,
            EventTypeName: eventTypeName,
            Payload: Encoding.UTF8.GetBytes(payloadJson),
            SerializationFormat: "json",
            MetadataVersion: 1,
            MessageId: $"msg-{sequence}",
            CorrelationId: $"corr-{sequence}",
            CausationId: null);

    /// <summary>Canonical 18-event Counter fixture: 5 increments, 2 decrements, reset, 10 increments.</summary>
    private static ReplayEventEnvelope[] CanonicalCounterFixture() {
        var events = new List<ReplayEventEnvelope>(18);
        long seq = 0;
        for (int i = 0; i < 5; i++) {
            events.Add(BuildEnvelope(++seq, nameof(CounterIncremented)));
        }

        for (int i = 0; i < 2; i++) {
            events.Add(BuildEnvelope(++seq, nameof(CounterDecremented)));
        }

        events.Add(BuildEnvelope(++seq, nameof(CounterReset)));
        for (int i = 0; i < 10; i++) {
            events.Add(BuildEnvelope(++seq, nameof(CounterIncremented)));
        }

        events.Count.ShouldBe(18);
        return [.. events];
    }

    private static AggregateReconstructionRequest BuildRequest(
        IReadOnlyList<ReplayEventEnvelope> events,
        long upToSequence,
        bool includeTimeline = false)
        => new(
            TenantId: "tenant-a",
            Domain: "counter",
            AggregateType: "Counter",
            AggregateId: "counter-1",
            UpToSequence: upToSequence,
            Events: events,
            IncludeTimeline: includeTimeline,
            RequestId: null);

    private static int CountFromState(string? json) =>
        JsonDocument.Parse(json ?? "{}").RootElement.GetProperty("count").GetInt32();

    // ---------------------------------------------------------------
    // AC #1 — Apply-driven replay, canonical 18-event fixture
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(7, 3)]
    [InlineData(8, 0)]
    [InlineData(18, 10)]
    public void Replay_CanonicalCounterFixture_StateMatchesCheckpoint(long upTo, int expectedCount) {
        ReplayEventEnvelope[] events = CanonicalCounterFixture();

        AggregateReconstructionResult result = AggregateReplayer.Replay<CounterState>(BuildRequest(events, upTo));

        result.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        result.LastAppliedSequenceNumber.ShouldBe(upTo);
        CountFromState(result.StateJson).ShouldBe(expectedCount);
    }

    [Fact]
    public void Replay_CanonicalCounterFixture_AtSequence18_Returns10NotZero() {
        // Guard: payload deep-merge would return {} (count == 0) for marker events. Apply-driven
        // replay produces count = 10. This is the headline correctness assertion in AC #1.
        ReplayEventEnvelope[] events = CanonicalCounterFixture();

        AggregateReconstructionResult result = AggregateReplayer.Replay<CounterState>(BuildRequest(events, 18));

        result.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        CountFromState(result.StateJson).ShouldBe(10);
        JsonDocument.Parse(result.StateJson!).RootElement.GetProperty("isTerminated").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public void Replay_UpToSequenceZero_ReturnsInitialState_WithoutInvokingApply() {
        AggregateReconstructionResult result = AggregateReplayer.Replay<CounterState>(BuildRequest(CanonicalCounterFixture(), 0));

        result.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        result.LastAppliedSequenceNumber.ShouldBe(0);
        CountFromState(result.StateJson).ShouldBe(0);
    }

    [Fact]
    public void Replay_UpToSequenceBeyondLastEvent_ReturnsLastValidState() {
        ReplayEventEnvelope[] events = CanonicalCounterFixture();

        AggregateReconstructionResult result = AggregateReplayer.Replay<CounterState>(BuildRequest(events, 1000));

        result.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        result.LastAppliedSequenceNumber.ShouldBe(18);
        CountFromState(result.StateJson).ShouldBe(10);
    }

    [Fact]
    public void Replay_OutOfOrderInput_AppliesInSequenceOrder() {
        ReplayEventEnvelope[] shuffled = CanonicalCounterFixture()
            .OrderBy(e => e.SequenceNumber * 7 % 17)
            .ToArray();

        AggregateReconstructionResult result = AggregateReplayer.Replay<CounterState>(BuildRequest(shuffled, 18));

        result.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        CountFromState(result.StateJson).ShouldBe(10);
    }

    [Fact]
    public void Replay_DuplicateSequenceNumber_FailsExplicitly() {
        // R2-A6 / story Replay Semantics: duplicate sequence/version must fail rather than
        // silently pick one ordering.
        ReplayEventEnvelope[] events = [
            BuildEnvelope(1, nameof(CounterIncremented)),
            BuildEnvelope(1, nameof(CounterIncremented)),
            BuildEnvelope(2, nameof(CounterIncremented)),
        ];

        AggregateReconstructionResult result = AggregateReplayer.Replay<CounterState>(BuildRequest(events, 2));

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.Unexpected);
        result.FailedSequenceNumber.ShouldBe(1);
        _ = result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("Duplicate");
    }

    [Fact]
    public void Replay_MissingSequenceGap_FailsExplicitly() {
        ReplayEventEnvelope[] events = [
            BuildEnvelope(1, nameof(CounterIncremented)),
            BuildEnvelope(3, nameof(CounterIncremented)),
        ];

        AggregateReconstructionResult result = AggregateReplayer.Replay<CounterState>(BuildRequest(events, 3));

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.Unexpected);
        result.FailedSequenceNumber.ShouldBe(2);
        _ = result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("Missing stream sequence");
    }

    [Fact]
    public void Replay_MissingInitialSequence_FailsExplicitly() {
        ReplayEventEnvelope[] events = [
            BuildEnvelope(2, nameof(CounterIncremented)),
        ];

        AggregateReconstructionResult result = AggregateReplayer.Replay<CounterState>(BuildRequest(events, 2));

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.Unexpected);
        result.FailedSequenceNumber.ShouldBe(1);
    }

    [Fact]
    public void Replay_IncludeTimeline_EmitsPerEventStateSnapshots() {
        ReplayEventEnvelope[] events = CanonicalCounterFixture();

        AggregateReconstructionResult result = AggregateReplayer.Replay<CounterState>(BuildRequest(events, 18, includeTimeline: true));

        result.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        _ = result.Timeline.ShouldNotBeNull();
        result.Timeline!.Count.ShouldBe(18);
        CountFromState(result.Timeline[0].StateJson).ShouldBe(1);
        CountFromState(result.Timeline[4].StateJson).ShouldBe(5);
        CountFromState(result.Timeline[6].StateJson).ShouldBe(3);
        CountFromState(result.Timeline[7].StateJson).ShouldBe(0);
        CountFromState(result.Timeline[17].StateJson).ShouldBe(10);
    }

    [Fact]
    public void Replay_IncludeTimelineFalse_DoesNotAllocateSnapshots() {
        AggregateReconstructionResult result = AggregateReplayer.Replay<CounterState>(BuildRequest(CanonicalCounterFixture(), 18));

        result.Timeline.ShouldBeNull();
    }

    // ---------------------------------------------------------------
    // AC #2 — Explicit Failed / Partial responses for the 7 categories
    // ---------------------------------------------------------------

    [Fact]
    public void Replay_UnknownEventType_FailsWithUnknownEventType() {
        ReplayEventEnvelope[] events = [
            BuildEnvelope(1, nameof(CounterIncremented)),
            BuildEnvelope(2, "TotallyUnregisteredEvent"),
        ];

        AggregateReconstructionResult result = AggregateReplayer.Replay<CounterState>(BuildRequest(events, 2));

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnknownEventType);
        result.FailedSequenceNumber.ShouldBe(2);
        result.FailedEventType.ShouldBe("TotallyUnregisteredEvent");
        result.LastAppliedSequenceNumber.ShouldBe(0);
    }

    [Fact]
    public void Replay_DeserializationFailure_FailsWithDeserializationFailed() {
        ReplayEventEnvelope[] events = [
            new ReplayEventEnvelope(
                SequenceNumber: 1,
                EventTypeName: nameof(CounterIncremented),
                Payload: Encoding.UTF8.GetBytes("{not valid json"),
                SerializationFormat: "json",
                MetadataVersion: 1,
                MessageId: "msg-1",
                CorrelationId: null,
                CausationId: null),
        ];

        AggregateReconstructionResult result = AggregateReplayer.Replay<CounterState>(BuildRequest(events, 1));

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.DeserializationFailed);
        result.FailedSequenceNumber.ShouldBe(1);
    }

    [Fact]
    public void Replay_NonJsonDeserializerFailure_FailsWithDeserializationFailed() {
        ReplayEventEnvelope[] events = [
            BuildEnvelope(1, nameof(UnsupportedDeserializerEvent), "{\"value\":\"System.String\"}"),
        ];

        AggregateReconstructionResult result = AggregateReplayer.Replay<UnsupportedDeserializerState>(BuildRequest(events, 1));

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.DeserializationFailed);
        result.FailedSequenceNumber.ShouldBe(1);
    }

    [Fact]
    public void Replay_StateWithNoApplyMethod_FailsWithApplyHandlerMissing() {
        // ApplyHandlerMissing is the canonical category when the state type has no Apply for
        // the named event. Distinguishes the "no public Apply at all" case from "wrong type name".
        ReplayEventEnvelope[] events = [
            BuildEnvelope(1, nameof(CounterIncremented)),
        ];

        AggregateReconstructionResult result = AggregateReplayer.Replay<HandlerlessState>(BuildRequest(events, 1));

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.ApplyHandlerMissing);
        result.FailedSequenceNumber.ShouldBe(1);
        result.FailedEventType.ShouldBe(nameof(CounterIncremented));
    }

    /// <summary>Checks the first failing Apply returns the unchanged initial canonical state.</summary>
    [Fact]
    public void Replay_ApplyMethodThrows_ReturnsPartialAtLastGoodSequence() {
        ReplayEventEnvelope[] events = [
            BuildEnvelope(1, nameof(CounterIncremented)),
            BuildEnvelope(2, nameof(CounterIncremented)),
        ];

        AggregateReconstructionResult result = AggregateReplayer.Replay<FailingState>(BuildRequest(events, 2));

        result.Status.ShouldBe(AggregateReconstructionStatus.Partial);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.ApplyFailed);
        result.LastAppliedSequenceNumber.ShouldBe(0);
        result.FailedSequenceNumber.ShouldBe(1);
        // Partial result preserves state up to last good event (which is empty here)
        _ = result.StateJson.ShouldNotBeNull();
        _ = result.Message.ShouldNotBeNull();
        result.Message.ShouldContain("Apply");
        using JsonDocument state = JsonDocument.Parse(result.StateJson);
        state.RootElement.GetProperty("callCount").GetInt32().ShouldBe(0);
    }

    /// <summary>Checks failed mutations cannot change the returned state or earlier timeline.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Replay_LaterApplyMutatesThenThrows_ReturnsThePreviousCanonicalState(bool includeTimeline) {
        ReplayEventEnvelope[] events = [
            BuildEnvelope(1, nameof(CounterIncremented)),
            BuildEnvelope(2, nameof(CounterDecremented)),
        ];
        AggregateReconstructionResult result = AggregateReplayer.Replay<MutatingReplayFailureState>(
            BuildRequest(events, 2, includeTimeline));

        result.Status.ShouldBe(AggregateReconstructionStatus.Partial);
        result.LastAppliedSequenceNumber.ShouldBe(1);
        result.FailedSequenceNumber.ShouldBe(2);
        CountFromState(result.StateJson).ShouldBe(1);
        if (includeTimeline) {
            result.Timeline.ShouldNotBeNull().Count.ShouldBe(1);
            result.Timeline[0].StateJson.ShouldBe(result.StateJson);
        }
        else {
            result.Timeline.ShouldBeNull();
        }
    }

    /// <summary>Checks initial serialization fails safely before Apply without leaking a getter error.</summary>
    [Fact]
    public void Replay_InitialStateSerializationFailure_ReturnsTypedFailureWithoutState() {
        AggregateReconstructionResult result = AggregateReplayer.Replay<UnserializableInitialReplayState>(
            BuildRequest([BuildEnvelope(1, nameof(CounterIncremented))], 1, includeTimeline: true));
        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.Unexpected);
        result.LastAppliedSequenceNumber.ShouldBe(0);
        result.StateJson.ShouldBeNull();
        result.Timeline.ShouldBeNull();
        result.Message.ShouldNotBeNull().ShouldNotContain("secret-initial-state");
    }

    /// <summary>Checks failed successor serialization emits no damaged or authoritative partial state.</summary>
    [Fact]
    public void Replay_StateSerializationFailureAfterApply_ReturnsTypedFailureWithoutState() {
        AggregateReconstructionResult result = AggregateReplayer.Replay<UnserializableReplayState>(
            BuildRequest([BuildEnvelope(1, nameof(CounterIncremented))], 1, includeTimeline: true));
        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.Unexpected);
        result.LastAppliedSequenceNumber.ShouldBe(0);
        result.FailedSequenceNumber.ShouldBe(1);
        result.StateJson.ShouldBeNull();
        result.Timeline.ShouldBeNull();
        result.Message.ShouldNotBeNull().ShouldNotContain("secret-payload-in-getter");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Replay_UnsupportedMetadataVersion_FailsWithUnsupportedVersion(int metadataVersion) {
        ReplayEventEnvelope[] events = [
            new ReplayEventEnvelope(
                SequenceNumber: 1,
                EventTypeName: nameof(CounterIncremented),
                Payload: Encoding.UTF8.GetBytes("{}"),
                SerializationFormat: "json",
                MetadataVersion: metadataVersion,
                MessageId: "msg-1",
                CorrelationId: null,
                CausationId: null),
        ];

        AggregateReconstructionResult result = AggregateReplayer.Replay<CounterState>(BuildRequest(events, 1));

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnsupportedVersion);
        result.StateJson.ShouldBeNull();
    }

    [Fact]
    public void Replay_UnsupportedSerializationFormat_FailsWithUnsupportedVersion() {
        ReplayEventEnvelope[] events = [
            new ReplayEventEnvelope(
                SequenceNumber: 1,
                EventTypeName: nameof(CounterIncremented),
                Payload: Encoding.UTF8.GetBytes("payload"),
                SerializationFormat: "protobuf",
                MetadataVersion: 1,
                MessageId: "msg-1",
                CorrelationId: null,
                CausationId: null),
        ];

        AggregateReconstructionResult result = AggregateReplayer.Replay<CounterState>(BuildRequest(events, 1));

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnsupportedVersion);
    }

    [Fact]
    public void Replay_MissingEventTypeName_FailsWithUnknownEventType() {
        ReplayEventEnvelope[] events = [
            new ReplayEventEnvelope(
                SequenceNumber: 1,
                EventTypeName: string.Empty,
                Payload: Encoding.UTF8.GetBytes("{}"),
                SerializationFormat: "json",
                MetadataVersion: 1,
                MessageId: "msg-1",
                CorrelationId: null,
                CausationId: null),
        ];

        AggregateReconstructionResult result = AggregateReplayer.Replay<CounterState>(BuildRequest(events, 1));

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnknownEventType);
    }

    // ---------------------------------------------------------------
    // Side-effect-free contract
    // ---------------------------------------------------------------

    [Fact]
    public void Replay_RepeatedInvocations_ReturnIdenticalState() {
        // The replayer must be pure: a fresh TState() each call. If state were shared across
        // calls the second invocation would see Count = 20 instead of 10.
        ReplayEventEnvelope[] events = CanonicalCounterFixture();

        AggregateReconstructionResult first = AggregateReplayer.Replay<CounterState>(BuildRequest(events, 18));
        AggregateReconstructionResult second = AggregateReplayer.Replay<CounterState>(BuildRequest(events, 18));

        CountFromState(first.StateJson).ShouldBe(10);
        CountFromState(second.StateJson).ShouldBe(10);
        first.StateJson.ShouldBe(second.StateJson);
    }

    [Fact]
    public void Replay_DoesNotMutateRequestEnvelopes() {
        ReplayEventEnvelope[] events = CanonicalCounterFixture();
        long[] originalSequences = events.Select(e => e.SequenceNumber).ToArray();
        byte[][] originalPayloads = events.Select(e => (byte[])e.Payload.Clone()).ToArray();

        _ = AggregateReplayer.Replay<CounterState>(BuildRequest(events, 18));

        for (int i = 0; i < events.Length; i++) {
            events[i].SequenceNumber.ShouldBe(originalSequences[i]);
            events[i].Payload.ShouldBe(originalPayloads[i]);
        }
    }

    [Fact]
    public void Replay_EventsBeyondUpToSequence_AreIgnored() {
        ReplayEventEnvelope[] events = CanonicalCounterFixture();

        AggregateReconstructionResult result = AggregateReplayer.Replay<CounterState>(BuildRequest(events, 5));

        result.LastAppliedSequenceNumber.ShouldBe(5);
        CountFromState(result.StateJson).ShouldBe(5);
    }
}
