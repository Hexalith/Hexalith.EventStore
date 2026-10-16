using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class EventStoreProjectionDeliveryHistoryReaderTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompactGuardAdmission_PreservesExactSourceRoutePrefixAndLegacyIdentityBytes(bool positionSensitive)
    {
        const string route = "order-detail";
        const int count = 257;
        string initial = ProjectionDeliveryFingerprint.ComputeInitial(Identity, route, positionSensitive);
        string prefix = initial;
        var compact = new ProjectionSourceAdmittedDigests(9000, 9000 + count, positionSensitive, initial);
        var expected = new List<ProjectionDeliveryEventDigest>();
        var admitted = new List<ProjectionSourceAdmittedEvent>();
        for (int offset = 0; offset < count; offset++)
        {
            long sequence = 9001 + offset;
            EventEnvelope stored = Envelope(offset + 1, null) with { SequenceNumber = sequence,
                MessageId = $"00000000-0000-0000-0000-{offset:000000000000}" };
            var item = new Hexalith.EventStore.Contracts.Projections.ProjectionEventDto(stored.EventTypeName, stored.Payload,
                stored.SerializationFormat, sequence, stored.Timestamp, stored.CorrelationId, stored.MessageId, stored.UserId, stored.GlobalPosition);
            string source = SourceProofCodec.ComputeEventHash(stored);
            string fingerprint = ProjectionDeliveryFingerprint.ComputeEvent(item, positionSensitive);
            prefix = ProjectionDeliveryFingerprint.Extend(prefix, fingerprint);
            compact.Add(sequence, stored.MessageId, source, fingerprint, prefix);
            expected.Add(new(sequence, stored.MessageId, fingerprint, prefix));
            admitted.Add(new(sequence, stored.MessageId, source, fingerprint));
        }
        compact.Seal();
        compact.RouteEvents.ShouldBe(expected);
        compact.SourceEvents.ShouldBe(admitted);
        ((ProjectionSourceAdmittedDigestList<ProjectionDeliveryEventDigest>)compact.RouteEvents).Table.ShouldBeSameAs(
            ((ProjectionSourceAdmittedDigestList<ProjectionSourceAdmittedEvent>)compact.SourceEvents).Table);
        for (int index = 0; index < count; index++)
        {
            ProjectionSourceAdmittedEvent item = admitted[index];
            compact.Matches(item.Sequence, item.MessageId, item.SourceEventDigest, item.RouteEventFingerprint,
                expected[index].PrefixFingerprint).ShouldBeTrue();
            compact.Matches(item.Sequence, item.MessageId + "changed", item.SourceEventDigest, item.RouteEventFingerprint,
                expected[index].PrefixFingerprint).ShouldBeFalse();
            compact.Matches(item.Sequence, item.MessageId, new string('0', 64), item.RouteEventFingerprint,
                expected[index].PrefixFingerprint).ShouldBeFalse();
        }
        Should.Throw<InvalidOperationException>(() => compact.Add(9258, "late", new string('0', 64), prefix, prefix));
        compact.RouteEvents[^1].PrefixFingerprint.ShouldBe(prefix);
    }

    [Fact]
    public async Task Readability_LateProviderFailureChargesEarlierDecodedWorkAndTheFailedAttemptWithoutVisitingLaterEvents()
    {
        var metadata = new EventStorePayloadProtectionMetadata(PayloadProtectionState.Protected, 1, "test", null, null, null);
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        protection.TryUnprotectEventPayloadAsync(Identity, "OrderChanged", Arg.Any<byte[]>(), "protected+json",
            Arg.Any<EventStorePayloadProtectionMetadata?>(), Arg.Any<CancellationToken>()).Returns(call =>
                call.ArgAt<byte[]>(2)[1] == 1 ? PayloadUnprotectionOutcome.Readable([1, 2, 3], "json", metadata)
                    : PayloadUnprotectionOutcome.Unreadable(UnreadableProtectedDataReason.ProviderUnavailable, metadata));
        using Hexalith.EventStore.Client.Projections.ProjectionDeliveryCostScope cost = Hexalith.EventStore.Client.Projections.ProjectionDeliveryCostDiagnostics.BeginScope();
        ProjectionEventReadabilityResult result = await ProjectionEventWireBuilder.BuildAsync(protection, Identity,
            [Envelope(1, metadata), Envelope(2, metadata), Envelope(3, metadata)], CancellationToken.None);
        result.Events.ShouldBeNull();
        result.SequenceNumber.ShouldBe(2);
        result.UnreadableReason.ShouldBe(UnreadableProtectedDataReason.ProviderUnavailable);
        cost[Hexalith.EventStore.Client.Projections.ProjectionDeliveryCost.WireReadabilityAttempts].ShouldBe(2);
        cost[Hexalith.EventStore.Client.Projections.ProjectionDeliveryCost.WireReadabilityInputBytes].ShouldBe(4);
        cost[Hexalith.EventStore.Client.Projections.ProjectionDeliveryCost.WireUnprotectionAttempts].ShouldBe(2);
        cost[Hexalith.EventStore.Client.Projections.ProjectionDeliveryCost.WireDecodedEvents].ShouldBe(1);
        cost[Hexalith.EventStore.Client.Projections.ProjectionDeliveryCost.WireDecodedPayloadBytes].ShouldBe(3);
        cost[Hexalith.EventStore.Client.Projections.ProjectionDeliveryCost.HandlerAttempts].ShouldBe(0);
        _ = await protection.ReceivedWithAnyArgs(2).TryUnprotectEventPayloadAsync(default!, default!, default!, default!, default, default);
    }

    private static readonly AggregateIdentity Identity = new("tenant-a", "sales", "order-42");

    [Fact]
    public async Task ReadAsync_PagesExactPrefixAndDecryptsProtectedPayloads() {
        var metadata = new EventStorePayloadProtectionMetadata(
            PayloadProtectionState.Protected,
            EventStorePayloadProtectionMetadata.CurrentMetadataVersion,
            "test",
            null,
            null,
            null);
        IAggregateActor aggregate = Substitute.For<IAggregateActor>();
        _ = aggregate.ReadEventsRangeAsync(Arg.Any<long>(), 300, 1)
            .Returns(call => [Envelope(checked((int)call.ArgAt<long>(0) + 1), metadata)]);
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        _ = protection.TryUnprotectEventPayloadAsync(
                Identity,
                "OrderChanged",
                Arg.Any<byte[]>(),
                "protected+json",
                Arg.Is<EventStorePayloadProtectionMetadata?>(value => value == metadata),
                Arg.Any<CancellationToken>())
            .Returns(call => PayloadUnprotectionOutcome.Readable(
                [.. call.ArgAt<byte[]>(2).Reverse()],
                "json",
                metadata));
        EventStoreProjectionDeliveryHistoryReader reader = CreateReader(aggregate, protection);

        IReadOnlyList<Hexalith.EventStore.Contracts.Projections.ProjectionEventDto> result = await reader.ReadAsync(
            Identity,
            300);

        result.Count.ShouldBe(300);
        result[0].Payload.ShouldBe([(byte)1, (byte)255]);
        result[^1].Payload.ShouldBe([(byte)49, (byte)255]);
        result[^1].SequenceNumber.ShouldBe(300);
        _ = await aggregate.Received(300).ReadEventsRangeAsync(Arg.Any<long>(), 300, 1);
        _ = await aggregate.Received(1).ReadEventsRangeAsync(0, 300, 1);
        _ = await aggregate.Received(1).ReadEventsRangeAsync(299, 300, 1);
        _ = await protection.Received(300).TryUnprotectEventPayloadAsync(
            Identity,
            "OrderChanged",
            Arg.Any<byte[]>(),
            "protected+json",
            Arg.Is<EventStorePayloadProtectionMetadata?>(value => value == metadata),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadAsync_CancellationAfterPage_PreventsNextActorCall() {
        using var cancellation = new CancellationTokenSource();
        IAggregateActor aggregate = Substitute.For<IAggregateActor>();
        _ = aggregate.ReadEventsRangeAsync(0, 300, 1)
            .Returns(_ => {
                cancellation.Cancel();
                return [Envelope(1, null)];
            });
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        EventStoreProjectionDeliveryHistoryReader reader = CreateReader(aggregate, protection);

        _ = await Should.ThrowAsync<OperationCanceledException>(() => reader.ReadAsync(
            Identity,
            300,
            cancellation.Token));

        _ = await aggregate.Received(1).ReadEventsRangeAsync(0, 300, 1);
        _ = await aggregate.DidNotReceive().ReadEventsRangeAsync(1, 300, 1);
        _ = await protection.DidNotReceiveWithAnyArgs().TryUnprotectEventPayloadAsync(
            default!,
            default!,
            default!,
            default!,
            default,
            default);
    }

    [Fact]
    public async Task ReadAsync_ProviderUnavailable_IsTransientRatherThanValidationFailure() {
        var metadata = new EventStorePayloadProtectionMetadata(
            PayloadProtectionState.Protected,
            EventStorePayloadProtectionMetadata.CurrentMetadataVersion,
            "test",
            null,
            null,
            null);
        IAggregateActor aggregate = Substitute.For<IAggregateActor>();
        _ = aggregate.ReadEventsRangeAsync(0, 1, 1).Returns([Envelope(1, metadata)]);
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        _ = protection.TryUnprotectEventPayloadAsync(
                Identity,
                "OrderChanged",
                Arg.Any<byte[]>(),
                "protected+json",
                Arg.Any<EventStorePayloadProtectionMetadata?>(),
                Arg.Any<CancellationToken>())
            .Returns(PayloadUnprotectionOutcome.Unreadable(
                UnreadableProtectedDataReason.ProviderUnavailable,
                metadata));
        EventStoreProjectionDeliveryHistoryReader reader = CreateReader(aggregate, protection);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            reader.ReadAsync(Identity, 1));

        exception.ShouldNotBeOfType<ProjectionDeliveryHistoryValidationException>();
    }

    [Fact]
    public async Task ReadAsync_ForeignEnvelopeFailsBeforePayloadDecoding() {
        IAggregateActor aggregate = Substitute.For<IAggregateActor>();
        _ = aggregate.ReadEventsRangeAsync(0, 1, 1)
            .Returns([Envelope(1, null) with { TenantId = "other-tenant" }]);
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        EventStoreProjectionDeliveryHistoryReader reader = CreateReader(aggregate, protection);

        _ = await Should.ThrowAsync<ProjectionDeliveryHistoryValidationException>(() => reader.ReadAsync(Identity, 1));
        _ = await protection.DidNotReceiveWithAnyArgs().TryUnprotectEventPayloadAsync(
            default!, default!, default!, default!, default, default);
    }

    [Fact]
    public async Task ReadAsync_IncompleteSecondPageFailsClosed() {
        IAggregateActor aggregate = Substitute.For<IAggregateActor>();
        _ = aggregate.ReadEventsRangeAsync(0, 257, 1).Returns([Envelope(1, null)]);
        _ = aggregate.ReadEventsRangeAsync(1, 257, 1).Returns([]);
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        EventStoreProjectionDeliveryHistoryReader reader = CreateReader(aggregate, protection);

        _ = await Should.ThrowAsync<ProjectionDeliveryHistoryValidationException>(() => reader.ReadAsync(Identity, 257));
    }

    [Fact]
    public async Task ReadPagesFromAsync_StartsAtAcceptedCheckpointWithoutReadingPrefix() {
        IAggregateActor aggregate = Substitute.For<IAggregateActor>();
        _ = aggregate.ReadEventsRangeAsync(Arg.Any<long>(), 10_000, 1)
            .Returns(call => [Envelope(checked((int)call.ArgAt<long>(0) + 1), null)]);
        EventStoreProjectionDeliveryHistoryReader reader = CreateReader(
            aggregate, Substitute.For<IEventPayloadProtectionService>());
        var observed = new List<long>();

        await foreach (Hexalith.EventStore.Contracts.Projections.ProjectionEventDto[] page
            in reader.ReadPagesFromAsync(Identity, 9_989, 10_000)) {
            observed.AddRange(page.Select(static item => item.SequenceNumber));
        }

        observed.ShouldBe(Enumerable.Range(9_990, 11).Select(static item => (long)item));
        _ = await aggregate.Received(11).ReadEventsRangeAsync(Arg.Any<long>(), 10_000, 1);
        _ = await aggregate.DidNotReceive().ReadEventsRangeAsync(0, 10_000, 1);
        _ = await aggregate.Received(1).ReadEventsRangeAsync(9_989, 10_000, 1);
    }

    private static EventStoreProjectionDeliveryHistoryReader CreateReader(
        IAggregateActor aggregate,
        IEventPayloadProtectionService protection) {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IAggregateActor>(
                Arg.Is<ActorId>(actorId => actorId.ToString() == Identity.ActorId),
                nameof(AggregateActor))
            .Returns(aggregate);
        return new EventStoreProjectionDeliveryHistoryReader(
            factory,
            protection,
            Options.Create(new EventStoreActorOptions()));
    }

    private static EventEnvelope Envelope(
        int sequence,
        EventStorePayloadProtectionMetadata? metadata) => new(
            $"01J{sequence:D23}",
            Identity.AggregateId,
            "Order",
            Identity.TenantId,
            Identity.Domain,
            sequence,
            sequence,
            DateTimeOffset.UnixEpoch.AddMinutes(sequence),
            "correlation-1",
            "causation-1",
            "user-1",
            "v1",
            "OrderChanged",
            1,
            metadata is null ? "json" : "protected+json",
            [(byte)255, (byte)(sequence % 251)],
            metadata is null
                ? null
                : EventStorePayloadProtectionMetadataCarrier.Write((IDictionary<string, string>?)null, metadata));
}
