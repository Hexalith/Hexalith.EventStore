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
        _ = aggregate.ReadEventsRangeAsync(0, 300, 256)
            .Returns([.. Enumerable.Range(1, 256).Select(sequence => Envelope(sequence, metadata))]);
        _ = aggregate.ReadEventsRangeAsync(256, 300, 256)
            .Returns([.. Enumerable.Range(257, 44).Select(sequence => Envelope(sequence, metadata))]);
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
        _ = await aggregate.Received(1).ReadEventsRangeAsync(0, 300, 256);
        _ = await aggregate.Received(1).ReadEventsRangeAsync(256, 300, 256);
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
        _ = aggregate.ReadEventsRangeAsync(0, 300, 256)
            .Returns(_ => {
                cancellation.Cancel();
                return [.. Enumerable.Range(1, 256).Select(sequence => Envelope(sequence, null))];
            });
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        EventStoreProjectionDeliveryHistoryReader reader = CreateReader(aggregate, protection);

        _ = await Should.ThrowAsync<OperationCanceledException>(() => reader.ReadAsync(
            Identity,
            300,
            cancellation.Token));

        _ = await aggregate.Received(1).ReadEventsRangeAsync(0, 300, 256);
        _ = await aggregate.DidNotReceive().ReadEventsRangeAsync(256, 300, 256);
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
        _ = aggregate.ReadEventsRangeAsync(0, 1, 256).Returns([Envelope(1, metadata)]);
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
            $"message-{sequence}",
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
