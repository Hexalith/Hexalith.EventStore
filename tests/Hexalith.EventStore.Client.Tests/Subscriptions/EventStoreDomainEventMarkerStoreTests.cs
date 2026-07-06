using Dapr.Client;

using Hexalith.EventStore.Client.Subscriptions;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Subscriptions;

public sealed class EventStoreDomainEventMarkerStoreTests {
    [Fact]
    public async Task InMemoryMarkerStore_AcquiresCompletesDetectsDuplicatesAndReleases() {
        var store = new InMemoryEventStoreDomainEventMarkerStore();

        EventStoreDomainEventMarkerAcquisitionResult first = await store.TryAcquireAsync("message-1");
        EventStoreDomainEventMarkerAcquisitionResult concurrent = await store.TryAcquireAsync("message-1");
        await store.MarkCompletedAsync("message-1");
        EventStoreDomainEventMarkerAcquisitionResult completed = await store.TryAcquireAsync("message-1");
        await store.ReleaseAsync("message-1");
        EventStoreDomainEventMarkerAcquisitionResult reacquired = await store.TryAcquireAsync("message-1");

        first.ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.Acquired);
        concurrent.ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.InProgress);
        completed.ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.Completed);
        reacquired.ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.Acquired);
    }

    [Fact]
    public async Task DaprMarkerStore_TryAcquireAsync_ReturnsAcquiredWhenNoCompletedMarkerExists() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<EventStoreDomainEventMarkerRecord?>(
                "markers",
                "domain-event:my-domain.events:%2Fmy-domain%2Fevents:message-1",
                Arg.Any<ConsistencyMode?>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<CancellationToken>())
            .Returns((EventStoreDomainEventMarkerRecord?)null);
        var store = new DaprEventStoreDomainEventMarkerStore(
            daprClient,
            Options.Create(new EventStoreDomainEventsOptions {
                MarkerStateStoreName = "markers",
                MarkerKeyPrefix = "domain-event:",
                TopicName = "my-domain.events",
                SubscriptionRoute = "/my-domain/events",
            }));

        EventStoreDomainEventMarkerAcquisitionResult result = await store.TryAcquireAsync("message-1");

        result.ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.Acquired);
        _ = await daprClient.DidNotReceiveWithAnyArgs().TrySaveStateAsync(
            default!,
            default!,
            Arg.Any<EventStoreDomainEventMarkerRecord>(),
            default!,
            default!,
            default!,
            default);
    }

    [Fact]
    public async Task DaprMarkerStore_TryAcquireAsync_ReturnsCompletedWhenCompletedMarkerExists() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<EventStoreDomainEventMarkerRecord?>(
                "markers",
                "domain-event:my-domain.events:%2Fmy-domain%2Fevents:message-1",
                Arg.Any<ConsistencyMode?>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<CancellationToken>())
            .Returns(EventStoreDomainEventMarkerRecord.Completed(DateTimeOffset.UtcNow));
        var store = new DaprEventStoreDomainEventMarkerStore(
            daprClient,
            Options.Create(new EventStoreDomainEventsOptions {
                MarkerStateStoreName = "markers",
                MarkerKeyPrefix = "domain-event:",
                TopicName = "my-domain.events",
                SubscriptionRoute = "/my-domain/events",
            }));

        EventStoreDomainEventMarkerAcquisitionResult result = await store.TryAcquireAsync("message-1");

        result.ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.Completed);
    }

    [Fact]
    public async Task DaprMarkerStore_MarkCompleted_UsesConfiguredScopedMarkerKeyWithFirstWrite() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        var store = new DaprEventStoreDomainEventMarkerStore(
            daprClient,
            Options.Create(new EventStoreDomainEventsOptions {
                MarkerStateStoreName = "markers",
                MarkerKeyPrefix = "domain-event:",
                TopicName = "my-domain.events",
                SubscriptionRoute = "/my-domain/events",
            }));

        await store.MarkCompletedAsync("message-1");

        _ = await daprClient.Received(1).TrySaveStateAsync(
            "markers",
            "domain-event:my-domain.events:%2Fmy-domain%2Fevents:message-1",
            Arg.Is<EventStoreDomainEventMarkerRecord>(record => record.State == EventStoreDomainEventMarkerState.Completed),
            string.Empty,
            Arg.Is<StateOptions>(options => options.Concurrency == ConcurrencyMode.FirstWrite),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DaprMarkerStore_Release_IsNoOpAndDoesNotDeleteCompletedMarker() {
        // The DAPR store never persists an in-progress lease, so a failing delivery owns no marker to
        // release. An unconditional delete would race a concurrent sibling's durable Completed marker and
        // wipe it, letting a later redelivery re-run side effects. Release must therefore issue no state
        // mutation at all — this is the regression guard for that concurrency defect.
        DaprClient daprClient = Substitute.For<DaprClient>();
        var store = new DaprEventStoreDomainEventMarkerStore(
            daprClient,
            Options.Create(new EventStoreDomainEventsOptions {
                MarkerStateStoreName = "markers",
                MarkerKeyPrefix = "domain-event:",
                TopicName = "my-domain.events",
                SubscriptionRoute = "/my-domain/events",
            }));

        await store.ReleaseAsync("message-1");

        await daprClient.DidNotReceiveWithAnyArgs().DeleteStateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<StateOptions>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }
}
