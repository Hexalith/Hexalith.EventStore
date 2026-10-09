using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.DomainService;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

public sealed class VersionedWireRoundTripTests
{
    [Fact]
    public async Task BoundedResponseToPersister_RetainsStandaloneVersionTwo()
    {
        byte[] payload = "{\"Value\":7}"u8.ToArray();
        var source = new DomainServiceWireResult(false,
            [new DomainServiceWireEvent("Domain.VersionedEvent", payload) { PayloadVersion = 2 }]);
        using var output = new MemoryStream();
        await BoundedV1WireResultResponse.WriteAsync(output, source, CancellationToken.None);
        output.Position = 0;
        using var parser = new BoundedV1DomainResponseParser(output, CancellationToken.None);
        DomainServiceWireResult parsed = await parser.ParseAsync();
        parsed.Events.ShouldHaveSingleItem().PayloadVersion.ShouldBe(2);

        DomainResult domainResult = DaprDomainServiceInvoker.ToDomainResult(parsed);
        var identity = new AggregateIdentity("tenant-a", "orders", "order-1");
        IActorStateManager state = Substitute.For<IActorStateManager>();
        _ = state.TryGetStateAsync<AggregateMetadata>(identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(false, default!));
        var persister = new EventPersister(state, Substitute.For<ILogger<EventPersister>>(),
            new NoOpEventPayloadProtectionService());
        var command = new CommandEnvelope("01ARZ3NDEKTSV4RRFFQ69G5FAV", "tenant-a", "orders", "order-1",
            "Change", "{}"u8.ToArray(), "trace-1", "cause-1", "user-1", null);

        EventPersistResult persisted = await persister.PersistEventsAsync(identity, "Order", command, domainResult, "v2");

        EventEnvelope envelope = persisted.PersistedEnvelopes.ShouldHaveSingleItem();
        envelope.MetadataVersion.ShouldBe(1);
        envelope.PayloadVersion.ShouldBe(2);
        envelope.EventContractType.ShouldBeNull();
        envelope.Payload.ShouldBe(payload);
    }
}
