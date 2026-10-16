
using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Actors;

public class AggregateActorGetEventsTests {
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task MeasuredBoundedRange_EmptyRangeAbsentMetadataAndTouchedEmptyAllChargeTheirActualEmptyResponse(int mode)
    {
        (AggregateActor actor, IActorStateManager state) = CreateActor();
        if (mode == 2) ConfigureMetadata(state, 0);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();

        SourceEventRange response = await actor.ReadMeasuredEventsBoundedRangeAsync(0, mode == 0 ? 0 : 1, 256, 2_097_152);

        response.Events.ShouldBeEmpty();
        response.EnvelopeArrayBytes.ShouldBe(2);
        response.ReasonCode.ShouldBeNull();
        cost[ProjectionDeliveryCost.SourceRanges].ShouldBe(1);
        cost[ProjectionDeliveryCost.SourceMetadataReads].ShouldBe(mode == 0 ? 0 : 1);
        cost[ProjectionDeliveryCost.SourceEventReads].ShouldBe(0);
        cost[ProjectionDeliveryCost.SourceReturnedEvents].ShouldBe(0);
        cost[ProjectionDeliveryCost.SourceResponses].ShouldBe(1);
        cost[ProjectionDeliveryCost.SourceResponseBytes].ShouldBe(2);
        cost[ProjectionDeliveryCost.SourceResponseSerializationBytes].ShouldBe(2);
        await state.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BoundedRange_FailedTrackerUnloadFencesReadOnlyCurrentUntilTheExistingBarrierRecovers()
    {
        var state = new FaultInjectingActorStateManager();
        await state.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [MetadataKey] = new AggregateMetadata(1, DateTimeOffset.UtcNow, null),
            [EventKeyPrefix + "1"] = CreateEvent(1),
        });
        state.FaultOnCall("ClearCache", 2, new IOException("fixture read-tracker failure"));
        (AggregateActor actor, _) = CreateActor(state);
        await Should.ThrowAsync<IOException>(() => actor.ReadMeasuredEventsBoundedRangeAsync(0, 1, 256, 2_097_152));
        int beforeInspection = state.Trace.Count;
        (await Should.ThrowAsync<InvalidOperationException>(() => actor.InspectSourceProofAsync())).Message.ShouldBe("delivery_state_unavailable");
        state.Trace.Count.ShouldBe(beforeInspection);
        (await actor.ReadMeasuredEventsBoundedRangeAsync(0, 1, 256, 2_097_152)).Events.ShouldHaveSingleItem();
        (await actor.InspectSourceProofAsync()).CurrentSequence.ShouldBe(1);
        state.Trace.ShouldNotContain("SaveState");
        state.CommittedState.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoundedRange_UnloadsOwningReadTrackerAfterEveryReturnedPageAndValidationFailure(bool invalid)
    {
        var state = new FaultInjectingActorStateManager();
        await state.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [MetadataKey] = new AggregateMetadata(2, DateTimeOffset.UtcNow, null),
            [EventKeyPrefix + "1"] = CreateEvent(1),
            [EventKeyPrefix + "2"] = CreateEvent(2) with { TenantId = invalid ? "foreign" : "tenant-a" },
        });
        (AggregateActor actor, _) = CreateActor(state);
        if (invalid) (await actor.ReadMeasuredEventsBoundedRangeAsync(0, 2, 256, 2_097_152)).ReasonCode.ShouldBe("source_conflict");
        else (await actor.ReadMeasuredEventsBoundedRangeAsync(0, 2, 256, 2_097_152)).Events.Length.ShouldBe(2);
        state.Trace[0].ShouldBe("ClearCache");
        state.Trace[^2].ShouldBe("ClearCache");
        state.Trace[^1].ShouldBe("ClearCache:Delegated");
        state.Trace.Count(static item => item == "ClearCache").ShouldBe(2);
        state.Trace.ShouldNotContain("SaveState");
        state.CommittedState.Count.ShouldBe(3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceInspection_AbsentMetadataChargesErasureProgressAndDistinguishesItFromEmptyNoWork(bool completed)
    {
        var state = new FaultInjectingActorStateManager();
        var progress = new TrustedEffectErasureProgress(70, 71, completed, "erasure-nonce");
        await state.SeedCommittedStateAsync(new Dictionary<string, object> { [TrustedEffectErasureProgress.StateName] = progress });
        (AggregateActor actor, _) = CreateActor(state);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();

        SourceProofInspection observed = await actor.InspectSourceProofAsync();

        observed.Erased.ShouldBeTrue();
        observed.Exists.ShouldBeFalse();
        observed.CurrentSequence.ShouldBe(0);
        observed.Status.ShouldBe(completed ? SourceProofInspectionStatus.Unsupported : SourceProofInspectionStatus.Invalid);
        observed.Proof.ShouldBeNull();
        cost[ProjectionDeliveryCost.SourceMetadataReads].ShouldBe(2);
        int bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(progress, System.Text.Json.JsonSerializerOptions.Web).Length;
        cost[ProjectionDeliveryCost.SourceMetadataBytes].ShouldBe(bytes);
        cost[ProjectionDeliveryCost.MetadataSizingBytes].ShouldBe(bytes);
        cost[ProjectionDeliveryCost.SourceEventReads].ShouldBe(0);
        state.Trace.ShouldNotContain("SaveState");
    }

    [Theory]
    [InlineData("_stateCacheUnsafe")]
    [InlineData("_pendingFinalizerRecoveryRequired")]
    [InlineData("_pendingCountReconciliationRequired")]
    public async Task SourceInspection_RequiredCacheRecoveryCannotRunInsideTheReadOnlyCurrentProbe(string recoveryField)
    {
        (AggregateActor actor, IActorStateManager state) = CreateActor();
        typeof(AggregateActor).GetField(recoveryField, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(actor, true);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();

        (await Should.ThrowAsync<InvalidOperationException>(() => actor.InspectSourceProofAsync()))
            .Message.ShouldBe("delivery_state_unavailable");

        state.ReceivedCalls().ShouldBeEmpty();
        cost[ProjectionDeliveryCost.SourceInspections].ShouldBe(1);
        cost[ProjectionDeliveryCost.SourceMetadataReads].ShouldBe(0);
        cost[ProjectionDeliveryCost.SourceEventReads].ShouldBe(0);
        ((bool)typeof(AggregateActor).GetField(recoveryField,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(actor)!).ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task SourceInspection_EnforcesItsIndependentMetadataBodyCapAndChargesRejectedBytes(int over)
    {
        (AggregateActor actor, IActorStateManager state) = CreateActor();
        var identity = new Hexalith.EventStore.Contracts.Identity.AggregateIdentity("tenant-a", "counter", "counter-1");
        SourceProof proof = SourceProofCodec.Extend(SourceProofCodec.CreateSeed(identity,
            "01ARZ3NDEKTSV4RRFFQ69G5FAV"), CreateEvent(1));
        var metadata = new AggregateMetadata(1, DateTimeOffset.UnixEpoch, string.Empty) { SourceProof = proof };
        int baseline = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(metadata,
            System.Text.Json.JsonSerializerOptions.Web).Length;
        metadata = metadata with { ETag = new string('x', 4096 - baseline + over) };
        state.TryGetStateAsync<AggregateMetadata>(MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));

        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        SourceProofInspection observed = await actor.InspectSourceProofAsync();

        observed.Status.ShouldBe(over == 0 ? SourceProofInspectionStatus.Available : SourceProofInspectionStatus.Invalid);
        observed.Proof.ShouldBe(over == 0 ? proof : null);
        cost[ProjectionDeliveryCost.SourceMetadataReads].ShouldBe(1);
        cost[ProjectionDeliveryCost.SourceMetadataBytes].ShouldBe(4096 + over);
        cost[ProjectionDeliveryCost.MetadataSizingBytes].ShouldBe(4096 + over);
        cost[ProjectionDeliveryCost.SourceEventReads].ShouldBe(0);
    }

    [Fact]
    public async Task BoundedRange_OversizedFirstEnvelopeChargesItsFullReadDecodeAndSizingBeforeRejection()
    {
        (AggregateActor actor, IActorStateManager state) = CreateActor();
        ConfigureMetadata(state, 1);
        EventEnvelope envelope = CreateEvent(1) with { Payload = new byte[1_600_000] };
        int actualBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(envelope,
            System.Text.Json.JsonSerializerOptions.Web).Length;
        actualBytes.ShouldBeGreaterThan(2_097_150);
        state.TryGetStateAsync<EventEnvelope>(EventKeyPrefix + "1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, envelope));

        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await actor.ReadMeasuredEventsBoundedRangeAsync(0, 1, 256, 2_097_152)).ReasonCode.ShouldBe("delivery_page_limit_exceeded");

        cost[ProjectionDeliveryCost.SourceEventReads].ShouldBe(1);
        cost[ProjectionDeliveryCost.SourceDeserializations].ShouldBe(1);
        cost[ProjectionDeliveryCost.SourceDeserializationBytes].ShouldBe(actualBytes);
        cost[ProjectionDeliveryCost.SourceSizingBytes].ShouldBe(actualBytes);
        cost[ProjectionDeliveryCost.SourceReturnedEvents].ShouldBe(0);
        cost[ProjectionDeliveryCost.SourceResponses].ShouldBe(1);
        cost[ProjectionDeliveryCost.SourceEventHashes].ShouldBe(0);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public async Task SourceBootstrap_ErasedOrErasingSourceNeverCreatesANewIncarnation(bool completed, int head)
    {
        var state = new FaultInjectingActorStateManager();
        var seed = new Dictionary<string, object>
        { [TrustedEffectErasureProgress.StateName] = new TrustedEffectErasureProgress(3, 1, completed, "nonce") };
        if (head > 0)
        {
            seed[MetadataKey] = new AggregateMetadata(head, DateTimeOffset.UnixEpoch, "legacy-head");
            for (int index = 1; index <= head; index++) seed[EventKeyPrefix + index] = CreateEvent(index);
        }
        await state.SeedCommittedStateAsync(seed);
        (SourceProofBootstrapAuthority authority, IServiceProvider services) = BootstrapAuthority();
        (AggregateActor actor, _) = CreateActor(state, services);
        SourceProofBootstrapRequest request = await authority.AuthorizeAsync(BootstrapRequest(head), CancellationToken.None);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        SourceProofInspection result = await actor.BootstrapSourceProofAsync(request);
        result.Proof.ShouldBeNull();
        result.Erased.ShouldBeTrue();
        result.CurrentSequence.ShouldBe(head);
        state.CommittedState.ContainsKey(MetadataKey).ShouldBe(head > 0);
        if (head > 0) ((AggregateMetadata)state.CommittedState[MetadataKey]).SourceProof.ShouldBeNull();
        cost[ProjectionDeliveryCost.SourceMetadataReads].ShouldBe(2);
        cost[ProjectionDeliveryCost.SourceMetadataBytes].ShouldBeGreaterThan(0);
        cost[ProjectionDeliveryCost.SourceEventReads].ShouldBe(0);
        cost[ProjectionDeliveryCost.SourceAnchorWriteAttempts].ShouldBe(0);
        state.Trace.ShouldNotContain("SaveState");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceBootstrap_EmptyNewOrTouchedSourceRetainsOneSeedWithoutInventingEnvelopes(bool touched)
    {
        var state = new FaultInjectingActorStateManager();
        if (touched) await state.SeedCommittedStateAsync(new Dictionary<string, object>
        { [MetadataKey] = new AggregateMetadata(0, DateTimeOffset.UnixEpoch, "preserved-etag") });
        (SourceProofBootstrapAuthority authority, IServiceProvider services) = BootstrapAuthority();
        (AggregateActor actor, _) = CreateActor(state, services);
        SourceProofBootstrapRequest request = await authority.AuthorizeAsync(BootstrapRequest(0), CancellationToken.None);
        SourceProofInspection inspected = await actor.BootstrapSourceProofAsync(request);
        inspected.CurrentSequence.ShouldBe(0);
        inspected.Proof.ShouldNotBeNull().HeadMessageId.ShouldBeNull();
        inspected.Proof.ShouldBe(SourceProofCodec.CreateSeed(new("tenant-a", "counter", "counter-1"), inspected.Proof.SourceIncarnation));
        state.CommittedState.Keys.ShouldBe([MetadataKey]);
        state.Trace.Count(static item => item == "SaveState").ShouldBe(1);
        (await actor.BootstrapSourceProofAsync(request)).Proof.ShouldBe(inspected.Proof);
        state.Trace.Count(static item => item == "SaveState").ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceBootstrap_PersistsSameCanonicalAnchorAndReadsBackAmbiguousSave(bool loseSaveResponse)
    {
        var state = new FaultInjectingActorStateManager();
        await state.SeedCommittedStateAsync(new Dictionary<string, object>
        {
            [MetadataKey] = new AggregateMetadata(2, DateTimeOffset.UnixEpoch, "head-etag"),
            [EventKeyPrefix + "1"] = CreateEvent(1), [EventKeyPrefix + "2"] = CreateEvent(2),
        });
        if (loseSaveResponse) state.FaultAfterCall("SaveState", 1, new IOException("lost save response"));
        (SourceProofBootstrapAuthority authority, IServiceProvider services) = BootstrapAuthority();
        (AggregateActor actor, _) = CreateActor(state, services);
        SourceProofBootstrapRequest request = await authority.AuthorizeAsync(BootstrapRequest(2), CancellationToken.None);

        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        SourceProofInspection result = await actor.BootstrapSourceProofAsync(request);

        cost[ProjectionDeliveryCost.SourceBootstrapAttempts].ShouldBe(1);
        cost[ProjectionDeliveryCost.BootstrapStateReads].ShouldBe(6); // metadata, erasure progress, two events, pre-save metadata, readback
        cost[ProjectionDeliveryCost.SourceMetadataReads].ShouldBe(4);
        cost[ProjectionDeliveryCost.SourceEventReads].ShouldBe(2);
        cost[ProjectionDeliveryCost.SourceDeserializations].ShouldBe(2);
        cost[ProjectionDeliveryCost.SourceDeserializationBytes].ShouldBeGreaterThan(0);
        cost[ProjectionDeliveryCost.SourceAnchorWriteAttempts].ShouldBe(1);
        cost[ProjectionDeliveryCost.SourceAnchorWriteBytes].ShouldBeGreaterThan(0);
        cost[ProjectionDeliveryCost.BootstrapSaveAttempts].ShouldBe(1);
        cost[ProjectionDeliveryCost.SourceAnchorReadbacks].ShouldBe(1);
        cost[ProjectionDeliveryCost.WireReadabilityAttempts].ShouldBe(2);
        cost[ProjectionDeliveryCost.WireSizingBytes].ShouldBeGreaterThan(0);
        result.Status.ShouldBe(SourceProofInspectionStatus.Available);
        SourceProof actual = result.Proof.ShouldNotBeNull();
        var identity = new Hexalith.EventStore.Contracts.Identity.AggregateIdentity("tenant-a", "counter", "counter-1");
        SourceProof expected = SourceProofCodec.CreateSeed(identity, actual.SourceIncarnation);
        expected = SourceProofCodec.Extend(expected, (EventEnvelope)state.CommittedState[EventKeyPrefix + "1"]);
        expected = SourceProofCodec.Extend(expected, (EventEnvelope)state.CommittedState[EventKeyPrefix + "2"]);
        actual.ShouldBe(expected);
        ((AggregateMetadata)state.CommittedState[MetadataKey]).ETag.ShouldBe("head-etag");
        (await actor.InspectSourceProofAsync()).Proof.ShouldBe(actual);
        state.Trace.Count(static item => item == "SaveState").ShouldBe(1);
        state.Trace.Any(item => item.StartsWith("SetState:" + EventKeyPrefix, StringComparison.Ordinal)).ShouldBeFalse();
        (await actor.BootstrapSourceProofAsync(request)).Proof.ShouldBe(actual);
        state.Trace.Count(static item => item == "SaveState").ShouldBe(1);
    }

    [Fact]
    public async Task SourceBootstrap_ChangedAuthorizationRefusesBeforeAnyActorStateAccess()
    {
        var state = new FaultInjectingActorStateManager();
        (SourceProofBootstrapAuthority authority, IServiceProvider services) = BootstrapAuthority();
        (AggregateActor actor, _) = CreateActor(state, services);
        SourceProofBootstrapRequest request = await authority.AuthorizeAsync(BootstrapRequest(2), CancellationToken.None);
        await Should.ThrowAsync<InvalidOperationException>(() => actor.BootstrapSourceProofAsync(request with { ExpectedHead = 3 }));
        state.Trace.ShouldBeEmpty();
    }

    private static SourceProofBootstrapRequest BootstrapRequest(long head) => new("tenant-a", "counter", "counter-1", head,
        "operator-1", new string('a', 40), "verified-backup-1", "78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711",
        true, true, default, string.Empty, string.Empty);

    private static (SourceProofBootstrapAuthority Authority, IServiceProvider Services) BootstrapAuthority()
    {
        IIdempotencyDigestKeyProvider keys = Substitute.For<IIdempotencyDigestKeyProvider>();
        keys.GetKeyRingAsync(Arg.Any<CancellationToken>()).Returns(_ => new ValueTask<IdempotencyDigestKeyRing>(
            new IdempotencyDigestKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = new byte[32] }, [])));
        var authority = new SourceProofBootstrapAuthority(keys, TimeProvider.System);
        IServiceProvider services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(SourceProofBootstrapAuthority)).Returns(authority);
        return (authority, services);
    }
    [Theory]
    [InlineData(8190, 256, 0)]
    [InlineData(8191, 255, 1)]
    [InlineData(8192, 255, 1)]
    public async Task BoundedRange_ChargesArrayFramingAndAtMostOneLookahead(int eventBytes, int firstCount, int lookahead)
    {
        (AggregateActor actor, IActorStateManager state) = CreateActor();
        ConfigureMetadata(state, 256);
        for (int sequence = 1; sequence <= 256; sequence++)
        {
            EventEnvelope envelope = CreateEvent(sequence) with { UserId = "" };
            int length = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(envelope, System.Text.Json.JsonSerializerOptions.Web).Length;
            envelope = envelope with { UserId = new string(' ', eventBytes - length) };
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(envelope, System.Text.Json.JsonSerializerOptions.Web).Length.ShouldBe(eventBytes);
            state.TryGetStateAsync<EventEnvelope>($"{EventKeyPrefix}{sequence}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, envelope));
        }

        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        EventEnvelope[] first = await actor.ReadEventsBoundedRangeAsync(0, 256, 256, 2_097_152);
        first.Length.ShouldBe(firstCount);
        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(first, System.Text.Json.JsonSerializerOptions.Web).Length
            .ShouldBeLessThanOrEqualTo(2_097_152);
        state.ReceivedCalls().Count(call => call.GetMethodInfo().IsGenericMethod
            && call.GetMethodInfo().GetGenericArguments().Contains(typeof(EventEnvelope))).ShouldBe(firstCount + lookahead);
        cost[ProjectionDeliveryCost.SourceEventReads].ShouldBe(firstCount + lookahead);
        cost[ProjectionDeliveryCost.SourceDeserializations].ShouldBe(firstCount + lookahead);
        cost[ProjectionDeliveryCost.SourceSizingBytes].ShouldBe((long)eventBytes * (firstCount + lookahead));
        cost[ProjectionDeliveryCost.SourceLookaheadEvents].ShouldBe(lookahead);
        cost[ProjectionDeliveryCost.SourceLookaheadBytes].ShouldBe((long)lookahead * eventBytes);
        cost[ProjectionDeliveryCost.SourceEventHashes].ShouldBe(0);
        cost[ProjectionDeliveryCost.SourceChainExtensions].ShouldBe(0);
        cost[ProjectionDeliveryCost.SourceResponseBytes].ShouldBe(2L + ((long)eventBytes * firstCount) + firstCount - 1);
        if (firstCount < 256)
        {
            EventEnvelope[] last = await actor.ReadEventsBoundedRangeAsync(firstCount, 256, 256, 2_097_152);
            last.ShouldHaveSingleItem().SequenceNumber.ShouldBe(256);
            state.ReceivedCalls().Count(call => call.GetMethodInfo().IsGenericMethod
                && call.GetMethodInfo().GetGenericArguments().Contains(typeof(EventEnvelope))).ShouldBe(257);
        }
    }

    [Fact]
    public async Task SourceInspection_UsesOnlyMetadataAndNeverInfersLegacyProof()
    {
        (AggregateActor actor, IActorStateManager state) = CreateActor();
        ConfigureMetadata(state, 9);
        SourceProofInspection result = await actor.InspectSourceProofAsync();
        result.Status.ShouldBe(SourceProofInspectionStatus.Unsupported);
        result.CurrentSequence.ShouldBe(9);
        result.Proof.ShouldBeNull();
        state.ReceivedCalls().Any(call => call.GetMethodInfo().IsGenericMethod
            && call.GetMethodInfo().GetGenericArguments().Contains(typeof(EventEnvelope))).ShouldBeFalse();
    }

    private const string ActorIdString = "tenant-a:counter:counter-1";
    private const string MetadataKey = "tenant-a:counter:counter-1:metadata";
    private const string EventKeyPrefix = "tenant-a:counter:counter-1:events:";

    private static (AggregateActor Actor, IActorStateManager StateManager) CreateActor(
        IActorStateManager? stateManager = null, IServiceProvider? serviceProvider = null) {
        stateManager ??= Substitute.For<IActorStateManager>();
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        ISnapshotManager snapshotManager = Substitute.For<ISnapshotManager>();
        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        IEventPublisher eventPublisher = Substitute.For<IEventPublisher>();
        IDeadLetterPublisher deadLetterPublisher = Substitute.For<IDeadLetterPublisher>();

        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId(ActorIdString) });

        var actor = new AggregateActor(
            host, logger, invoker, snapshotManager,
            new NoOpEventPayloadProtectionService(), commandStatusStore, eventPublisher,
            Options.Create(new EventDrainOptions()),
            Options.Create(new BackpressureOptions()),
            deadLetterPublisher, serviceProvider: serviceProvider);

        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);

        return (actor, stateManager);
    }

    private static EventEnvelope CreateEvent(int seq) => new(
        MessageId: Guid.NewGuid().ToString(),
        AggregateId: "counter-1",
        AggregateType: "counter",
        TenantId: "tenant-a",
        Domain: "counter",
        SequenceNumber: seq,
        GlobalPosition: 0,
        Timestamp: DateTimeOffset.UtcNow,
        CorrelationId: $"corr-{seq}",
        CausationId: $"cause-{seq}",
        UserId: "user-1",
        DomainServiceVersion: "1.0.0",
        EventTypeName: "CounterIncremented",
        MetadataVersion: 1,
        SerializationFormat: "json",
        Payload: [1, 2, 3],
        Extensions: null);

    private static void ConfigureMetadata(IActorStateManager stateManager, long currentSequence) {
        var metadata = new AggregateMetadata(currentSequence, DateTimeOffset.UtcNow, null);
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));
    }

    private static void ConfigureEvents(IActorStateManager stateManager, int count) {
        for (int i = 1; i <= count; i++) {
            int seq = i;
            EventEnvelope evt = CreateEvent(seq);
            _ = stateManager.TryGetStateAsync<EventEnvelope>($"{EventKeyPrefix}{seq}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, evt));
        }
    }

    [Fact]
    public async Task GetEventsAsync_MetadataReadCancellation_PropagatesWithoutDeserializationWrapper() {
        var stateManager = new FaultInjectingActorStateManager();
        stateManager.FaultOnCall(
            $"TryGetState:{MetadataKey}",
            1,
            new OperationCanceledException("metadata canceled"));
        (AggregateActor actor, _) = CreateActor(stateManager);

        _ = await Should.ThrowAsync<OperationCanceledException>(
            () => actor.GetEventsAsync(0));
    }

    [Fact]
    public async Task GetEventsAsync_MetadataTransportFailure_PropagatesWithoutDeserializationWrapper() {
        var stateManager = new FaultInjectingActorStateManager();
        var expected = new IOException("metadata unavailable");
        stateManager.FaultOnCall($"TryGetState:{MetadataKey}", 1, expected);
        (AggregateActor actor, _) = CreateActor(stateManager);

        IOException observed = await Should.ThrowAsync<IOException>(
            () => actor.GetEventsAsync(0));

        observed.ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task GetEventsAsync_NewAggregate_ReturnsEmptyArray() {
        (AggregateActor actor, IActorStateManager stateManager) = CreateActor();
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));

        EventEnvelope[] result = await actor.GetEventsAsync(0);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetEventsAsync_WithEvents_ReturnsEventsAfterSequence() {
        (AggregateActor actor, IActorStateManager stateManager) = CreateActor();
        ConfigureMetadata(stateManager, 3);
        ConfigureEvents(stateManager, 3);

        EventEnvelope[] result = await actor.GetEventsAsync(1);

        result.Length.ShouldBe(2);
        result[0].SequenceNumber.ShouldBe(2);
        result[1].SequenceNumber.ShouldBe(3);
    }

    [Fact]
    public async Task GetEventsAsync_FromSequenceAtCurrent_ReturnsEmpty() {
        (AggregateActor actor, IActorStateManager stateManager) = CreateActor();
        ConfigureMetadata(stateManager, 3);

        EventEnvelope[] result = await actor.GetEventsAsync(3);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetEventsAsync_FromSequenceBeyondCurrent_ReturnsEmpty() {
        (AggregateActor actor, IActorStateManager stateManager) = CreateActor();
        ConfigureMetadata(stateManager, 3);

        EventEnvelope[] result = await actor.GetEventsAsync(100);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetEventsAsync_ExactlyMaxBatchSize_ReturnsAllEvents() {
        (AggregateActor actor, IActorStateManager stateManager) = CreateActor();
        ConfigureMetadata(stateManager, 32);
        ConfigureEvents(stateManager, 32);

        EventEnvelope[] result = await actor.GetEventsAsync(0);

        result.Length.ShouldBe(32);
        for (int i = 0; i < 32; i++) {
            result[i].SequenceNumber.ShouldBe(i + 1);
        }
    }

    [Fact]
    public async Task GetEventsAsync_MoreThanMaxBatchSize_ReturnsAllEvents() {
        (AggregateActor actor, IActorStateManager stateManager) = CreateActor();
        ConfigureMetadata(stateManager, 33);
        ConfigureEvents(stateManager, 33);

        EventEnvelope[] result = await actor.GetEventsAsync(0);

        result.Length.ShouldBe(33);
        for (int i = 0; i < 33; i++) {
            result[i].SequenceNumber.ShouldBe(i + 1);
        }
    }

    [Fact]
    public async Task GetEventsAsync_MissingEventKey_ThrowsMissingEventException() {
        (AggregateActor actor, IActorStateManager stateManager) = CreateActor();
        ConfigureMetadata(stateManager, 3);

        // Configure event 1 and 3 but NOT event 2
        EventEnvelope evt1 = CreateEvent(1);
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{EventKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, evt1));
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{EventKeyPrefix}2", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(false, default!));
        EventEnvelope evt3 = CreateEvent(3);
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{EventKeyPrefix}3", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, evt3));

        MissingEventException ex = await Should.ThrowAsync<MissingEventException>(
            () => actor.GetEventsAsync(0));

        ex.SequenceNumber.ShouldBe(2);
    }

    [Fact]
    public async Task GetEventsAsync_NegativeFromSequence_ClampsToZero() {
        (AggregateActor actor, IActorStateManager stateManager) = CreateActor();
        ConfigureMetadata(stateManager, 3);
        ConfigureEvents(stateManager, 3);

        EventEnvelope[] result = await actor.GetEventsAsync(-1);

        result.Length.ShouldBe(3);
        result[0].SequenceNumber.ShouldBe(1);
        result[1].SequenceNumber.ShouldBe(2);
        result[2].SequenceNumber.ShouldBe(3);
    }
}
