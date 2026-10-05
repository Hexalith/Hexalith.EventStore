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

    [Fact]
    public async Task ZeroHopV1ReplayKeepsStoredBytesAndMessageId()
    {
        using EventDomainRegistry registry = CreateRegistry();
        (IActorStateManager stateManager, EventEnvelope stored) = StoreCurrentV1();
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);

        using DaprLogicalEventPage page = await reader.ReadPageAsync(
            Identity, "r", 1, 1, CancellationToken.None).ConfigureAwait(true);

        page.Events.ShouldHaveSingleItem().MessageId.ShouldBe("message");
        page.Events[0].Source.Payload.ShouldBe(stored.Payload);
        stored.Payload.ShouldBe([1, 2]);
        DaprProductionLogicalEventReader.IsZeroHopV1(page.Events[0]).ShouldBeTrue();
        _ = stateManager.DidNotReceiveWithAnyArgs().SetStateAsync(default!, default(EventEnvelope)!, default);
        _ = stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

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

        AggregateReconstructionResult result = await reconstructor.ReconstructAddressedAsync(
            Identity, "r", reader, CreateSnapshotManager(), upToSequence: 1).ConfigureAwait(true);

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.StateJson.ShouldBeNull();
        result.LastAppliedSequenceNumber.ShouldBe(0);
        stored.Payload.ShouldBe([1, 2]);
        _ = await resolver.DidNotReceiveWithAnyArgs().ResolveAsync(default!, default!, default!, default).ConfigureAwait(true);
        _ = stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("missing-alias", 1, null, null, "UnknownEventContract")]
    [InlineData("evt", 2, "evt", 2, "UnknownEventContract")]
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

    [Fact]
    public async Task UpcastReplayKeepsStoredBytesAndExposesEffectivePayload()
    {
        using EventDomainRegistry registry = CreateRegistry(upcasting: true);
        (IActorStateManager stateManager, EventEnvelope stored) = StoreCurrentV1();
        var upcaster = new ShrinkingLogicalEventUpcaster();
        var reader = CreateReader(registry, stateManager, registry.Fingerprint, upcaster);
        var replay = new EventStreamReader(stateManager, Substitute.For<ILogger<EventStreamReader>>());
        SnapshotManager snapshots = CreateSnapshotManager();

        RehydrationResult? result = await replay.RehydrateAsync(
            Identity, snapshot: null, CancellationToken.None, reader, "r", snapshots).ConfigureAwait(true);

        _ = result.ShouldNotBeNull();
        result.Events.ShouldHaveSingleItem().Payload.ShouldBe([1, 2]);
        result.Events[0].MessageId.ShouldBe("message");
        _ = result.EffectiveEvents.ShouldNotBeNull();
        result.EffectiveEvents.ShouldHaveSingleItem().Payload.ShouldBe([1]);
        result.EffectiveEvents[0].MessageId.ShouldBe("message");
        result.EffectiveEvents[0].MetadataVersion.ShouldBe(1);
        stored.Payload.ShouldBe([1, 2]);
        upcaster.Calls.ShouldBe(1);
        _ = stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

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

    [Fact]
    public async Task AddressedReconstructionOfCurrentV1ReachesCanonicalReplay()
    {
        using EventDomainRegistry registry = CreateRegistry();
        (IActorStateManager stateManager, EventEnvelope stored) = StoreCurrentV1(includeDigest: true);
        var reader = CreateReader(registry, stateManager, registry.Fingerprint);
        var reconstructor = CreateReconstructor(out IDomainServiceResolver resolver);

        AggregateReconstructionResult result = await reconstructor.ReconstructAddressedAsync(
            Identity, "r", reader, CreateSnapshotManager(), upToSequence: 1).ConfigureAwait(true);

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.ErrorCategory.ShouldBe(AggregateReconstructionErrorCategory.UnknownAggregateType);
        result.StateJson.ShouldBeNull();
        result.LastAppliedSequenceNumber.ShouldBe(0);
        stored.Payload.ShouldBe([1, 2]);
        _ = await resolver.Received(1).ResolveAsync(Identity.TenantId, Identity.Domain, "v1", Arg.Any<CancellationToken>())
            .ConfigureAwait(true);
    }

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
            "order-created", [1, 2, 3], "json", 2, "order-created", 2);
        var command = new CommandEnvelope(
            MessageId: "message",
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
            Substitute.For<IDeadLetterPublisher>(),
            new KeyedCandidateProvider(candidate));
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
        stored.Payload.ShouldBe([1, 2]);
        _ = stateManager.Received().TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>());
        invoker.ReceivedCalls().ShouldBeEmpty();
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
            EventEnvelope envelope = stored with { SequenceNumber = sequence, MessageId = sequence == 1 ? stored.MessageId : $"message-{sequence}" };
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
        MessageId: "message", AggregateId: "aggregate", AggregateType: "r", TenantId: "tenant",
        Domain: "d", SequenceNumber: 1, GlobalPosition: 0, Timestamp: DateTimeOffset.UnixEpoch,
        CorrelationId: "correlation", CausationId: "causation", UserId: "user",
        DomainServiceVersion: "v1", EventTypeName: "Legacy.Event", MetadataVersion: 1,
        SerializationFormat: "json", Payload: [1, 2], Extensions: null);

    private static EventEvolutionManifestCandidate CreateCandidate()
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

    private static EventDomainRegistry CreateRegistry(bool upcasting = false)
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
            ]);
        }

        return new EventDomainRegistry("d", CreateUpcastingRows(fixture));
    }

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

    private sealed class KeyedCandidateProvider(EventEvolutionManifestCandidate candidate) : IKeyedServiceProvider
    {
        public object? GetService(Type serviceType) => null;

        public object? GetKeyedService(Type serviceType, object? serviceKey)
            => serviceType == typeof(EventEvolutionManifestCandidate) && Equals(serviceKey, "d")
                ? candidate
                : null;

        public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
            => GetKeyedService(serviceType, serviceKey)
                ?? throw new InvalidOperationException($"No keyed service for {serviceType}.");
    }
}
