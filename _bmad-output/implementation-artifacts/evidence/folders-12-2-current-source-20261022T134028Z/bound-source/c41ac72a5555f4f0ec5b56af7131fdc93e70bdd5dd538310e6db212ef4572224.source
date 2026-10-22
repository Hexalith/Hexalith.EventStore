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

    [Fact]
    public async Task BuildAsync_VersionedSource_RefusesLegacyProjectionWireBeforeHandler() {
        EventEnvelope versioned = CreateEnvelope(1, 101) with {
            EventTypeName = "order-changed",
            MetadataVersion = 2,
            EventContractType = "order-changed",
            PayloadVersion = 2,
        };

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            ProjectionEventWireBuilder.BuildAsync(
                new NoOpEventPayloadProtectionService(), s_identity, [versioned], CancellationToken.None));

        exception.Message.ShouldContain("RollbackReaderCapabilityHold");
    }

    [Fact]
    public async Task BuildAsync_MetadataV1CarriesStoredPayloadVersion()
    {
        EventEnvelope stored = CreateEnvelope(1, 101) with { PayloadVersion = 2 };

        ProjectionEventReadabilityResult result = await ProjectionEventWireBuilder.BuildAsync(
            new NoOpEventPayloadProtectionService(), s_identity, [stored], CancellationToken.None);

        ProjectionEventDto projected = result.Events.ShouldNotBeNull().ShouldHaveSingleItem();
        projected.MetadataVersion.ShouldBe(1);
        projected.StoredPayloadVersion.ShouldBe(2);
        projected.Payload.ShouldBe(stored.Payload);
    }

    [Fact]
    public async Task BuildAsync_ChangedLogicalPayloadRefusesProjectionWire() {
        EventEnvelope original = CreateEnvelope(1, 101);
        original = original with {
            ApplicationPayloadDigest = EventLogicalDigest.Compute(original, "json",
                EventLogicalDigest.HashPayload(original.Payload)),
        };

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            ProjectionEventWireBuilder.BuildAsync(
                new NoOpEventPayloadProtectionService(), s_identity,
                [original with { Payload = [9] }], CancellationToken.None));

        exception.Message.ShouldContain("LogicalDigestMismatch");
    }

    [Fact]
    public async Task BuildAsync_LaterForeignEventRefusesWholeBatch() {
        EventEnvelope first = CreateEnvelope(1, 101);
        EventEnvelope foreign = CreateEnvelope(2, 104) with { TenantId = "tenant-b" };

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            ProjectionEventWireBuilder.BuildAsync(
                new NoOpEventPayloadProtectionService(), s_identity,
                [first, foreign], CancellationToken.None));

        exception.Message.ShouldContain("AddressMismatch");
    }

    [Fact]
    public async Task BuildAsync_LaterSequenceGapRefusesWholeBatch() {
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            ProjectionEventWireBuilder.BuildAsync(
                new NoOpEventPayloadProtectionService(), s_identity,
                [CreateEnvelope(1, 101), CreateEnvelope(3, 103)], CancellationToken.None));

        exception.Message.ShouldContain("AddressMismatch");
    }

    private static EventEnvelope CreateEnvelope(long sequenceNumber, long globalPosition) => new(
        MessageId: "01ARZ3NDEKTSV4RRFFQ69G5FAV",
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
