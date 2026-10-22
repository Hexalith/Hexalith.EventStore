using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Builders;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

public sealed partial class NamedProjectionDispatchLiveSidecarTests
{
    [Theory]
    [InlineData(256)]
    [InlineData(257)]
    public async Task SourceProof_RealActorAtomicAppendAndBoundedPagesRetainExactStoredChainAcrossProxyRecreation(int count)
    {
        fixture.ThrowIfHostStopped();
        fixture.ResetTestState();
        fixture.SetupCounterDomain();
        fixture.DomainServiceInvoker.SetupResponse("IncrementCounter", DomainResult.Success(
            [.. Enumerable.Range(0, count).Select(static _ => (Hexalith.EventStore.Contracts.Events.IEventPayload)
                new Hexalith.EventStore.Sample.Counter.Events.CounterIncremented())]));
        var identity = new AggregateIdentity("tenant-a", "counter", UniqueIdHelper.GenerateSortableUniqueStringId());
        using var transportBytes = new ActorTransportByteObserver();
        var proxies = new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = fixture.DaprHttpEndpoint }, transportBytes);
        IAggregateActor source = proxies.CreateActorProxy<IAggregateActor>(new ActorId(identity.ActorId), fixture.AggregateActorTypeName);
        (await source.ProcessCommandAsync(new CommandEnvelopeBuilder().WithTenantId(identity.TenantId).WithDomain(identity.Domain)
            .WithAggregateId(identity.AggregateId).WithCommandType("IncrementCounter").Build()).ConfigureAwait(true)).Accepted.ShouldBeTrue();
        transportBytes.Reset();
        using var measuredCost = new ProjectionDeliveryCostListener();
        SourceProofInspection first = await source.InspectSourceProofAsync().ConfigureAwait(true);
        first.Status.ShouldBe(SourceProofInspectionStatus.Available);
        SourceProof frozen = first.Proof.ShouldNotBeNull();
        frozen.HeadSequence.ShouldBe(count);
        SourceProof chain = SourceProofCodec.CreateSeed(identity, frozen.SourceIncarnation);
        long after = 0;
        int pages = 0;
        while (after < count)
        {
            SourceEventRange measured = await source.ReadMeasuredEventsBoundedRangeAsync(after, count, 256, 2_097_152).ConfigureAwait(true);
            EventEnvelope[] page = measured.Events;
            page.Length.ShouldBe((int)Math.Min(count - after, 256));
            measured.EnvelopeArrayBytes.ShouldBe(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(page,
                System.Text.Json.JsonSerializerOptions.Web).LongLength);
            measured.EnvelopeArrayBytes.ShouldBeLessThanOrEqualTo(2_097_152);
            foreach (EventEnvelope envelope in page)
            {
                envelope.SequenceNumber.ShouldBe(++after);
                chain = SourceProofCodec.Extend(chain, envelope);
            }
            pages++;
        }
        pages.ShouldBe((count + 255) / 256);
        chain.ShouldBe(frozen);
        var restartedProxy = new ActorProxyFactory(new ActorProxyOptions { HttpEndpoint = fixture.DaprHttpEndpoint }, transportBytes);
        IAggregateActor reread = restartedProxy.CreateActorProxy<IAggregateActor>(new ActorId(identity.ActorId), fixture.AggregateActorTypeName);
        (await reread.InspectSourceProofAsync().ConfigureAwait(true)).ShouldBe(first);
        (await reread.GetStreamMetadataAsync().ConfigureAwait(true)).CurrentSequence.ShouldBe(count);
        (await reread.ReadEventsBoundedRangeAsync(count, count, 256, 2_097_152).ConfigureAwait(true)).ShouldBeEmpty();
        measuredCost[ProjectionDeliveryCost.SourceInspections].ShouldBe(2);
        measuredCost[ProjectionDeliveryCost.SourceRanges].ShouldBe(pages + 1); // includes the explicit legacy empty-range API check
        measuredCost[ProjectionDeliveryCost.SourceEventReads].ShouldBe(count);
        measuredCost[ProjectionDeliveryCost.SourceReturnedEvents].ShouldBe(count);
        measuredCost[ProjectionDeliveryCost.SourceDeserializations].ShouldBe(count);
        measuredCost[ProjectionDeliveryCost.SourceSizingBytes].ShouldBe(measuredCost[ProjectionDeliveryCost.SourceDeserializationBytes]);
        measuredCost[ProjectionDeliveryCost.SourceSizingBytes].ShouldBeLessThanOrEqualTo(measuredCost[ProjectionDeliveryCost.SourceResponseBytes]);
        transportBytes.Requests.ShouldBe(pages + 4);
        transportBytes.RequestBodyBytes.ShouldBeGreaterThan(0);
        transportBytes.ResponseBodyBytes.ShouldBeGreaterThan(0);
        TestContext.Current.TestOutputHelper?.WriteLine("ProjectionSourceCost " + System.Text.Json.JsonSerializer.Serialize(new
        {
            head = count, pages,
            ranges = measuredCost[ProjectionDeliveryCost.SourceRanges],
            reads = measuredCost[ProjectionDeliveryCost.SourceEventReads],
            returned = measuredCost[ProjectionDeliveryCost.SourceReturnedEvents],
            sizingBytes = measuredCost[ProjectionDeliveryCost.SourceSizingBytes],
            responseBytes = measuredCost[ProjectionDeliveryCost.SourceResponseBytes],
            transportRequests = transportBytes.Requests,
            transportRequestBodyBytes = transportBytes.RequestBodyBytes,
            transportResponseBodyBytes = transportBytes.ResponseBodyBytes,
        }));
    }
}
