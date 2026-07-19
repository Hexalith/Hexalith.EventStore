using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class ProjectionEventWireBuilderTests {
    private static readonly AggregateIdentity s_identity = new("tenant-a", "orders", "order-42");

    [Fact]
    public async Task BuildAsync_PreservesExactGappedPersistedGlobalPositionsAcrossReplay() {
        EventEnvelope[] persisted = [
            CreateEnvelope(sequenceNumber: 1, globalPosition: 101),
            CreateEnvelope(sequenceNumber: 2, globalPosition: 104),
            CreateEnvelope(sequenceNumber: 3, globalPosition: 109),
        ];
        var protection = new NoOpEventPayloadProtectionService();

        ProjectionEventReadabilityResult firstBuild = await ProjectionEventWireBuilder
            .BuildAsync(protection, s_identity, persisted, CancellationToken.None)
            .ConfigureAwait(true);
        ProjectionEventReadabilityResult replayBuild = await ProjectionEventWireBuilder
            .BuildAsync(protection, s_identity, persisted, CancellationToken.None)
            .ConfigureAwait(true);

        ProjectionEventDto[] firstEvents = firstBuild.Events.ShouldNotBeNull();
        ProjectionEventDto[] replayEvents = replayBuild.Events.ShouldNotBeNull();
        firstEvents.Select(static value => value.GlobalPosition).ShouldBe([101L, 104L, 109L]);
        replayEvents.Select(static value => value.GlobalPosition).ShouldBe([101L, 104L, 109L]);
        firstEvents.Max(static value => value.GlobalPosition).ShouldBe(109L);
        replayEvents.ShouldBe(firstEvents);
    }

    private static EventEnvelope CreateEnvelope(long sequenceNumber, long globalPosition) => new(
        MessageId: $"message-{sequenceNumber}",
        AggregateId: s_identity.AggregateId,
        AggregateType: "Order",
        TenantId: s_identity.TenantId,
        Domain: s_identity.Domain,
        SequenceNumber: sequenceNumber,
        GlobalPosition: globalPosition,
        Timestamp: DateTimeOffset.UnixEpoch.AddMinutes(sequenceNumber),
        CorrelationId: "correlation-1",
        CausationId: "causation-1",
        UserId: "user-1",
        DomainServiceVersion: "v1",
        EventTypeName: "OrderChanged",
        MetadataVersion: 1,
        SerializationFormat: "json",
        Payload: [(byte)sequenceNumber],
        Extensions: null);
}
