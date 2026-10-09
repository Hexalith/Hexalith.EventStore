using System.Security.Cryptography;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using EventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Events;

public sealed class DaprProductionLogicalEventReaderTests
{
    private static readonly AggregateIdentity Identity = new("tenant", "d", "aggregate");

    /// <summary>Verifies a mismatched caller pin is refused before any actor read.</summary>
    [Fact]
    public void WrongCallerPinFailsBeforeActorRead()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        string pin = new('0', 64);
        if (string.Equals(pin, registry.Fingerprint, StringComparison.Ordinal))
        {
            pin = new('1', 64);
        }

        InvalidOperationException error = Should.Throw<InvalidOperationException>(() =>
            CreateReader(registry, stateManager, pin));

        error.Message.ShouldContain("CapabilityMismatch");
        stateManager.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Verifies range admission observes shared loss while preserving the original pre-cancellation token.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RangeRefusesSharedLossBeforeActorRead(bool cancel)
    {
        using EventDomainRegistry registry = CreateRegistry(capabilityLoss: new EventEvolutionCapabilityLoss());
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);
        var budget = new EventBufferBudget();
        using var cancellation = new CancellationTokenSource();
        if (cancel) { cancellation.Cancel(); }
        registry.CapabilityLoss.ObserveViolation();

