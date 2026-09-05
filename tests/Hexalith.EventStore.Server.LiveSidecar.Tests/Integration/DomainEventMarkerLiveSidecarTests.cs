using Dapr.Client;

using Hexalith.EventStore.Client.Subscriptions;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Exercises the durable domain-event marker protocol against the Redis-backed DAPR state store.</summary>
[Collection("DaprTestContainer")]
[Trait("Category", "LiveSidecar")]
public sealed class DomainEventMarkerLiveSidecarTests(DaprTestContainerFixture fixture) {
    private const string StoreName = "statestore";

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Tier", "2")]
    public async Task DispatchedMarker_IsRecoveredAndCompletedThroughFreshClientView() {
        fixture.ThrowIfHostStopped();

        string markerPrefix = $"eventstore:live-domain-marker:{Guid.NewGuid():N}:";
        string messageId = $"message-{Guid.NewGuid():N}";
        var markerOptions = new EventStoreDomainEventsOptions {
            MarkerStateStoreName = StoreName,
            MarkerKeyPrefix = markerPrefix,
            TopicName = "work.events",
            SubscriptionRoute = "/work/events",
        };
        string markerKey = string.Concat(
            markerPrefix,
            Uri.EscapeDataString(markerOptions.TopicName),
            ":",
            Uri.EscapeDataString(markerOptions.SubscriptionRoute),
            ":",
            messageId);

        using DaprClient dispatchClient = CreateClient();
        using DaprClient recoveryClient = CreateClient();
        using DaprClient verificationClient = CreateClient();
        var dispatchStore = new DaprEventStoreDomainEventMarkerStore(dispatchClient, Options.Create(markerOptions));
        var recoveryStore = new DaprEventStoreDomainEventMarkerStore(recoveryClient, Options.Create(markerOptions));
        var verificationStore = new DaprEventStoreDomainEventMarkerStore(verificationClient, Options.Create(markerOptions));

        try {
            (await dispatchStore.MarkDispatchedAsync(messageId)).ShouldBeTrue();
            EventStoreDomainEventMarkerRecord dispatched = (await dispatchClient.GetStateAsync<EventStoreDomainEventMarkerRecord>(
                StoreName,
                markerKey,
                ConsistencyMode.Strong)).ShouldNotBeNull();
            dispatched.State.ShouldBe(EventStoreDomainEventMarkerState.Dispatched);

            (await recoveryStore.TryAcquireAsync(messageId))
                .ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.CompletionPending);
            await recoveryStore.MarkCompletedAsync(messageId);

            (await verificationStore.TryAcquireAsync(messageId))
                .ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.Completed);
            EventStoreDomainEventMarkerRecord completed = (await verificationClient.GetStateAsync<EventStoreDomainEventMarkerRecord>(
                StoreName,
                markerKey,
                ConsistencyMode.Strong)).ShouldNotBeNull();
            completed.State.ShouldBe(EventStoreDomainEventMarkerState.Completed);
        }
        finally {
            await dispatchClient.DeleteStateAsync(StoreName, markerKey);
        }

        DaprClient CreateClient()
            => new DaprClientBuilder()
                .UseHttpEndpoint(fixture.DaprHttpEndpoint)
                .UseGrpcEndpoint(fixture.DaprGrpcEndpoint)
                .Build();
    }
}
