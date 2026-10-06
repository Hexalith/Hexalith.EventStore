using System.Text.Json;
using System.Security.Cryptography;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Events;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Checks Dapr logical event admission, private ownership and fixed-prefix refusal preserve stored evidence.</summary>
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
        _ = await stateManager.Received(3).TryGetStateAsync<AggregateMetadata>(
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
        budget.LiveBytes.ShouldBeGreaterThan(4);
        budget.LiveBytes.ShouldBeLessThan(4 + 2 * 512 * 1024);
        page.Dispose();
        budget.LiveBytes.ShouldBe(0);
        Should.Throw<ObjectDisposedException>(() => page.Events[0].Resolved.Payload.CopyTo(0, new byte[2]));
        page.Dispose();
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Checks measured zero-hop V1 payloads fit the composed budget and disposal preserves actor bytes.</summary>
    [Theory]
    [InlineData(22)]
    [InlineData(64)]
    public async Task NoOpZeroHopReadsMeasuredLargeV1WithoutRedundantPrivateCopies(int payloadMiB)
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        byte[] original = new byte[payloadMiB * 1024 * 1024];
        original[0] = 17;
        original[^1] = 91;
        EventEnvelope stored = CreateEvent() with { Payload = original };
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(1, DateTimeOffset.UnixEpoch, "etag")));
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, stored));
        var budget = new EventBufferBudget();
        var reader = CreateReader(registry, stateManager, new NoOpEventPayloadProtectionService());

        using DaprLogicalEventPage page = await reader.ReadPageAsync(Identity, "r", 1, 1,
            CancellationToken.None, sharedBudget: budget);
        budget.LiveBytes.ShouldBeGreaterThan(original.Length);
        budget.LiveBytes.ShouldBeLessThan(original.Length + 512 * 1024);
        page.Events[0].Resolved.Payload.Length.ShouldBe(original.Length);
        page.Dispose();
        budget.LiveBytes.ShouldBe(0);
        original[0].ShouldBe((byte)17);
        original[^1].ShouldBe((byte)91);
    }

    /// <summary>Checks actual provider ownership replaces its conservative reservation before resolver allocation.</summary>
    [Fact]
    public async Task DistinctProviderOutputReleasesProvenUnusedReservationBeforeResolution()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        byte[] original = new byte[22 * 1024 * 1024];
        original[0] = 17;
        byte[] providerOutput = original.ToArray();
        EventEnvelope stored = CreateEvent() with { Payload = original };
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(1, DateTimeOffset.UnixEpoch, "etag")));
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, stored));
        _ = protection.TryUnprotectEventPayloadAsync(Identity, Arg.Any<string>(), Arg.Any<byte[]>(), "json",
            Arg.Any<EventStorePayloadProtectionMetadata>(), Arg.Any<CancellationToken>())
            .Returns(PayloadUnprotectionOutcome.Readable(providerOutput, "json", EventStorePayloadProtectionMetadata.Unprotected()));
        var budget = new EventBufferBudget();
        var reader = CreateReader(registry, stateManager, protection);

        using DaprLogicalEventPage page = await reader.ReadPageAsync(Identity, "r", 1, 1,
            CancellationToken.None, sharedBudget: budget);
        budget.LiveBytes.ShouldBeGreaterThan(original.Length);
        budget.LiveBytes.ShouldBeLessThan(original.Length + 512 * 1024);
        providerOutput.All(static value => value == 0).ShouldBeTrue();
        original[0].ShouldBe((byte)17);
        page.Dispose();
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Checks fixed-field and extension metadata ceilings refuse before protection and release snapshot admission.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OversizedMetadataRefusesBeforeProviderAndReleasesSnapshotCapacity(bool oversizedExtension)
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        EventEnvelope source = oversizedExtension
            ? CreateEvent() with { Extensions = new Dictionary<string, string> { ["application-note"] = new string('x', 90_000) } }
            : CreateEvent() with { MessageId = new string('x', 90_000) };
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(1, DateTimeOffset.UnixEpoch, "etag")));
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, source));
        var budget = new EventBufferBudget();
        var reader = CreateReader(registry, stateManager, protection);

        (await Should.ThrowAsync<InvalidOperationException>(() => reader.ReadPageAsync(Identity, "r", 1, 1,
            CancellationToken.None, sharedBudget: budget))).Message.ShouldContain("MetadataLimit");
        budget.LiveBytes.ShouldBe(0);
        protection.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Checks a partial private extension snapshot failure releases admission before any provider callback.</summary>
    [Fact]
    public async Task ExtensionSnapshotEnumerationFailureReleasesCapacityBeforeProviderInvocation()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        var budget = new EventBufferBudget();
        var expected = new InvalidOperationException("Injected extension snapshot failure.");
        var extensions = new ThrowingSnapshotExtensions(budget, expected);
        EventEnvelope source = CreateEvent() with { Extensions = extensions };
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(1, DateTimeOffset.UnixEpoch, "etag")));
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, source));
        var reader = CreateReader(registry, stateManager, protection);

        InvalidOperationException actual = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadPageAsync(Identity, "r", 1, 1, CancellationToken.None, sharedBudget: budget));

        actual.ShouldBeSameAs(expected);
        extensions.EnumeratorCalls.ShouldBe(1);
        extensions.YieldedEntries.ShouldBe(1);
        extensions.LiveBytesAtFailure.ShouldBe(512 * 1024);
        budget.LiveBytes.ShouldBe(0);
        protection.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Checks a protection callback cannot substitute extension values in the private source snapshot.</summary>
    [Fact]
    public async Task CallbackMutationOfCallerExtensionsCannotChangeAdmittedMetadataSnapshot()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        var extensions = new Dictionary<string, string> { ["application-note"] = "original" };
        EventEnvelope source = CreateEvent() with { Extensions = extensions };
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(1, DateTimeOffset.UnixEpoch, "etag")));
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, source));
        _ = protection.TryUnprotectEventPayloadAsync(Identity, Arg.Any<string>(), Arg.Any<byte[]>(), "json",
            Arg.Any<EventStorePayloadProtectionMetadata>(), Arg.Any<CancellationToken>()).Returns(_ =>
            {
                extensions["application-note"] = "mutated";
                return PayloadUnprotectionOutcome.Readable([1, 2], "json", EventStorePayloadProtectionMetadata.Unprotected());
            });
        var budget = new EventBufferBudget();
        var reader = CreateReader(registry, stateManager, protection);

        using DaprLogicalEventPage page = await reader.ReadPageAsync(Identity, "r", 1, 1,
            CancellationToken.None, sharedBudget: budget);
        page.Events[0].Source.Extensions!["application-note"].ShouldBe("original");
        extensions["application-note"].ShouldBe("mutated");
        Should.Throw<NotSupportedException>(() => page.Events[0].Source.Extensions!["application-note"] = "substituted");
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
    public async Task ShrinkingUpcastsCannotBypassReadableSourcePageLimit()
    {
        using EventDomainRegistry registry = CreateRegistry(upcasting: true);
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        var upcaster = new ShrinkingLogicalEventUpcaster();
        var budget = new EventBufferBudget();
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(2, DateTimeOffset.UnixEpoch, null)));
        for (int sequence = 1; sequence <= 2; sequence++)
        {
            _ = stateManager.TryGetStateAsync<EventEnvelope>(
                $"{Identity.EventStreamKeyPrefix}{sequence}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, CreateEvent() with { SequenceNumber = sequence }));
        }
        var executor = new EventUpcastChainExecutor(registry,
            new Dictionary<(string, int), RegisteredEventUpcaster>
            {
                [("evt", 1)] = new RegisteredEventUpcaster("test-upcaster", upcaster, new byte[32]),
            }, static (_, _, _, _, _, _) => { });
        var reader = new DaprLogicalEventReader(stateManager, new NoOpEventPayloadProtectionService(),
            new EventLogicalViewResolver(registry, executor), maximumReadablePageBytes: 3);

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadPageAsync(Identity, "r", 1, 2, CancellationToken.None, sharedBudget: budget)).ConfigureAwait(true);

        error.Message.ShouldContain("ReadableLimit");
        upcaster.Calls.ShouldBe(0);
        budget.LiveBytes.ShouldBe(0);
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

    /// <summary>Verifies changed logical bytes, metadata and absent V2 digests refuse resolution.</summary>
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
            .Returns(_ => Task.FromResult(PayloadUnprotectionOutcome.Readable([1, 2], "json",
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

    /// <summary>Checks every later-source refusal precedes the first schema validator or upcaster callback.</summary>
    [Theory]
    [InlineData("missing", "Missing")]
    [InlineData("address", "AddressMismatch")]
    [InlineData("alias", "UnknownEventContract")]
    [InlineData("format", "UnknownEventContract")]
    [InlineData("digest", "LogicalDigestMismatch")]
    [InlineData("head", "SourceHeadChanged")]
    [InlineData("floor", "SourceHeadChanged")]
    [InlineData("etag", "SourceHeadChanged")]
    public async Task EntireSourcePageIsAdmittedBeforeFirstCatalogCallback(string failure, string reason)
    {
        using EventDomainRegistry registry = CreateRegistry(upcasting: true);
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        var upcaster = new ShrinkingLogicalEventUpcaster();
        int validators = 0;
        var executor = new EventUpcastChainExecutor(registry,
            new Dictionary<(string, int), RegisteredEventUpcaster>
            {
                [("evt", 1)] = new RegisteredEventUpcaster("test-upcaster", upcaster, new byte[32]),
            }, (_, _, _, _, _, _) => validators++);
        var reader = new DaprLogicalEventReader(stateManager, new NoOpEventPayloadProtectionService(),
            new EventLogicalViewResolver(registry, executor));
        var initial = new AggregateMetadata(2, DateTimeOffset.UnixEpoch, "etag");
        AggregateMetadata subsequent = failure switch
        {
            "head" => initial with { CurrentSequence = 3 },
            "floor" => initial with { RetainedFloor = 2 },
            "etag" => initial with { ETag = "changed" },
            _ => initial,
        };
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, initial), new ConditionalValue<AggregateMetadata>(true, subsequent));
        EventEnvelope first = CreateEvent();
        EventEnvelope second = first with { SequenceNumber = 2, Payload = [1, 2] };
        second = failure switch
        {
            "address" => second with { SequenceNumber = 3 },
            "alias" => second with { EventTypeName = "Unlisted.Event" },
            "format" => second with { SerializationFormat = "unregistered" },
            "digest" => second with { ApplicationPayloadDigest = new string('0', 64) },
            _ => second,
        };
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, first));
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{Identity.EventStreamKeyPrefix}2", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(failure != "missing", second));
        var budget = new EventBufferBudget();

        Exception error = await Should.ThrowAsync<Exception>(() => reader.ReadPageAsync(
            Identity, "r", 1, 2, CancellationToken.None, sharedBudget: budget));

        error.Message.ShouldContain(reason);
        validators.ShouldBe(0);
        upcaster.Calls.ShouldBe(0);
        budget.LiveBytes.ShouldBe(0);
        first.Payload.ShouldBe([1, 2]);
        second.Payload.ShouldBe([1, 2]);
        _ = stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Checks later unreadability clears prepared/provider owners before any schema validation.</summary>
    [Fact]
    public async Task LaterUnreadableSourceReleasesWholePreparedPageBeforeCatalogCode()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(2, DateTimeOffset.UnixEpoch, "etag")));
        for (int sequence = 1; sequence <= 2; sequence++)
        {
            _ = stateManager.TryGetStateAsync<EventEnvelope>($"{Identity.EventStreamKeyPrefix}{sequence}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, CreateEvent() with { SequenceNumber = sequence }));
        }
        byte[] providerOutput = [1, 2];
        int providerCalls = 0;
        _ = protection.TryUnprotectEventPayloadAsync(Identity, Arg.Any<string>(), Arg.Any<byte[]>(), "json",
            Arg.Any<EventStorePayloadProtectionMetadata>(), Arg.Any<CancellationToken>()).Returns(_ => ++providerCalls == 1
                ? PayloadUnprotectionOutcome.Readable(providerOutput, "json", EventStorePayloadProtectionMetadata.Unprotected())
                : PayloadUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable));
        int validators = 0;
        var executor = new EventUpcastChainExecutor(registry, new Dictionary<(string, int), RegisteredEventUpcaster>(),
            (_, _, _, _, _, _) => validators++);
        var reader = new DaprLogicalEventReader(stateManager, protection, new EventLogicalViewResolver(registry, executor));
        var budget = new EventBufferBudget();

        await Should.ThrowAsync<ProtectedDataUnreadableException>(() => reader.ReadPageAsync(
            Identity, "r", 1, 2, CancellationToken.None, sharedBudget: budget));

        validators.ShouldBe(0);
        providerCalls.ShouldBe(2);
        providerOutput.ShouldBe([0, 0]);
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Checks the final head, floor, and ETag readback refuses after catalog callbacks and clears all owners.</summary>
    [Theory]
    [InlineData("head")]
    [InlineData("floor")]
    [InlineData("etag")]
    public async Task FinalMetadataChangeAfterCatalogCallbacksRefusesWholePage(string change)
    {
        using EventDomainRegistry registry = CreateRegistry(upcasting: true);
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        var initial = new AggregateMetadata(1, DateTimeOffset.UnixEpoch, "etag");
        AggregateMetadata final = change switch
        {
            "head" => initial with { CurrentSequence = 2 },
            "floor" => initial with { RetainedFloor = 2 },
            _ => initial with { ETag = "changed" },
        };
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, initial),
                new ConditionalValue<AggregateMetadata>(true, initial),
                new ConditionalValue<AggregateMetadata>(true, final));
        EventEnvelope stored = CreateEvent();
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, stored));
        var upcaster = new ShrinkingLogicalEventUpcaster();
        int validators = 0;
        var executor = new EventUpcastChainExecutor(registry,
            new Dictionary<(string, int), RegisteredEventUpcaster>
            {
                [("evt", 1)] = new RegisteredEventUpcaster("test-upcaster", upcaster, new byte[32]),
            }, (_, _, _, _, _, _) => validators++);
        var reader = new DaprLogicalEventReader(stateManager, new NoOpEventPayloadProtectionService(),
            new EventLogicalViewResolver(registry, executor));
        var budget = new EventBufferBudget();

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() => reader.ReadPageAsync(
            Identity, "r", 1, 1, CancellationToken.None, sharedBudget: budget));

        error.Message.ShouldContain("SourceHeadChanged");
        validators.ShouldBe(2);
        upcaster.Calls.ShouldBe(1);
        stored.Payload.ShouldBe([1, 2]);
        budget.LiveBytes.ShouldBe(0);
        _ = await stateManager.Received(3).TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey,
            Arg.Any<CancellationToken>()).ConfigureAwait(true);
        _ = stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Checks a later catalog callback cannot silently alter an earlier stored source array.</summary>
    [Fact]
    public async Task LaterCallbackChangingEarlierStoredSourceRefusesWholePage()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        EventEnvelope first = CreateEvent();
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(2, DateTimeOffset.UnixEpoch, "etag")));
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{Identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, first));
        _ = stateManager.TryGetStateAsync<EventEnvelope>($"{Identity.EventStreamKeyPrefix}2", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true, CreateEvent() with { SequenceNumber = 2 }));
        int calls = 0;
        var executor = new EventUpcastChainExecutor(registry, new Dictionary<(string, int), RegisteredEventUpcaster>(),
            (_, _, _, _, _, _) =>
            {
                if (++calls == 2) { first.Payload[0] = 9; }
            });
        var reader = new DaprLogicalEventReader(stateManager, new NoOpEventPayloadProtectionService(),
            new EventLogicalViewResolver(registry, executor));
        var budget = new EventBufferBudget();

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() => reader.ReadPageAsync(
            Identity, "r", 1, 2, CancellationToken.None, sharedBudget: budget));

        error.Message.ShouldContain("AddressMismatch");
        calls.ShouldBe(2);
        budget.LiveBytes.ShouldBe(0);
        _ = stateManager.DidNotReceive().SaveStateAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Checks maximum-count tiny pages retain measured metadata and source ownership within one budget.</summary>
    [Fact]
    public async Task MaximumCountPageFitsMeasuredMetadataAndClearsAllOwners()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(256, DateTimeOffset.UnixEpoch, "etag")));
        for (int sequence = 1; sequence <= 256; sequence++)
        {
            _ = stateManager.TryGetStateAsync<EventEnvelope>($"{Identity.EventStreamKeyPrefix}{sequence}", Arg.Any<CancellationToken>())
                .Returns(new ConditionalValue<EventEnvelope>(true, CreateEvent() with { SequenceNumber = sequence }));
        }
        var reader = CreateReader(registry, stateManager, new NoOpEventPayloadProtectionService());
        var budget = new EventBufferBudget();

        using DaprLogicalEventPage page = await reader.ReadPageAsync(Identity, "r", 1, 256,
            CancellationToken.None, sharedBudget: budget);

        page.Events.Count.ShouldBe(256);
        page.Events.Select(static value => value.SequenceNumber).ShouldBe(Enumerable.Range(1, 256).Select(static value => (long)value));
        budget.LiveBytes.ShouldBeGreaterThan(512);
        budget.LiveBytes.ShouldBeLessThan(1024 * 1024);
        page.Dispose();
        budget.LiveBytes.ShouldBe(0);
    }

    /// <summary>Checks an observation during count-zero logical readback prevents an empty success from escaping.</summary>
    [Fact]
    public async Task EmptyPageRefusesObservedLossAtFinalReadbackBoundary()
    {
        using EventDomainRegistry registry = CreateRegistry();
        IActorStateManager stateManager = Substitute.For<IActorStateManager>();
        int reads = 0;
        _ = stateManager.TryGetStateAsync<AggregateMetadata>(Identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (++reads == 2) { registry.CapabilityLoss.ObserveViolation(); }
                return new ConditionalValue<AggregateMetadata>(true, new AggregateMetadata(0, DateTimeOffset.UnixEpoch, "etag"));
            });
        var reader = CreateReader(registry, stateManager, new NoOpEventPayloadProtectionService());
        var budget = new EventBufferBudget();

        InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() => reader.ReadPageAsync(
            Identity, "r", 1, 1, CancellationToken.None, sharedBudget: budget));

        error.Message.ShouldContain("CapabilityMismatch");
        reads.ShouldBe(2);
        budget.LiveBytes.ShouldBe(0);
        _ = stateManager.DidNotReceiveWithAnyArgs().TryGetStateAsync<EventEnvelope>(default!, default);
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

    private static EventDomainRegistry CreateRegistry(bool upcasting = false)
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
        if (upcasting)
        {
            byte[] descriptor = Convert.FromHexString(fixture["DescriptorRow"]);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(descriptor.AsSpan(22, 4), 2);
            byte[] firstVersion = Convert.FromHexString(fixture["VersionRow"]);
            byte[] secondVersion = firstVersion.ToArray();
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(secondVersion.AsSpan(13, 4), 2);
            using var edge = new EventEvolutionBinaryWriter(1024);
            edge.WriteByte(0x45); edge.WriteString("d"); edge.WriteString("evt"); edge.WriteInt32(1); edge.WriteUInt16(12);
            edge.WriteByte(1); edge.WriteInt32(2);
            edge.WriteByte(2); edge.WriteString("json");
            edge.WriteByte(3); edge.WriteString("json");
            edge.WriteByte(4); edge.WriteString("serializer");
            edge.WriteByte(5); edge.WriteHash(new byte[32]);
            edge.WriteByte(6); edge.WriteHash(new byte[32]);
            edge.WriteByte(7); edge.WriteString("test-upcaster");
            edge.WriteByte(8);
            using (FileStream assembly = File.OpenRead(typeof(ShrinkingLogicalEventUpcaster).Assembly.Location))
            {
                edge.WriteHash(SHA256.HashData(assembly));
            }
            edge.WriteByte(9); edge.WriteHash(new byte[32]);
            edge.WriteByte(10); edge.WriteString("no-payload-identity");
            edge.WriteByte(11); edge.WriteHash(new byte[32]);
            edge.WriteByte(12); edge.WriteHash(new byte[32]);
            return new EventDomainRegistry("d", [Convert.FromHexString(fixture["AliasRow"]), descriptor,
                firstVersion, secondVersion, edge.CopyEncodedBytes(), Convert.FromHexString(fixture["SharedRow"])],
                capabilityLoss: new EventEvolutionCapabilityLoss());
        }

        return new EventDomainRegistry("d", [
            Convert.FromHexString(fixture["AliasRow"]),
            Convert.FromHexString(fixture["DescriptorRow"]),
            Convert.FromHexString(fixture["VersionRow"]),
            Convert.FromHexString(fixture["SharedRow"]),
        ], capabilityLoss: new EventEvolutionCapabilityLoss());
    }
}