        if (cancel)
        {
            OperationCanceledException error = await Should.ThrowAsync<OperationCanceledException>(() => reader.ReadRangeAsync(
                Identity, "r", 1, 1, cancellation.Token, 1, 1, true, arrayBudget: null, budget)).ConfigureAwait(true);
            error.CancellationToken.ShouldBe(cancellation.Token);
        }
        else
        {
            InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() => reader.ReadRangeAsync(
                Identity, "r", 1, 1, cancellation.Token, 1, 1, true, arrayBudget: null, budget)).ConfigureAwait(true);
            error.Message.ShouldContain("CapabilityMismatch");
        }

        stateManager.ReceivedCalls().ShouldBeEmpty();
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Verifies zero-hop V1 reads retain the original actor payload and message identity.</summary>
    [Fact]
    public async Task ZeroHopV1ReplayKeepsStoredBytesAndMessageId()
    {
        using EventDomainRegistry registry = CreateRegistry();
        (IActorStateManager stateManager, EventEnvelope stored) = StoreCurrentV1();
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);

        using DaprLogicalEventPage page = await reader.ReadPageAsync(
            Identity, "r", 1, 1, CancellationToken.None).ConfigureAwait(true);

        page.Events.ShouldHaveSingleItem().MessageId.ShouldBe("01J00000000000000000000001");
        page.Events[0].Source.Payload.ShouldBe(stored.Payload);
        stored.Payload.ShouldBe([1, 2]);
        DaprProductionLogicalEventReader.IsZeroHopV1(page.Events[0]).ShouldBeTrue();
        _ = stateManager.DidNotReceiveWithAnyArgs().SetStateAsync(default!, default(EventEnvelope)!, default);
        _ = stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies historical V1 history without an additive digest remains readable.</summary>
    [Fact]
    public async Task HistoricalV1WithoutDigestStaysReadable()
    {
        using EventDomainRegistry registry = CreateRegistry();
        (IActorStateManager stateManager, EventEnvelope stored) = StoreCurrentV1(includeDigest: false);
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);
        var replay = new EventStreamReader(stateManager, Substitute.For<ILogger<EventStreamReader>>());

        RehydrationResult? result = await replay.RehydrateAsync(
            Identity, snapshot: null, CancellationToken.None, reader, "r").ConfigureAwait(true);

        _ = result.ShouldNotBeNull();
        result.EffectiveEvents.ShouldBeNull();
        result.Events.ShouldHaveSingleItem().Payload.ShouldBe(stored.Payload);
        result.Events[0].MessageId.ShouldBe(stored.MessageId);
        stored.Payload.ShouldBe([1, 2]);
    }

    /// <summary>Verifies digest mismatches refuse replay and reconstruction before domain resolution.</summary>
    [Fact]
    public async Task DigestMismatchFailsClosedBeforeDomainResolution()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        EventEnvelope stored = CreateEvent();
        stored = stored with
        {
            ApplicationPayloadDigest = EventLogicalDigest.Compute(
                stored, "json", EventLogicalDigest.HashPayload("other"u8)),
        };
        Store(stateManager, stored);
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);
        var reconstructor = CreateReconstructor(out IDomainServiceResolver resolver);

        InvalidOperationException direct = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadPageAsync(Identity, "r", 1, 1, CancellationToken.None)).ConfigureAwait(true);
        direct.Message.ShouldContain("LogicalDigestMismatch");
        var stream = new EventStreamReader(stateManager, Substitute.For<ILogger<EventStreamReader>>());
        InvalidOperationException replayError = await Should.ThrowAsync<InvalidOperationException>(() =>
            stream.RehydrateAsync(Identity, null, CancellationToken.None, reader, "r")).ConfigureAwait(true);
        replayError.Message.ShouldContain("LogicalDigestMismatch");

        AggregateReconstructionResult result = await reconstructor.ReconstructAddressedAsync(
            Identity, "r", reader, CreateSnapshotManager(), upToSequence: 1).ConfigureAwait(true);

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.Unexpected);
        result.Message.ShouldContain("LogicalDigestMismatch");
        result.StateJson.ShouldBeNull();
        result.LastAppliedSequenceNumber.ShouldBe(0);
        stored.Payload.ShouldBe([1, 2]);
        _ = await resolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default!, default!, default).ConfigureAwait(true);
        _ = stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies unknown aliases and versioned sources are refused before protection work.</summary>
    [Theory]
    [InlineData("missing-alias", 1, null, null, "UnknownEventContract")]
    [InlineData("evt", 2, "evt", 2, "RollbackReaderCapabilityHold")]
    public async Task MissingMappingOrVersion2FailsBeforeProtection(
        string eventType, int metadataVersion, string? contractType, int? payloadVersion, string reason)
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        EventEnvelope stored = CreateEvent() with
        {
            EventTypeName = eventType,
            MetadataVersion = metadataVersion,
            EventContractType = contractType,
            PayloadVersion = payloadVersion,
        };
        Store(stateManager, stored);
        var reader = CreateReader(registry, stateManager, registry.Fingerprint, protection: protection);

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadPageAsync(Identity, "r", 1, 1, CancellationToken.None)).ConfigureAwait(true);

        error.Message.ShouldContain(reason);
        stored.Payload.ShouldBe([1, 2]);
        _ = protection.DidNotReceiveWithAnyArgs().TryUnprotectEventPayloadAsync(
            default!, default!, default!, default!, default!, default);
    }

    /// <summary>Verifies an unbound required hop returns no partial replay or actor mutation.</summary>
    [Fact]
    public async Task BrokenChainFailsWithoutPartialReplay()
    {
        using EventDomainRegistry registry = CreateRegistry(upcasting: true);
        (IActorStateManager stateManager, EventEnvelope stored) = StoreCurrentV1(head: 2, count: 2);
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadRangeAsync(Identity, "r", 1, 2, CancellationToken.None, expectedActorHead: 2, expectedRetainedFloor: 1))
            .ConfigureAwait(true);

        error.Message.ShouldContain("CapabilityMismatch");
        stored.Payload.ShouldBe([1, 2]);
        _ = stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies a retained-floor violation is refused before reading events.</summary>
    [Fact]
    public async Task IncompletePrefixFailsBeforeEventRead()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(3, DateTimeOffset.UnixEpoch, "etag", 3)));
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadPageAsync(Identity, "r", 1, 1, CancellationToken.None, expectedActorHead: 3, expectedRetainedFloor: 3))
            .ConfigureAwait(true);

        error.Message.ShouldContain("ReplayRestartRequired");
        _ = stateManager.DidNotReceiveWithAnyArgs().TryGetStateAsync<EventEnvelope>(default!, default);
    }

    /// <summary>Verifies actor-read cancellation escapes without publishing partial replay.</summary>
    [Fact]
    public async Task CancellationDuringActorReadPublishesNoPartialResult()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        using var cancellation = new CancellationTokenSource();
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(2, DateTimeOffset.UnixEpoch, "etag")));
        _ = stateManager.TryGetStateAsync<EventEnvelope>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellation.Cancel();
                return new ConditionalValue<EventEnvelope>(true, CreateEvent());
            });
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);
        var replay = new EventStreamReader(stateManager, Substitute.For<ILogger<EventStreamReader>>());

        _ = await Should.ThrowAsync<OperationCanceledException>(() =>
            replay.RehydrateAsync(Identity, snapshot: null, cancellation.Token, reader, "r")).ConfigureAwait(true);

        _ = stateManager.DidNotReceiveWithAnyArgs().SetStateAsync(default!, default(EventEnvelope)!, default);
        _ = stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies evolved payloads cannot enter either legacy domain route.</summary>
    [Fact]
    public async Task EvolvedReplayAndReconstructionRefuseWithoutAnEffectiveRoute()
    {
        using EventDomainRegistry registry = CreateRegistry(upcasting: true);
        (IActorStateManager stateManager, EventEnvelope stored) = StoreCurrentV1();
        var upcaster = new ShrinkingLogicalEventUpcaster();
        var reader = CreateReader(registry, stateManager, registry.Fingerprint, upcaster);
        var replay = new EventStreamReader(stateManager, Substitute.For<ILogger<EventStreamReader>>());
        SnapshotManager snapshots = CreateSnapshotManager();

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() => replay.RehydrateAsync(
            Identity, snapshot: null, CancellationToken.None, reader, "r", snapshots)).ConfigureAwait(true);
        error.Message.ShouldContain("CapabilityMismatch");
        var reconstructor = CreateReconstructor(out IDomainServiceResolver resolver);
        AggregateReconstructionResult result = await reconstructor.ReconstructAddressedAsync(
            Identity, "r", reader, snapshots, 1).ConfigureAwait(true);
        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnsupportedVersion);
        result.Message.ShouldContain("CapabilityMismatch");
        result.StateJson.ShouldBeNull();
        stored.Payload.ShouldBe([1, 2]);
        upcaster.Calls.ShouldBe(2);
        resolver.ReceivedCalls().ShouldBeEmpty();
        _ = stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies unsupported addressed history reaches neither domain resolution nor partial state.</summary>
    [Fact]
    public async Task AddressedReconstructionRejectsUnsupportedHistoryWithoutPartialState()
    {
        using EventDomainRegistry registry = CreateRegistry(upcasting: true);
        (IActorStateManager stateManager, EventEnvelope stored) = StoreCurrentV1();
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);
        var reconstructor = CreateReconstructor(out IDomainServiceResolver resolver);

        AggregateReconstructionResult result = await reconstructor.ReconstructAddressedAsync(
            Identity, "r", reader, CreateSnapshotManager(), upToSequence: 1).ConfigureAwait(true);

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnsupportedVersion);
        result.Message.ShouldContain("CapabilityMismatch");
        result.StateJson.ShouldBeNull();
        result.LastAppliedSequenceNumber.ShouldBe(0);
        stored.Payload.ShouldBe([1, 2]);
        _ = await resolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default!, default!, default).ConfigureAwait(true);
    }

    /// <summary>Verifies addressed plaintext reconstruction matches the canonical Apply replay.</summary>
    [Fact]
    public async Task AddressedReconstructionOfCurrentV1ReachesCanonicalReplay()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        EventEnvelope[] stored = StoreCanonicalHistory(stateManager);
        IEventPayloadProtectionService protection = CreateCanonicalProtection();
        var reader = CreateReader(registry, stateManager, registry.Fingerprint, protection: protection);
        using var handler = new CanonicalLogicalReplayHandler();
        using var httpClient = new HttpClient(handler);
        using var dapr = new Dapr.Client.DaprClientBuilder().Build();
        var reconstructor = CreateCanonicalReconstructor(dapr, httpClient, out IDomainServiceResolver resolver);

        AggregateReconstructionResult result = await reconstructor.ReconstructAddressedAsync(
            Identity, "r", reader, CreateSnapshotManager(), upToSequence: 3).ConfigureAwait(true);

        result.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.None);
        result.LastAppliedSequenceNumber.ShouldBe(3);
        using JsonDocument state = JsonDocument.Parse(result.StateJson!);
        state.RootElement.GetProperty("count").GetInt32().ShouldBe(6);
        AggregateReconstructionRequest request = handler.Requests.ShouldHaveSingleItem();
        request.Events.Select(e => e.SequenceNumber).ShouldBe([1L, 2L, 3L]);
        request.Events[0].Payload.ShouldBe(JsonSerializer.SerializeToUtf8Bytes(new Legacy.Event(1)));
        stored.All(e => e.Payload.SequenceEqual(new byte[] { 9, (byte)e.SequenceNumber })).ShouldBeTrue();
        result.ShouldBe(Hexalith.EventStore.Client.Aggregates.AggregateReplayer.Replay<LogicalReplayState>(request));
        _ = await resolver.Received(1).ResolveAsync(Identity.TenantId, Identity.Domain, "v1", Arg.Any<CancellationToken>())
            .ConfigureAwait(true);
    }

    /// <summary>Verifies declared payload versions cannot bypass the production V1 source fence.</summary>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task DeclaredVersionedHistoryStillRejectsAtReplayAndReconstruction(bool upcasting, int payloadVersion)
    {
        using EventDomainRegistry registry = CreateRegistry(upcasting);
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        EventEnvelope stored = CreateEvent() with
        {
            MetadataVersion = 2,
            EventTypeName = "evt",
            EventContractType = "evt",
            PayloadVersion = payloadVersion,
        };
        Store(stateManager, stored);
        var reader = CreateReader(registry, stateManager, registry.Fingerprint,
            upcasting ? new ShrinkingLogicalEventUpcaster() : null);
        var stream = new EventStreamReader(stateManager, Substitute.For<ILogger<EventStreamReader>>());
        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            stream.RehydrateAsync(Identity, null, CancellationToken.None, reader, "r")).ConfigureAwait(true);
        error.Message.ShouldContain("RollbackReaderCapabilityHold");
        var reconstructor = CreateReconstructor(out IDomainServiceResolver resolver);
        AggregateReconstructionResult result = await reconstructor.ReconstructAddressedAsync(
            Identity, "r", reader, CreateSnapshotManager(), 1).ConfigureAwait(true);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnsupportedVersion);
        result.Message.ShouldContain("RollbackReaderCapabilityHold");
        result.LastAppliedSequenceNumber.ShouldBe(0);
        result.StateJson.ShouldBeNull();
        resolver.ReceivedCalls().ShouldBeEmpty();
        stored.MetadataVersion.ShouldBe(2);
        stored.Payload.ShouldBe([1, 2]);
    }

    /// <summary>Verifies complete page boundaries and refusal when the pinned retained floor changes.</summary>
    [Fact]
    public async Task MultiPageRangeKeepsContiguousSequencesAndPinsRetainedFloor()
    {
        using EventDomainRegistry registry = CreateRegistry();
        (IActorStateManager stateManager, _) = StoreCurrentV1(head: 258, count: 258);
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);
        using DaprProductionLogicalReplay range = await reader.ReadRangeAsync(
            Identity, "r", 1, 258, CancellationToken.None, 258, expectedRetainedFloor: 1,
            includeDomainView: false).ConfigureAwait(true);
        range.ActorHead.ShouldBe(258);
        range.RetainedFloor.ShouldBe(1);
        range.StoredEvents.Count.ShouldBe(258);
        range.StoredEvents.Select(e => e.SequenceNumber).ShouldBe(Enumerable.Range(1, 258).Select(i => (long)i));
        range.DomainEvents.ShouldBeEmpty();
        int metadataReads = stateManager.ReceivedCalls().Count(call => call.GetMethodInfo().Name == "TryGetStateAsync"
            && call.GetMethodInfo().GetGenericArguments().Single() == typeof(AggregateMetadata));
        // Two pages retain three observations each; every event's no-op validation
        // now has separate bounded source checks before and after its invocation.
        metadataReads.ShouldBeInRange(6 + 2 * range.StoredEvents.Count, 6 + 4 * range.StoredEvents.Count);

        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(258, DateTimeOffset.UnixEpoch, "etag")),
                new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(258, DateTimeOffset.UnixEpoch, "etag")),
                new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(258, DateTimeOffset.UnixEpoch, "etag", 2)));
        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadRangeAsync(Identity, "r", 1, 258, CancellationToken.None, 258, includeDomainView: false))
            .ConfigureAwait(true);
        error.Message.ShouldContain("SourceHeadChanged");
    }

    /// <summary>Verifies production snapshot-tail replay preserves the typed-reader result.</summary>
    [Fact]
    public async Task SnapshotTailReplayMatchesTheTypedReader()
    {
        using EventDomainRegistry registry = CreateRegistry();
        (IActorStateManager stateManager, _) = StoreCurrentV1(head: 3, count: 3);
        var snapshot = new SnapshotRecord(1, new { Count = 1 }, DateTimeOffset.UnixEpoch, "d", "aggregate", "tenant");
        var stream = new EventStreamReader(stateManager, Substitute.For<ILogger<EventStreamReader>>());
        RehydrationResult typed = (await stream.RehydrateAsync(Identity, snapshot, CancellationToken.None)
            .ConfigureAwait(true)).ShouldNotBeNull();
        RehydrationResult logical = (await stream.RehydrateAsync(Identity, snapshot, CancellationToken.None,
            CreateReader(registry, stateManager, registry.Fingerprint), "r", CreateSnapshotManager())
            .ConfigureAwait(true)).ShouldNotBeNull();
        logical.Events.ShouldBe(typed.Events);
        logical.Events.Select(e => e.SequenceNumber).ShouldBe([2L, 3L]);
        logical.SnapshotState.ShouldBe(typed.SnapshotState);
        logical.LastSnapshotSequence.ShouldBe(1);
        logical.CurrentSequence.ShouldBe(3);
    }

    /// <summary>Verifies a binding-free production caller pin refuses a required upcast hop.</summary>
    [Fact]
    public async Task CallerPinWithRequiredHopFailsClosed()
    {
        using EventEvolutionManifestCandidate candidate = CreateUpcastingCandidate();
        (IActorStateManager stateManager, _) = StoreCurrentV1();
        var reader = DaprProductionLogicalEventReader.FromCallerPin(stateManager,
            new NoOpEventPayloadProtectionService(), candidate).ShouldNotBeNull();
        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadRangeAsync(Identity, "r", 1, 1, CancellationToken.None, 1)).ConfigureAwait(true);
        error.Message.ShouldContain("CapabilityMismatch");
    }

    /// <summary>Verifies mapping and prefix failures keep typed outcomes at replay and reconstruction entry points.</summary>
    [Theory]
    [InlineData("mapping", "UnknownEventContract", AggregateReconstructionErrorCategory.UnsupportedVersion)]
    [InlineData("floor", "ReplayRestartRequired", AggregateReconstructionErrorCategory.Unexpected)]
    [InlineData("missing", "The addressed logical prefix is incomplete.", AggregateReconstructionErrorCategory.Unexpected)]
    public async Task ReaderRefusalsReachBothEntryPointsWithNoPartialState(
        string scenario, string reason, AggregateReconstructionErrorCategory category)
    {
        using EventDomainRegistry registry = CreateRegistry();
        (IActorStateManager stateManager, _) = StoreCurrentV1(head: 2, count: 1);
        if (scenario == "mapping")
        {
            Store(stateManager, CreateEvent() with { EventTypeName = "missing" }, head: 2, count: 2);
        }
        if (scenario == "floor")
        {
            _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(2, DateTimeOffset.UnixEpoch, "etag", 2)));
        }
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);
        var stream = new EventStreamReader(stateManager, Substitute.For<ILogger<EventStreamReader>>());
        Exception error = await Should.ThrowAsync<InvalidOperationException>(() =>
            stream.RehydrateAsync(Identity, null, CancellationToken.None, reader, "r")).ConfigureAwait(true);
        if (scenario == "missing")
        {
            error.ShouldBeOfType<MissingEventException>().SequenceNumber.ShouldBe(2);
        }
        else
        {
            error.Message.ShouldContain(reason);
        }
        var reconstructor = CreateReconstructor(out IDomainServiceResolver resolver);
        AggregateReconstructionResult result = await reconstructor.ReconstructAddressedAsync(
            Identity, "r", reader, CreateSnapshotManager(), 2).ConfigureAwait(true);
        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(category);
        result.Message.ShouldContain(reason);
        result.LastAppliedSequenceNumber.ShouldBe(0);
        result.StateJson.ShouldBeNull();
        if (scenario == "missing")
        {
            result.FailedSequenceNumber.ShouldBe(2);
        }
        resolver.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Verifies addressed cancellation propagates the caller token before domain replay.</summary>
    [Fact]
    public async Task AddressedCancellationPreservesTheCallerTokenAndNeverInvokesDomainReplay()
    {
        using EventDomainRegistry registry = CreateRegistry();
        (IActorStateManager stateManager, _) = StoreCurrentV1(head: 2, count: 2);
        using var cancellation = new CancellationTokenSource();
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{Identity.EventStreamKeyPrefix}2", Arg.Any<CancellationToken>())
            .Returns(_ => { cancellation.Cancel(); return new ConditionalValue<EventEnvelope>(true, CreateEvent() with { SequenceNumber = 2 }); });
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);
        var reconstructor = CreateReconstructor(out IDomainServiceResolver resolver);
        _ = await Should.ThrowAsync<OperationCanceledException>(() => reconstructor.ReconstructAddressedAsync(
            Identity, "r", reader, CreateSnapshotManager(), 2, cancellationToken: cancellation.Token)).ConfigureAwait(true);
        _ = await stateManager.Received(1).TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}2", cancellation.Token).ConfigureAwait(true);
        resolver.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Verifies oversized ranges fail within their retained budget before later pages are read.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RangeBudgetRejectsBeforeReadingLaterPages(bool includeDomainView)
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        EventEnvelope stored = CreateEvent() with { Payload = new byte[160 * 1024] };
        Store(stateManager, stored, head: 768, count: 768);
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);
        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadRangeAsync(Identity, "r", 1, 768, CancellationToken.None, 768,
                includeDomainView: includeDomainView)).ConfigureAwait(true);
        error.Message.ShouldContain("LegacyArrayLimit");
        _ = await stateManager.DidNotReceive().TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}513", Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    /// <summary>Proves that domain copies consume the range budget when the stored range alone fits.</summary>
    [Fact]
    public async Task DomainCopiesExhaustTheBudgetForAnOtherwiseAdmittedMultiPageRange()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        EventEnvelope stored = CreateEvent() with { Payload = new byte[128 * 1024] };
        Store(stateManager, stored, head: 258, count: 258);
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);
        using DaprProductionLogicalReplay range = await reader.ReadRangeAsync(
            Identity, "r", 1, 258, CancellationToken.None, 258, includeDomainView: false).ConfigureAwait(true);

        range.StoredEvents.Count.ShouldBe(258);
        range.DomainEvents.ShouldBeEmpty();
        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadRangeAsync(Identity, "r", 1, 258, CancellationToken.None, 258, includeDomainView: true))
            .ConfigureAwait(true);
        error.Message.ShouldContain("LegacyArrayLimit");
    }

    /// <summary>Proves cancellation after partial plaintext copying clears the allocation before it can escape.</summary>
    [Fact]
    public void DomainCopyCancellationClearsThePartiallyWrittenDestination()
    {
        using var cancellation = new CancellationTokenSource();
        var payload = new PartialCopyCancellationPayload(cancellation);
        byte[] destination = new byte[payload.Length];
        EventEnvelope source = CreateEvent();

        OperationCanceledException error = Should.Throw<OperationCanceledException>(() =>
            DaprProductionLogicalEventReader.CopyDomainEvent(source, payload, "json", destination));

        payload.CopiedBytes.ShouldBe(2);
        error.CancellationToken.ShouldBe(cancellation.Token);
        destination.ShouldBe([0, 0, 0, 0]);
        source.Payload.ShouldBe([1, 2]);
    }

    /// <summary>Verifies the addressed reconstruction count limit is checked before actor access.</summary>
    [Fact]
    public async Task AddressedReconstructionCountLimitRejectsBeforeActorRead()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);
        AggregateReconstructionResult result = await CreateReconstructor(out _).ReconstructAddressedAsync(
            Identity, "r", reader, CreateSnapshotManager(), 32_769).ConfigureAwait(true);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.Unexpected);
        result.Message.ShouldContain("LegacyArrayLimit");
        stateManager.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Verifies opaque metadata and provider failures retain their safe protected-data reasons.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProtectionFailuresKeepTheirTypedReason(bool opaque)
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        EventEnvelope stored = CreateEvent();
        if (opaque)
        {
            stored = stored with
            {
                Extensions = EventStorePayloadProtectionMetadataCarrier.Write(
                (IDictionary<string, string>?)null, EventStorePayloadProtectionMetadata.ProviderOpaque())
            };
        }
        else
        {
            _ = protection.TryUnprotectEventPayloadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(),
                Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<EventStorePayloadProtectionMetadata>(), Arg.Any<CancellationToken>())
                .Returns<Task<PayloadUnprotectionOutcome>>(_ => throw new InvalidOperationException("provider-secret"));
        }
        Store(stateManager, stored);
        var reader = CreateReader(registry, stateManager, registry.Fingerprint, protection: protection);
        ProtectedDataUnreadableException error = await Should.ThrowAsync<ProtectedDataUnreadableException>(() =>
            reader.ReadPageAsync(Identity, "r", 1, 1, CancellationToken.None)).ConfigureAwait(true);
        error.Reason.ShouldBe(opaque ? UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation
            : UnreadableProtectedDataReason.ProviderUnavailable);
        error.Message.ShouldNotContain("provider-secret");
        if (opaque)
        {
            protection.ReceivedCalls().ShouldBeEmpty();
        }
        var reconstructor = CreateReconstructor(out IDomainServiceResolver resolver);
        _ = await Should.ThrowAsync<ProtectedDataUnreadableException>(() => reconstructor.ReconstructAddressedAsync(
            Identity, "r", reader, CreateSnapshotManager(), 1)).ConfigureAwait(true);
        resolver.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Verifies provider plaintext is cleared while an owned domain copy survives until disposal.</summary>
    [Fact]
    public async Task DistinctProviderPlaintextIsClearedWhileTheDomainCopyLivesUntilRangeDisposal()
    {
        using EventDomainRegistry registry = CreateRegistry();
        (IActorStateManager stateManager, EventEnvelope stored) = StoreCurrentV1();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        byte[] plaintext = [3, 4];
        _ = protection.TryUnprotectEventPayloadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(),
            Arg.Any<byte[]>(), "json", Arg.Any<EventStorePayloadProtectionMetadata>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PayloadUnprotectionOutcome.Readable(plaintext, "json", EventStorePayloadProtectionMetadata.Unprotected())));
        var reader = CreateReader(registry, stateManager, registry.Fingerprint, protection: protection);
        using DaprProductionLogicalReplay range = await reader.ReadRangeAsync(
            Identity, "r", 1, 1, CancellationToken.None, 1).ConfigureAwait(true);
        plaintext.ShouldBe([0, 0]);
        byte[] domainPayload = range.DomainEvents.ShouldHaveSingleItem().Payload;
        domainPayload.ShouldBe([3, 4]);
        domainPayload.ShouldNotBeSameAs(stored.Payload);
        range.Dispose();
        domainPayload.ShouldBe([0, 0]);
        stored.Payload.ShouldBe([1, 2]);
    }

    /// <summary>Verifies page disposal leaves source metadata charged until the actual range owner disposes.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RangeRetainsSourceMetadataChargeAfterPageDisposal(bool includeDomainView)
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        EventEnvelope stored = CreateEvent() with
        {
            Extensions = new Dictionary<string, string> { ["retained"] = "private-metadata" },
        };
        Store(stateManager, stored, head: 258, count: 258);
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);
        var budget = new EventBufferBudget();

        using DaprProductionLogicalReplay range = await reader.ReadRangeAsync(
            Identity, "r", 1, 258, CancellationToken.None, 258, 1, includeDomainView,
            arrayBudget: null, budget).ConfigureAwait(true);

        range.StoredEvents.Count.ShouldBe(258);
        range.StoredEvents[0].Extensions.ShouldNotBeSameAs(stored.Extensions);
        range.StoredEvents[0].Extensions!["retained"].ShouldBe("private-metadata");
        int domainBytes = range.DomainEvents.Sum(envelope => envelope.Payload.Length);
        budget.LiveBytes.ShouldBeGreaterThan(domainBytes);
        budget.LiveBytes.ShouldBeLessThan(1024 * 1024);
        range.Dispose();
        budget.LiveBytes.ShouldBe(0);
        stored.Payload.ShouldBe([1, 2]);
    }

    /// <summary>Verifies a later-page refusal releases metadata already transferred from a disposed earlier page.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaterPageRefusalReleasesTransferredMetadataAndDomainCharges(bool includeDomainView)
    {
        using EventDomainRegistry registry = CreateRegistry();
        (IActorStateManager stateManager, EventEnvelope stored) = StoreCurrentV1(head: 258, count: 257);
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);
        var budget = new EventBufferBudget();

        _ = await Should.ThrowAsync<MissingEventException>(() => reader.ReadRangeAsync(
            Identity, "r", 1, 258, CancellationToken.None, 258, 1, includeDomainView,
            arrayBudget: null, budget)).ConfigureAwait(true);

        budget.LiveBytes.ShouldBe(0);
        stored.Payload.ShouldBe([1, 2]);
        _ = stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies distinct provider plaintext is cleared after cancellation or digest refusal.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DistinctProviderPlaintextIsClearedOnCancellationOrDigestRefusal(bool cancel)
    {
        using EventDomainRegistry registry = CreateRegistry();
        (IActorStateManager stateManager, EventEnvelope stored) = StoreCurrentV1(includeDigest: true);
        using var cancellation = new CancellationTokenSource();
        byte[] plaintext = [7, 8];
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        _ = protection.TryUnprotectEventPayloadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(),
            "json", Arg.Any<EventStorePayloadProtectionMetadata>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (cancel) { cancellation.Cancel(); }
                return Task.FromResult(PayloadUnprotectionOutcome.Readable(plaintext, "json", EventStorePayloadProtectionMetadata.Unprotected()));
            });
        var reader = CreateReader(registry, stateManager, registry.Fingerprint, protection: protection);
        if (cancel)
        {
            _ = await Should.ThrowAsync<OperationCanceledException>(() =>
                reader.ReadPageAsync(Identity, "r", 1, 1, cancellation.Token)).ConfigureAwait(true);
        }
        else
        {
            InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
                reader.ReadPageAsync(Identity, "r", 1, 1, cancellation.Token)).ConfigureAwait(true);
            error.Message.ShouldContain("LogicalDigestMismatch");
        }
        plaintext.ShouldBe([0, 0]);
        stored.Payload.ShouldBe([1, 2]);
    }

    /// <summary>Verifies typed and keyed command rehydration deliver equivalent plaintext current state.</summary>
    [Fact]
    public async Task CanonicalV1CommandStateMatchesTypedRehydrationWithPlaintextPayloads()
    {
        using EventEvolutionManifestCandidate candidate = CreateCandidate();
        var command = new CommandEnvelope("command", Identity.TenantId, Identity.Domain, Identity.AggregateId,
            "CreateOrder", [1], "correlation", null, "user", null);
        DomainServiceCurrentState? typedState = null;
        DomainServiceCurrentState? logicalState = null;
        for (int lane = 0; lane < 2; lane++)
        {
            IActorStateManager stateManager = Substitute.For<IActorStateManager>();
            EventEnvelope[] stored = StoreCanonicalHistory(stateManager);
            IEventPayloadProtectionService protection = CreateCanonicalProtection();
            IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
            _ = invoker.InvokeAsync(command, Arg.Any<object?>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    if (lane == 0) { typedState = call.ArgAt<object>(1).ShouldBeOfType<DomainServiceCurrentState>(); }
                    else { logicalState = call.ArgAt<object>(1).ShouldBeOfType<DomainServiceCurrentState>(); }
                    return DomainResult.NoOp();
                });
            ICommandAggregateTypeResolver resolver = Substitute.For<ICommandAggregateTypeResolver>();
            _ = resolver.ResolveAsync(command, Arg.Any<CancellationToken>()).Returns("r");
            var host = ActorHost.CreateForTest<AggregateActor>(new ActorTestOptions { ActorId = new ActorId(Identity.ActorId) });
            var actor = new AggregateActor(host, Substitute.For<ILogger<AggregateActor>>(), invoker,
                Substitute.For<ISnapshotManager>(), protection, Substitute.For<ICommandStatusStore>(),
                Substitute.For<IEventPublisher>(), Options.Create(new EventDrainOptions()), Options.Create(new BackpressureOptions()),
                Substitute.For<IDeadLetterPublisher>(), lane == 1 ? new KeyedCandidateProvider(candidate) : null, resolver);
            ActorStateManagerTestHelper.SetStateManager(actor, stateManager);
            CommandProcessingResult outcome = await actor.ProcessCommandAsync(command).ConfigureAwait(true);
            outcome.Accepted.ShouldBeTrue();
            stored.All(e => e.Payload.SequenceEqual(new byte[] { 9, (byte)e.SequenceNumber })).ShouldBeTrue();
        }
        typedState.ShouldNotBeNull();
        logicalState.ShouldNotBeNull();
        JsonSerializer.Serialize(logicalState).ShouldBe(JsonSerializer.Serialize(typedState));
        logicalState.Events.Count.ShouldBe(3);
        logicalState.Events[0].Payload.ShouldBe(JsonSerializer.SerializeToUtf8Bytes(new Legacy.Event(1)));
        logicalState.CurrentSequence.ShouldBe(3);
    }

    /// <summary>Verifies a current V1 caller pin needs no separate chain registration.</summary>
    [Fact]
    public async Task CallerSuppliedPinReadsHistoricalV1WithoutASeparateChainRegistration()
    {
        using EventEvolutionManifestCandidate candidate = CreateCandidate();
        (IActorStateManager stateManager, EventEnvelope stored) = StoreCurrentV1(includeDigest: false);
        DaprProductionLogicalEventReader? reader = DaprProductionLogicalEventReader.FromCallerPin(
            stateManager, new NoOpEventPayloadProtectionService(), candidate);
        var replay = new EventStreamReader(stateManager, Substitute.For<ILogger<EventStreamReader>>());

        _ = reader.ShouldNotBeNull();
        RehydrationResult? result = await replay.RehydrateAsync(
            Identity, snapshot: null, CancellationToken.None, reader, aggregateType: null).ConfigureAwait(true);

        _ = result.ShouldNotBeNull();
        result.EffectiveEvents.ShouldBeNull();
        result.Events.ShouldHaveSingleItem().Payload.ShouldBe(stored.Payload);
        result.Events[0].MessageId.ShouldBe(stored.MessageId);
        result.Events[0].AggregateType.ShouldBe("r");
        stored.Payload.ShouldBe([1, 2]);
    }

    /// <summary>Verifies versioned reconstruction publishes no partial aggregate state.</summary>
    [Fact]
    public async Task Version2ReconstructionAppliesNoPartialState()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        EventEnvelope stored = CreateEvent() with
        {
            MetadataVersion = 2,
            EventTypeName = "evt",
            EventContractType = "evt",
            PayloadVersion = 2,
        };
        Store(stateManager, stored);
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);
        var reconstructor = CreateReconstructor(out IDomainServiceResolver resolver);

        AggregateReconstructionResult result = await reconstructor.ReconstructAddressedAsync(
            Identity, "r", reader, CreateSnapshotManager(), upToSequence: 1).ConfigureAwait(true);

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.StateJson.ShouldBeNull();
        result.LastAppliedSequenceNumber.ShouldBe(0);
        stored.Payload.ShouldBe([1, 2]);
        _ = await resolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default!, default!, default).ConfigureAwait(true);
    }

    /// <summary>Verifies the implicit V1 persister refuses a versioned triplet before actor mutation.</summary>
    [Fact]
    public async Task ImplicitV1WriterStillRejectsVersionedTripletBeforeMutation()
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        IGlobalPositionAllocator allocator = Substitute.For<IGlobalPositionAllocator>();
        var persister = new EventPersister(
            stateManager,
            Substitute.For<ILogger<EventPersister>>(),
            protection,
            allocator);
        var versioned = new SerializedDomainEventPayload(
            "order-created", "{}"u8.ToArray(), "json", 2, "order-created", 2);
        var command = new CommandEnvelope(
            MessageId: "01J00000000000000000000001",
            TenantId: Identity.TenantId,
            Domain: Identity.Domain,
            AggregateId: Identity.AggregateId,
            CommandType: "Create",
            Payload: [1],
            CorrelationId: "correlation",
            CausationId: "causation",
            UserId: "user",
            Extensions: null);

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            persister.PersistEventsAsync(
                Identity, "r", command, DomainResult.Success([versioned]), "v1")).ConfigureAwait(true);

        error.Message.ShouldContain("CapabilityMismatch");
        stateManager.ReceivedCalls().ShouldBeEmpty();
        protection.ReceivedCalls().ShouldBeEmpty();
        _ = allocator.DidNotReceiveWithAnyArgs().AllocateAsync(default, default);
    }

    /// <summary>Verifies caller-pin hop refusal leaves replay and actor state unpublished.</summary>
    [Fact]
    public async Task FromCallerPinOnHopRegistryFailsClosedWithoutPartialReplayOrStateSave()
    {
        using EventEvolutionManifestCandidate candidate = CreateUpcastingCandidate();
        (IActorStateManager stateManager, EventEnvelope stored) = StoreCurrentV1();
        DaprProductionLogicalEventReader? reader = DaprProductionLogicalEventReader.FromCallerPin(
            stateManager, new NoOpEventPayloadProtectionService(), candidate);

        _ = reader.ShouldNotBeNull();
        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadRangeAsync(Identity, "r", 1, 1, CancellationToken.None, expectedActorHead: 1));

        error.Message.ShouldContain("CapabilityMismatch");
        stored.Payload.ShouldBe([1, 2]);
        stateManager.ReceivedCalls().Any(call =>
            call.GetMethodInfo().Name is "SaveStateAsync" or "SetStateAsync").ShouldBeFalse();
    }

    /// <summary>Verifies pinned production replay reads only the contiguous tail after a snapshot.</summary>
    [Fact]
    public async Task ProductionReplayWithSnapshotBehindHeadReturnsOnlyTheTail()
    {
        using EventDomainRegistry registry = CreateRegistry();
        (IActorStateManager stateManager, _) = StoreCurrentV1(head: 2, count: 2);
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);
        var replay = new EventStreamReader(stateManager, Substitute.For<ILogger<EventStreamReader>>());
        var snapshot = new SnapshotRecord(1, new { Name = "kept" }, DateTimeOffset.UnixEpoch, "d", "aggregate", "tenant");

        RehydrationResult? result = await replay.RehydrateAsync(
            Identity, snapshot, CancellationToken.None, reader, "r").ConfigureAwait(true);

        _ = result.ShouldNotBeNull();
        result.Events.Count.ShouldBe(1);
        result.Events[0].SequenceNumber.ShouldBe(2);
        result.LastSnapshotSequence.ShouldBe(1);
        result.CurrentSequence.ShouldBe(2);
    }

    /// <summary>Verifies logical rehydration refusal carries its safe code and bypasses domain invocation.</summary>
    [Fact]
    public async Task RejectedLogicalReadFailsTheCommandBeforeInvoke()
    {
        using EventEvolutionManifestCandidate candidate = CreateUpcastingCandidate();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        EventEnvelope stored = CreateEvent();
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(1, DateTimeOffset.UnixEpoch, "etag")));
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, stored));
        var host = ActorHost.CreateForTest<AggregateActor>(
            new ActorTestOptions { ActorId = new ActorId(Identity.ActorId) });
        ILogger<AggregateActor> logger = Substitute.For<ILogger<AggregateActor>>();
        _ = logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        IDeadLetterPublisher deadLetter = Substitute.For<IDeadLetterPublisher>();
        ICommandAggregateTypeResolver aggregateTypeResolver = Substitute.For<ICommandAggregateTypeResolver>();
        _ = aggregateTypeResolver.ResolveAsync(Arg.Any<CommandEnvelope>(), Arg.Any<CancellationToken>()).Returns("r");
        var actor = new AggregateActor(
            host,
            logger,
            invoker,
            Substitute.For<ISnapshotManager>(),
            new NoOpEventPayloadProtectionService(),
            Substitute.For<ICommandStatusStore>(),
            Substitute.For<IEventPublisher>(),
            Options.Create(new EventDrainOptions()),
            Options.Create(new BackpressureOptions()),
            deadLetter,
            new KeyedCandidateProvider(candidate),
            aggregateTypeResolver);
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);
        var command = new CommandEnvelope(
            MessageId: "msg-logical",
            TenantId: Identity.TenantId,
            Domain: Identity.Domain,
            AggregateId: Identity.AggregateId,
            CommandType: "CreateOrder",
            Payload: [1],
            CorrelationId: "corr-logical",
            CausationId: null,
            UserId: "user",
            Extensions: null);

        CommandProcessingResult result = await actor.ProcessCommandAsync(command);

        result.Accepted.ShouldBeFalse();
        result.FailureReason.ShouldContain("logical-event-read-rejected");
        _ = await deadLetter.Received(1).PublishDeadLetterAsync(Identity,
            Arg.Is<DeadLetterMessage>(message => message.ReasonCode == "logical-event-read-rejected"
                && message.ErrorMessage.Contains("logical-event-read-rejected", StringComparison.Ordinal)
                && !message.ErrorMessage.Contains(Identity.AggregateId, StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
        stored.Payload.ShouldBe([1, 2]);
        _ = stateManager.Received().TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>());
        invoker.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Proves an invocation-time writer fence keeps generic diagnostics after successful logical rehydration.</summary>
    [Fact]
    public async Task WriterCapabilityMismatchDoesNotReceiveTheLogicalReadRejectionCode()
    {
        using EventEvolutionManifestCandidate candidate = CreateCandidate();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        EventEnvelope[] stored = StoreCanonicalHistory(stateManager);
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns<Task<DomainResult>>(_ => throw new InvalidOperationException(
                "CapabilityMismatch: an implicit V1 writer returned a versioned event."));
        IDeadLetterPublisher deadLetter = Substitute.For<IDeadLetterPublisher>();
        ICommandAggregateTypeResolver resolver = Substitute.For<ICommandAggregateTypeResolver>();
        _ = resolver.ResolveAsync(Arg.Any<CommandEnvelope>(), Arg.Any<CancellationToken>()).Returns("r");
        var host = ActorHost.CreateForTest<AggregateActor>(new ActorTestOptions { ActorId = new ActorId(Identity.ActorId) });
        var actor = new AggregateActor(host, Substitute.For<ILogger<AggregateActor>>(), invoker,
            Substitute.For<ISnapshotManager>(), CreateCanonicalProtection(), Substitute.For<ICommandStatusStore>(),
            Substitute.For<IEventPublisher>(), Options.Create(new EventDrainOptions()), Options.Create(new BackpressureOptions()),
            deadLetter, new KeyedCandidateProvider(candidate), resolver);
        ActorStateManagerTestHelper.SetStateManager(actor, stateManager);
        var command = new CommandEnvelope("writer-command", Identity.TenantId, Identity.Domain, Identity.AggregateId,
            "CreateOrder", [1], "writer-correlation", null, "user", null);

        CommandProcessingResult result = await actor.ProcessCommandAsync(command).ConfigureAwait(true);

        result.Accepted.ShouldBeFalse();
        result.FailureReason.ShouldContain("protected-data-diagnostic-redacted");
        result.FailureReason.ShouldNotContain("logical-event-read-rejected");
        _ = await invoker.Received(1).InvokeAsync(command,
            Arg.Is<object?>(state => state is DomainServiceCurrentState && ((DomainServiceCurrentState)state).EventCount == 3),
            Arg.Any<CancellationToken>());
        _ = await deadLetter.Received(1).PublishDeadLetterAsync(Identity,
            Arg.Is<DeadLetterMessage>(message => message.ReasonCode == null
                && message.ErrorMessage.Contains("protected-data-diagnostic-redacted", StringComparison.Ordinal)
                && !message.ErrorMessage.Contains("logical-event-read-rejected", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
        stored.All(e => e.Payload.SequenceEqual(new byte[] { 9, (byte)e.SequenceNumber })).ShouldBeTrue();
    }

    private static DaprProductionLogicalEventReader CreateReader(
        EventDomainRegistry registry,
        IActorStateManager stateManager,
        string pin,
        ShrinkingLogicalEventUpcaster? upcaster = null,
        IEventPayloadProtectionService? protection = null)
    {
        var bindings = new Dictionary<(string Type, int Version), RegisteredEventUpcaster>();
        if (upcaster is not null)
        {
            bindings[("evt", 1)] = new RegisteredEventUpcaster("test-upcaster", upcaster, new byte[32]);
        }

        var executor = new EventUpcastChainExecutor(registry, bindings, static (_, _, _, _, _, _) => { });
        return new DaprProductionLogicalEventReader(
            stateManager,
            protection ?? new NoOpEventPayloadProtectionService(),
            registry,
            executor,
            pin);
    }

    private static (IActorStateManager StateManager, EventEnvelope Stored) StoreCurrentV1(
        bool includeDigest = false, long head = 1, int count = 1)
    {
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        EventEnvelope stored = CreateEvent();
        if (includeDigest)
        {
            stored = stored with
            {
                ApplicationPayloadDigest = EventLogicalDigest.Compute(
                    stored, "json", EventLogicalDigest.HashPayload(stored.Payload)),
            };
        }

        Store(stateManager, stored, head, count);
        return (stateManager, stored);
    }

    private static void Store(IActorStateManager stateManager, EventEnvelope stored, long head = 1, int count = 1)
    {
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(head, DateTimeOffset.UnixEpoch, "etag")));
        for (int sequence = 1; sequence <= count; sequence++)
        {
            EventEnvelope envelope = stored with { SequenceNumber = sequence, MessageId = $"01J{sequence:D23}" };
            _ = stateManager.TryGetStateAsync<EventEnvelope>(
                $"{Identity.EventStreamKeyPrefix}{sequence}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, envelope));
        }
    }

    private static DaprAggregateStateReconstructor CreateReconstructor(out IDomainServiceResolver resolver)
    {
        resolver = Substitute.For<IDomainServiceResolver>();
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DomainServiceRegistration?)null);
        return new DaprAggregateStateReconstructor(
            Substitute.For<Dapr.Client.DaprClient>(),
            Substitute.For<IHttpClientFactory>(),
            resolver,
            NullLogger<DaprAggregateStateReconstructor>.Instance);
    }

    private static SnapshotManager CreateSnapshotManager()
        => new(
            Options.Create(new SnapshotOptions()),
            Substitute.For<ILogger<SnapshotManager>>(),
            new NoOpEventPayloadProtectionService());

    private static EventEnvelope CreateEvent() => new(
        MessageId: "01J00000000000000000000001", AggregateId: "aggregate", AggregateType: "r", TenantId: "tenant",
        Domain: "d", SequenceNumber: 1, GlobalPosition: 0, Timestamp: DateTimeOffset.UnixEpoch,
        CorrelationId: "correlation", CausationId: "causation", UserId: "user",
        DomainServiceVersion: "v1", EventTypeName: "Legacy.Event", MetadataVersion: 1,
        SerializationFormat: "json", Payload: [1, 2], Extensions: null);

    /// <summary>Builds the fixture domain's pinned, current-V1 manifest candidate.</summary>
    /// <returns>The disposable caller-supplied fixture pin.</returns>
    internal static EventEvolutionManifestCandidate CreateCandidate()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Hexalith.EventStore.slnx")))
        {
            root = root.Parent;
        }

        string path = Path.Combine(root?.FullName ?? throw new DirectoryNotFoundException(),
            "tests", "Hexalith.EventStore.Client.Tests", "Events", "Fixtures", "EventRegistryV17.json");
        Dictionary<string, string> fixture = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
        ReadOnlyMemory<byte>[] rows =
        [
            Convert.FromHexString(fixture["AliasRow"]),
            Convert.FromHexString(fixture["DescriptorRow"]),
            Convert.FromHexString(fixture["VersionRow"]),
            Convert.FromHexString(fixture["SharedRow"]),
        ];
        using var registry = new EventDomainRegistry("d", rows);
        return new EventEvolutionManifestCandidate("d", rows, registry.Fingerprint, referencedManifestBytes: 0);
    }

    /// <summary>Configures three addressed events whose stored bytes differ from their logical payloads.</summary>
    /// <param name="stateManager">The substitute actor state manager to configure.</param>
    /// <returns>The immutable stored envelopes used to verify replay leaves history unchanged.</returns>
    internal static EventEnvelope[] StoreCanonicalHistory(IActorStateManager stateManager)
    {
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(3, DateTimeOffset.UnixEpoch, "etag")));
        var events = new EventEnvelope[3];
        for (int i = 0; i < events.Length; i++)
        {
            int sequence = i + 1;
            EventEnvelope envelope = CreateEvent() with
            {
                SequenceNumber = sequence,
                MessageId = $"01J{sequence:D23}",
                Payload = [9, (byte)sequence],
            };
            byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(new Legacy.Event(sequence));
            events[i] = envelope with
            {
                ApplicationPayloadDigest = EventLogicalDigest.Compute(envelope, "json", EventLogicalDigest.HashPayload(plaintext)),
            };
            _ = stateManager.TryGetStateAsync<EventEnvelope>($"{Identity.EventStreamKeyPrefix}{sequence}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, events[i]));
        }

        return events;
    }

    /// <summary>Creates a provider that returns independently owned canonical plaintext from fixture storage bytes.</summary>
    /// <returns>The non-identity protection service used by replay and command-state tests.</returns>
    internal static IEventPayloadProtectionService CreateCanonicalProtection()
    {
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        _ = protection.TryUnprotectEventPayloadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(),
            Arg.Any<byte[]>(), "json", Arg.Any<EventStorePayloadProtectionMetadata>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(PayloadUnprotectionOutcome.Readable(
                JsonSerializer.SerializeToUtf8Bytes(new Legacy.Event(call.ArgAt<byte[]>(2)[1])), "json",
                EventStorePayloadProtectionMetadata.Unprotected())));
        return protection;
    }

    /// <summary>Creates the addressed reconstructor with a registered fixture domain and canonical replay transport.</summary>
    /// <param name="dapr">The Dapr client that constructs the invocation request.</param>
    /// <param name="httpClient">The client whose handler runs the real canonical Apply replay.</param>
    /// <param name="resolver">The configured domain registration resolver.</param>
    /// <returns>The reconstructor used by positive replay and manual-snapshot controls.</returns>
    internal static DaprAggregateStateReconstructor CreateCanonicalReconstructor(
        Dapr.Client.DaprClient dapr, HttpClient httpClient, out IDomainServiceResolver resolver)
    {
        resolver = Substitute.For<IDomainServiceResolver>();
        _ = resolver.ResolveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new DomainServiceRegistration("domain", "process-command", call.ArgAt<string>(0), call.ArgAt<string>(1), "v1"));
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient(Arg.Any<string>()).Returns(httpClient);
        return new DaprAggregateStateReconstructor(dapr, factory, resolver, NullLogger<DaprAggregateStateReconstructor>.Instance);
    }

    private static EventDomainRegistry CreateRegistry(bool upcasting = false, EventEvolutionCapabilityLoss? capabilityLoss = null)
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Hexalith.EventStore.slnx")))
        {
            root = root.Parent;
        }

        string path = Path.Combine(root?.FullName ?? throw new DirectoryNotFoundException(),
            "tests", "Hexalith.EventStore.Client.Tests", "Events", "Fixtures", "EventRegistryV17.json");
        Dictionary<string, string> fixture = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
        if (!upcasting)
        {
            return new EventDomainRegistry("d", [
                Convert.FromHexString(fixture["AliasRow"]),
                Convert.FromHexString(fixture["DescriptorRow"]),
                Convert.FromHexString(fixture["VersionRow"]),
                Convert.FromHexString(fixture["SharedRow"]),
            ], capabilityLoss ?? EventEvolutionCapabilityLoss.Process);
        }

        return new EventDomainRegistry("d", CreateUpcastingRows(fixture), capabilityLoss ?? EventEvolutionCapabilityLoss.Process);
    }

    /// <summary>Builds the fixture domain's caller pin whose retained V1 events require an upcast hop.</summary>
    /// <returns>The disposable caller-supplied upcasting fixture manifest.</returns>
    internal static EventEvolutionManifestCandidate CreateUpcastingCandidate()
    {
        ReadOnlyMemory<byte>[] rows = CreateUpcastingRows(LoadFixture());
        using var registry = new EventDomainRegistry("d", rows);
        return new EventEvolutionManifestCandidate("d", rows, registry.Fingerprint, referencedManifestBytes: 0);
    }

    private static ReadOnlyMemory<byte>[] CreateUpcastingRows(Dictionary<string, string> fixture)
    {
        byte[] descriptor = Convert.FromHexString(fixture["DescriptorRow"]);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(descriptor.AsSpan(22, 4), 2);
        byte[] firstVersion = Convert.FromHexString(fixture["VersionRow"]);
        byte[] secondVersion = firstVersion.ToArray();
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(secondVersion.AsSpan(13, 4), 2);
        using var edge = new EventEvolutionBinaryWriter(1024);
        edge.WriteByte(0x45);
        edge.WriteString("d");
        edge.WriteString("evt");
        edge.WriteInt32(1);
        edge.WriteUInt16(12);
        edge.WriteByte(1);
        edge.WriteInt32(2);
        edge.WriteByte(2);
        edge.WriteString("json");
        edge.WriteByte(3);
        edge.WriteString("json");
        edge.WriteByte(4);
        edge.WriteString("serializer");
        edge.WriteByte(5);
        edge.WriteHash(new byte[32]);
        edge.WriteByte(6);
        edge.WriteHash(new byte[32]);
        edge.WriteByte(7);
        edge.WriteString("test-upcaster");
        edge.WriteByte(8);
        using (FileStream assembly = File.OpenRead(typeof(ShrinkingLogicalEventUpcaster).Assembly.Location))
        {
            edge.WriteHash(SHA256.HashData(assembly));
        }

        edge.WriteByte(9);
        edge.WriteHash(new byte[32]);
        edge.WriteByte(10);
        edge.WriteString("no-payload-identity");
        edge.WriteByte(11);
        edge.WriteHash(new byte[32]);
        edge.WriteByte(12);
        edge.WriteHash(new byte[32]);
        return
        [
            Convert.FromHexString(fixture["AliasRow"]),
            descriptor,
            firstVersion,
            secondVersion,
            edge.CopyEncodedBytes(),
            Convert.FromHexString(fixture["SharedRow"]),
        ];
    }

    private static Dictionary<string, string> LoadFixture()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Hexalith.EventStore.slnx")))
        {
            root = root.Parent;
        }

        string path = Path.Combine(root?.FullName ?? throw new DirectoryNotFoundException(),
            "tests", "Hexalith.EventStore.Client.Tests", "Events", "Fixtures", "EventRegistryV17.json");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
    }

}
