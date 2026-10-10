using System.Text.Json;

using Dapr.Actors.Runtime;
using Dapr.Client;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Tests.Actors;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

public sealed class VersionedWireRoundTripTests
{
    [Fact]
    public async Task ActorWriteAndRead_RoundTripsV1AndV2ThroughJsonStateAndDomainRouting()
    {
        var identity = new AggregateIdentity("test-tenant", "test-domain", "agg-001");
        byte[] oldPayload = "{\"Amount\":1}"u8.ToArray();
        var oldEvent = new EventEnvelope(
            Guid.NewGuid().ToString(), identity.AggregateId, "CreateOrder", identity.TenantId, identity.Domain,
            1, 0, DateTimeOffset.UtcNow, "corr-1", "cause-1", "system", "v1",
            typeof(VersionedActorEvent).FullName!, 1, "json", oldPayload, null);
        string oldJson = JsonSerializer.Serialize(oldEvent, JsonSerializerOptions.Web);
        var state = Substitute.For<IActorStateManager>();
        _ = state.TryGetStateAsync<AggregateMetadata>(identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true,
                new AggregateMetadata(1, DateTimeOffset.UtcNow, null)));
        _ = state.TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(_ => new ConditionalValue<EventEnvelope>(true,
                JsonSerializer.Deserialize<EventEnvelope>(oldJson, JsonSerializerOptions.Web)!));
        string? newJson = null;
        state.When(manager => manager.SetStateAsync(
            $"{identity.EventStreamKeyPrefix}2", Arg.Any<EventEnvelope>(), Arg.Any<CancellationToken>()))
            .Do(call => newJson = JsonSerializer.Serialize(call.ArgAt<EventEnvelope>(1), JsonSerializerOptions.Web));

        var aggregate = new VersionedActorAggregate();
        using ServiceProvider domainServices = new ServiceCollection()
            .AddKeyedSingleton<IAsyncDomainProcessor>(identity.Domain, aggregate)
            .BuildServiceProvider();
        var responseHandler = new VersionedActorResponseHandler(async (requestJson, token) =>
        {
            DomainServiceRequest request = JsonSerializer.Deserialize<DomainServiceRequest>(requestJson,
                JsonSerializerOptions.Web)!;
            DomainServiceWireResult response = await DomainServiceRequestRouter.ProcessAsync(domainServices,
                request, token);
            return JsonSerializer.Serialize(response, JsonSerializerOptions.Web);
        });
        using var httpClient = new HttpClient(responseHandler);
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        _ = factory.CreateClient(DaprDomainServiceInvoker.HttpClientName).Returns(httpClient);
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        _ = resolver.ResolveAsync(identity.TenantId, identity.Domain, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new DomainServiceRegistration("test-app", "process", identity.TenantId, identity.Domain, null));
        using DaprClient daprClient = new DaprClientBuilder().Build();
        var invoker = new DaprDomainServiceInvoker(daprClient, factory, resolver,
            Options.Create(new DomainServiceOptions()), TimeProvider.System,
            NullLogger<DaprDomainServiceInvoker>.Instance);
        ActorTestContext actor = AggregateActorTestHelper.CreateActor(stateManager: state, invoker: invoker);
        AggregateActorTestHelper.ConfigureNoDuplicate(state);
        // The existing stream has one committed event; override the helper's fresh-stream default.
        _ = state.TryGetStateAsync<AggregateMetadata>(identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true,
                new AggregateMetadata(1, DateTimeOffset.UtcNow, null)));

        CommandEnvelope command = AggregateActorTestHelper.CreateTestEnvelope() with
        {
            CommandType = nameof(VersionedActorCommand),
            Payload = "{}"u8.ToArray(),
            CorrelationId = "corr-1",
            CausationId = "cause-1",
        };
        CommandProcessingResult result = await actor.Actor.ProcessCommandAsync(command);

        result.Accepted.ShouldBeTrue();
        aggregate.ObservedValue.ShouldBe(1);
        responseHandler.RequestJson.ShouldNotBeNull().ShouldContain(Convert.ToBase64String(oldPayload));
        EventEnvelope written = JsonSerializer.Deserialize<EventEnvelope>(newJson.ShouldNotBeNull(), JsonSerializerOptions.Web)!;
        byte[] newPayload = "{\"Value\":2}"u8.ToArray();
        written.PayloadVersion.ShouldBe(2);
        written.MetadataVersion.ShouldBe(1);
        written.EventContractType.ShouldBeNull();
        written.Payload.ShouldBe(newPayload);
        _ = await actor.EventPublisher.Received(1).PublishEventsAsync(identity,
            Arg.Is<IReadOnlyList<EventEnvelope>>(events => events.Count == 1
                && events[0].MessageId == written.MessageId
                && events[0].MetadataVersion == 1
                && events[0].PayloadVersion == 2
                && events[0].Payload.SequenceEqual(newPayload)),
            Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>());

        var readState = Substitute.For<IActorStateManager>();
        _ = readState.TryGetStateAsync<AggregateMetadata>(identity.MetadataKey, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<AggregateMetadata>(true,
                JsonSerializer.Deserialize<AggregateMetadata>(JsonSerializer.Serialize(
                    new AggregateMetadata(2, DateTimeOffset.UtcNow, null), JsonSerializerOptions.Web), JsonSerializerOptions.Web)!));
        _ = readState.TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}1", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true,
                JsonSerializer.Deserialize<EventEnvelope>(oldJson, JsonSerializerOptions.Web)!));
        _ = readState.TryGetStateAsync<EventEnvelope>($"{identity.EventStreamKeyPrefix}2", Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<EventEnvelope>(true,
                JsonSerializer.Deserialize<EventEnvelope>(newJson, JsonSerializerOptions.Web)!));
        var reader = new EventStreamReader(readState, NullLogger<EventStreamReader>.Instance);

        RehydrationResult history = (await reader.RehydrateAsync(identity)).ShouldNotBeNull();
        history.Events.Count.ShouldBe(2);
        history.Events[0].PayloadVersion.ShouldBeNull();
        history.Events[0].Payload.ShouldBe(oldPayload);
        history.Events[1].PayloadVersion.ShouldBe(2);
        history.Events[1].Payload.ShouldBe(newPayload);
        var currentState = new DomainServiceCurrentState(null,
            [.. history.Events.Select(AggregateActor.ToContractEventEnvelope)], 0, 2);
        DomainServiceWireResult replayed = await DomainServiceRequestRouter.ProcessAsync(domainServices,
            new DomainServiceRequest(command, currentState));
        aggregate.ObservedValue.ShouldBe(3);
        replayed.Events.ShouldHaveSingleItem().PayloadVersion.ShouldBe(2);
        JsonSerializer.Deserialize<VersionedActorEvent>(replayed.Events[0].Payload)!.Value.ShouldBe(4);
    }

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
