using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Events;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;

public sealed class DaprLogicalEventReaderTests
{
    private static readonly AggregateIdentity Identity = new("tenant", "d", "aggregate");

    [Fact]
    public async Task ReadsBoundedAddressedPageAtStableLogicalHead()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        AggregateMetadata metadata = new(2, DateTimeOffset.UnixEpoch, null);
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, metadata));
        EventEnvelope first = CreateEvent();
        EventEnvelope second = first with { SequenceNumber = 2, MessageId = "message-2" };
        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, first));
        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}2", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, second));
        _ = protection.TryUnprotectEventPayloadAsync(Identity, Arg.Any<string>(),
            Arg.Any<byte[]>(), "json", Arg.Any<EventStorePayloadProtectionMetadata>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PayloadUnprotectionOutcome.Readable([1, 2], "json",
                EventStorePayloadProtectionMetadata.Unprotected())));
        var reader = CreateReader(registry, stateManager, protection);

        using DaprLogicalEventPage page = await reader.ReadPageAsync(
            Identity, "r", 1, 2, CancellationToken.None).ConfigureAwait(true);

        page.StartSequence.ShouldBe(1);
        page.ActorHead.ShouldBe(2);
        page.Events.Select(static view => view.SequenceNumber).ShouldBe([1L, 2L]);
        _ = await stateManager.Received(2).TryGetStateAsync<AggregateMetadata>(
            Identity.MetadataKey, Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    [Fact]
    public async Task WrongAggregateTypeInPageRefusesBeforeProtection()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true,
                new AggregateMetadata(1, DateTimeOffset.UnixEpoch, null)));
        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, CreateEvent()));
        var reader = CreateReader(registry, stateManager, protection);

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadPageAsync(Identity, "wrong-route", 1, 1, CancellationToken.None)).ConfigureAwait(true);

        error.Message.ShouldContain("AddressMismatch");
        _ = protection.DidNotReceiveWithAnyArgs().TryUnprotectEventPayloadAsync(
            default!, default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task ChangedActorHeadRefusesCompletedLogicalPage()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true,
                    new AggregateMetadata(1, DateTimeOffset.UnixEpoch, null)),
                new ConditionalValue<AggregateMetadata>(true,
                    new AggregateMetadata(2, DateTimeOffset.UnixEpoch, null)));
        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, CreateEvent()));
        _ = protection.TryUnprotectEventPayloadAsync(Identity, Arg.Any<string>(),
            Arg.Any<byte[]>(), "json", Arg.Any<EventStorePayloadProtectionMetadata>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PayloadUnprotectionOutcome.Readable([1, 2], "json",
                EventStorePayloadProtectionMetadata.Unprotected())));
        var reader = CreateReader(registry, stateManager, protection);

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadPageAsync(Identity, "r", 1, 1, CancellationToken.None)).ConfigureAwait(true);

        error.Message.ShouldContain("SourceHeadChanged");
    }

    [Fact]
    public async Task ReadsAddressedDaprValueAndResolvesAliasWithoutChangingStoredBytes()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        EventEnvelope stored = CreateEvent();
        stored = stored with {
            ApplicationPayloadDigest = EventLogicalDigest.Compute(stored, "json", EventLogicalDigest.HashPayload(stored.Payload)),
        };
        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, stored));
        _ = protection.TryUnprotectEventPayloadAsync(Identity, stored.EventTypeName,
            Arg.Any<byte[]>(), stored.SerializationFormat, Arg.Any<EventStorePayloadProtectionMetadata>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PayloadUnprotectionOutcome.Readable([1, 2], "json",
                EventStorePayloadProtectionMetadata.Unprotected())));
        var reader = CreateReader(registry, stateManager, protection);

        using DaprLogicalEventView view = await reader.ReadAsync(Identity, 1, CancellationToken.None).ConfigureAwait(true);

        view.MessageId.ShouldBe("message");
        view.CorrelationId.ShouldBe("correlation");
        view.CausationId.ShouldBe("causation");
        view.SequenceNumber.ShouldBe(1);
        view.Resolved.CanonicalType.ShouldBe("evt");
        view.Resolved.SourceVersion.ShouldBe(1);
        view.Resolved.CurrentVersion.ShouldBe(1);
        byte[] effective = new byte[view.Resolved.Payload.Length];
        view.Resolved.Payload.CopyTo(0, effective);
        effective.ShouldBe([1, 2]);
        stored.Payload.ShouldBe([1, 2]);
        _ = await stateManager.Received(1).TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>()).ConfigureAwait(true);
    }

    [Fact]
    public async Task MismatchedAddressRefusesBeforeUnprotectOrUpcast()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        EventEnvelope stored = CreateEvent() with { SequenceNumber = 2 };
        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, stored));
        var reader = CreateReader(registry, stateManager, protection);

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadAsync(Identity, 1, CancellationToken.None)).ConfigureAwait(true);

        error.Message.ShouldContain("AddressMismatch");
        _ = protection.DidNotReceiveWithAnyArgs().TryUnprotectEventPayloadAsync(
            default!, default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task UnknownAliasAndUnreadablePayloadRemainHeld()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        EventEnvelope stored = CreateEvent() with { EventTypeName = "Unlisted.Event" };
        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, stored));
        _ = protection.TryUnprotectEventPayloadAsync(Identity, stored.EventTypeName,
            Arg.Any<byte[]>(), stored.SerializationFormat, Arg.Any<EventStorePayloadProtectionMetadata>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PayloadUnprotectionOutcome.Readable([1, 2], "json",
                EventStorePayloadProtectionMetadata.Unprotected())));
        var reader = CreateReader(registry, stateManager, protection);

        InvalidOperationException unknown = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadAsync(Identity, 1, CancellationToken.None)).ConfigureAwait(true);
        unknown.Message.ShouldContain("UnknownEventContract");

        // Unreadability is tested against a separately allow-listed source; unknown
        // source identity now refuses before the protection provider is invoked.
        stored = stored with { EventTypeName = "Legacy.Event" };
        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, stored));

        _ = protection.TryUnprotectEventPayloadAsync(Identity, stored.EventTypeName,
            Arg.Any<byte[]>(), stored.SerializationFormat, Arg.Any<EventStorePayloadProtectionMetadata>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PayloadUnprotectionOutcome.Unreadable(
                UnreadableProtectedDataReason.ProviderUnavailable)));
        _ = await Should.ThrowAsync<ProtectedDataUnreadableException>(() =>
            reader.ReadAsync(Identity, 1, CancellationToken.None)).ConfigureAwait(true);
    }

    [Fact]
    public async Task MatchingEnvelopeAndAddressCannotOverrideManifestAggregateRoute()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(1, DateTimeOffset.UnixEpoch, null)));
        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, CreateEvent() with { AggregateType = "foreign" }));
        var reader = CreateReader(registry, stateManager, protection);

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadPageAsync(Identity, "foreign", 1, 1, CancellationToken.None)).ConfigureAwait(true);

        error.Message.ShouldContain("AddressMismatch");
        _ = protection.DidNotReceiveWithAnyArgs().TryUnprotectEventPayloadAsync(
            default!, default!, default!, default!, default!, default);
    }

    [Theory]
    [InlineData(2, 1, 1, 1, 1, "SourceHeadChanged")]
    [InlineData(2, 2, 1, 2, 2, "ReplayRestartRequired")]
    [InlineData(2, 2, 2, 2, 1, "SourceHeadChanged")]
    [InlineData(2, 0, 1, 2, 0, "SourceHeadChanged")]
    public async Task RefusesChangedPinOrUnavailablePrefixBeforeReadingEvents(
        long head, long floor, long start, long expectedHead, long expectedFloor, string reason)
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(head, DateTimeOffset.UnixEpoch, null, floor)));
        var reader = CreateReader(registry, stateManager, protection);

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadPageAsync(Identity, "r", start, 1, CancellationToken.None,
                expectedHead, expectedFloor)).ConfigureAwait(true);

        error.Message.ShouldContain(reason);
        _ = stateManager.DidNotReceiveWithAnyArgs().TryGetStateAsync<EventEnvelope>(default!, default);
    }

    [Fact]
    public async Task PageOwnsOneComposedBudgetAndClearsOwnersOnDisposal()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        var budget = new EventBufferBudget();
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(2, DateTimeOffset.UnixEpoch, "etag")));
        for (int sequence = 1; sequence <= 2; sequence++)
        {
            _ = stateManager.TryGetStateAsync<EventEnvelope>(
                $"{Identity.EventStreamKeyPrefix}{sequence}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, CreateEvent() with { SequenceNumber = sequence }));
        }
        var reader = CreateReader(registry, stateManager, new NoOpEventPayloadProtectionService());

        DaprLogicalEventPage page = await reader.ReadPageAsync(Identity, "r", 1, 2,
            CancellationToken.None, 2, 1, budget).ConfigureAwait(true);

        page.RetainedFloor.ShouldBe(1);
        budget.LiveBytes.ShouldBe(4);
        page.Dispose();
        budget.LiveBytes.ShouldBe(0);
        Should.Throw<ObjectDisposedException>(() => page.Events[0].Resolved.Payload.CopyTo(0, new byte[2]));
        page.Dispose();
        budget.LiveBytes.ShouldBe(0);
    }

    [Fact]
    public async Task PageBudgetRefusesBeforeProtectionAndReleasesReservation()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        var budget = new EventBufferBudget(64 * 1024 * 1024);
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(1, DateTimeOffset.UnixEpoch, null)));
        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, CreateEvent()));
        var reader = CreateReader(registry, stateManager, protection);

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadPageAsync(Identity, "r", 1, 1, CancellationToken.None, sharedBudget: budget)).ConfigureAwait(true);

        error.Message.ShouldContain("ScratchLimit");
        budget.LiveBytes.ShouldBe(0);
        _ = protection.DidNotReceiveWithAnyArgs().TryUnprotectEventPayloadAsync(
            default!, default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task ChangedMetadataETagClearsCompletedPageBeforeReturningIt()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        var budget = new EventBufferBudget();
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(1, DateTimeOffset.UnixEpoch, "before")),
                new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(1, DateTimeOffset.UnixEpoch, "after")));
        EventEnvelope source = CreateEvent();
        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, source));
        var reader = CreateReader(registry, stateManager, new NoOpEventPayloadProtectionService());

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadPageAsync(Identity, "r", 1, 1, CancellationToken.None, sharedBudget: budget)).ConfigureAwait(true);

        error.Message.ShouldContain("SourceHeadChanged");
        budget.LiveBytes.ShouldBe(0);
        source.Payload.ShouldBe([1, 2]);
    }

    [Fact]
    public async Task MissingAddressedEventRefusesBeforeProtection()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(false, default!));
        var reader = CreateReader(registry, stateManager, protection);

        MissingEventException error = await Should.ThrowAsync<MissingEventException>(() =>
            reader.ReadAsync(Identity, 1, CancellationToken.None)).ConfigureAwait(true);

        error.SequenceNumber.ShouldBe(1);
        _ = protection.DidNotReceiveWithAnyArgs().TryUnprotectEventPayloadAsync(
            default!, default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task ChangedApplicationPayloadOrMetadataOrMissingV2DigestRefusesBeforeResolution()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        EventEnvelope versioned = CreateEvent() with {
            MetadataVersion = 2, EventTypeName = "evt", EventContractType = "evt", PayloadVersion = 1,
        };
        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, versioned));
        _ = protection.TryUnprotectEventPayloadAsync(Identity, versioned.EventTypeName,
            Arg.Any<byte[]>(), versioned.SerializationFormat, Arg.Any<EventStorePayloadProtectionMetadata>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(PayloadUnprotectionOutcome.Readable([1, 2], "json",
                EventStorePayloadProtectionMetadata.Unprotected())));
        var reader = CreateReader(registry, stateManager, protection);

        InvalidOperationException missingDigest = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadAsync(Identity, 1, CancellationToken.None)).ConfigureAwait(true);
        missingDigest.Message.ShouldContain("LogicalDigestMismatch");

        versioned = versioned with {
            ApplicationPayloadDigest = EventLogicalDigest.Compute(versioned, "json",
                EventLogicalDigest.HashPayload("different"u8)),
        };
        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, versioned));
        InvalidOperationException changedPayload = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadAsync(Identity, 1, CancellationToken.None)).ConfigureAwait(true);
        changedPayload.Message.ShouldContain("LogicalDigestMismatch");

        versioned = versioned with {
            ApplicationPayloadDigest = EventLogicalDigest.Compute(versioned, "json",
                EventLogicalDigest.HashPayload([1, 2])),
        };
        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, versioned));
        using DaprLogicalEventView accepted = await reader.ReadAsync(Identity, 1, CancellationToken.None).ConfigureAwait(true);
        accepted.Resolved.CanonicalType.ShouldBe("evt");

        _ = stateManager.TryGetStateAsync<EventEnvelope>(
            $"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, versioned with { CorrelationId = "changed" }));
        InvalidOperationException changedMetadata = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadAsync(Identity, 1, CancellationToken.None)).ConfigureAwait(true);
        changedMetadata.Message.ShouldContain("LogicalDigestMismatch");
    }

    private static DaprLogicalEventReader CreateReader(EventDomainRegistry registry,
        IActorStateManager stateManager, IEventPayloadProtectionService protection)
    {
        var executor = new EventUpcastChainExecutor(registry,
            new Dictionary<(string, int), RegisteredEventUpcaster>(),
            static (_, _, _, _, _, _) => { });
        return new DaprLogicalEventReader(stateManager, protection,
            new EventLogicalViewResolver(registry, executor));
    }

    private static EventEnvelope CreateEvent() => new(
        MessageId: "message", AggregateId: "aggregate", AggregateType: "r", TenantId: "tenant",
        Domain: "d", SequenceNumber: 1, GlobalPosition: 0, Timestamp: DateTimeOffset.UnixEpoch,
        CorrelationId: "correlation", CausationId: "causation", UserId: "user",
        DomainServiceVersion: "v1", EventTypeName: "Legacy.Event", MetadataVersion: 1,
        SerializationFormat: "json", Payload: [1, 2], Extensions: null);

    private static EventDomainRegistry CreateRegistry()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Hexalith.EventStore.slnx")))
        {
            root = root.Parent;
        }
        string path = Path.Combine(root?.FullName ?? throw new DirectoryNotFoundException(),
            "tests", "Hexalith.EventStore.Client.Tests", "Events", "Fixtures", "EventRegistryV17.json");
        Dictionary<string, string> fixture = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(path))!;
        return new EventDomainRegistry("d", [
            Convert.FromHexString(fixture["AliasRow"]),
            Convert.FromHexString(fixture["DescriptorRow"]),
            Convert.FromHexString(fixture["VersionRow"]),
            Convert.FromHexString(fixture["SharedRow"]),
        ]);
    }
}
