using Dapr.Client;

using Hexalith.EventStore.Client.Subscriptions;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Subscriptions;

public sealed class EventStoreDomainEventMarkerStoreTests {
    private const string MarkerKey = "domain-event:my-domain.events:%2Fmy-domain%2Fevents:message-1";

    [Fact]
    public void MarkerEnums_PreserveExistingOrdinalsAndAppendNewStates() {
        ((int)EventStoreDomainEventMarkerAcquisitionResult.Acquired).ShouldBe(0);
        ((int)EventStoreDomainEventMarkerAcquisitionResult.Completed).ShouldBe(1);
        ((int)EventStoreDomainEventMarkerAcquisitionResult.InProgress).ShouldBe(2);
        ((int)EventStoreDomainEventMarkerAcquisitionResult.CompletionPending).ShouldBe(3);

        ((int)EventStoreDomainEventMarkerState.InProgress).ShouldBe(0);
        ((int)EventStoreDomainEventMarkerState.Completed).ShouldBe(1);
        ((int)EventStoreDomainEventMarkerState.Dispatched).ShouldBe(2);
    }

    [Fact]
    public async Task InMemoryMarkerStore_DispatchAndCompletionAreMonotonicAndReleasePreservesDurableStates() {
        var store = new InMemoryEventStoreDomainEventMarkerStore();

        (await store.TryAcquireAsync("message-1")).ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.Acquired);
        (await store.TryAcquireAsync("message-1")).ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.InProgress);

        (await store.MarkDispatchedAsync("message-1")).ShouldBeTrue();
        (await store.TryAcquireAsync("message-1")).ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.CompletionPending);
        await store.ReleaseAsync("message-1");
        (await store.TryAcquireAsync("message-1")).ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.CompletionPending);

        await store.MarkCompletedAsync("message-1");
        await store.ReleaseAsync("message-1");
        (await store.TryAcquireAsync("message-1")).ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.Completed);
        (await store.MarkDispatchedAsync("message-1")).ShouldBeFalse();
        (await store.TryAcquireAsync("message-1")).ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.Completed);
    }

    [Fact]
    public async Task InMemoryMarkerStore_ReleaseRemovesOnlyInProgressMarker() {
        var store = new InMemoryEventStoreDomainEventMarkerStore();
        (await store.TryAcquireAsync("message-1")).ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.Acquired);

        await store.ReleaseAsync("message-1");

        (await store.TryAcquireAsync("message-1")).ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.Acquired);
    }

    [Fact]
    public async Task InterfaceDefaultMarkDispatched_CompletesLegacyStoreExactlyOnce() {
        IEventStoreDomainEventMarkerStore store = new LegacyMarkerStore();

        bool completionPending = await store.MarkDispatchedAsync("message-1");

        completionPending.ShouldBeFalse();
        ((LegacyMarkerStore)store).CompletionCount.ShouldBe(1);
    }

    [Fact]
    public async Task DaprMarkerStore_TryAcquireAsync_UsesStrongConsistencyAndDoesNotPersistLease() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<EventStoreDomainEventMarkerRecord?>(
                "markers",
                MarkerKey,
                Arg.Any<ConsistencyMode?>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<CancellationToken>())
            .Returns((EventStoreDomainEventMarkerRecord?)null);
        DaprEventStoreDomainEventMarkerStore store = CreateDaprStore(daprClient);

        EventStoreDomainEventMarkerAcquisitionResult result = await store.TryAcquireAsync("message-1");

        result.ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.Acquired);
        _ = await daprClient.Received(1).GetStateAsync<EventStoreDomainEventMarkerRecord?>(
            "markers",
            MarkerKey,
            ConsistencyMode.Strong,
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
        _ = await daprClient.DidNotReceiveWithAnyArgs().TrySaveStateAsync(
            default!,
            default!,
            Arg.Any<EventStoreDomainEventMarkerRecord>(),
            default!,
            default!,
            default!,
            default);
    }

    [Theory]
    [InlineData(EventStoreDomainEventMarkerState.Completed, EventStoreDomainEventMarkerAcquisitionResult.Completed)]
    [InlineData(EventStoreDomainEventMarkerState.InProgress, EventStoreDomainEventMarkerAcquisitionResult.InProgress)]
    [InlineData(EventStoreDomainEventMarkerState.Dispatched, EventStoreDomainEventMarkerAcquisitionResult.CompletionPending)]
    public async Task DaprMarkerStore_TryAcquireAsync_MapsKnownDurableStates(
        EventStoreDomainEventMarkerState state,
        EventStoreDomainEventMarkerAcquisitionResult expected) {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<EventStoreDomainEventMarkerRecord?>(
                "markers",
                MarkerKey,
                Arg.Any<ConsistencyMode?>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<CancellationToken>())
            .Returns(new EventStoreDomainEventMarkerRecord(state, DateTimeOffset.UtcNow));
        DaprEventStoreDomainEventMarkerStore store = CreateDaprStore(daprClient);

        EventStoreDomainEventMarkerAcquisitionResult result = await store.TryAcquireAsync("message-1");

        result.ShouldBe(expected);
    }

    [Fact]
    public async Task DaprMarkerStore_TryAcquireAsync_FailsClosedForUnknownDurableState() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAsync<EventStoreDomainEventMarkerRecord?>(
                "markers",
                MarkerKey,
                Arg.Any<ConsistencyMode?>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<CancellationToken>())
            .Returns(new EventStoreDomainEventMarkerRecord((EventStoreDomainEventMarkerState)999, DateTimeOffset.UtcNow));
        DaprEventStoreDomainEventMarkerStore store = CreateDaprStore(daprClient);

        EventStoreDomainEventMarkerAcquisitionResult result = await store.TryAcquireAsync("message-1");

        Enum.IsDefined(result).ShouldBeFalse();
    }

    [Fact]
    public async Task DaprMarkerStore_MarkDispatched_UsesStrongReadAndCheckedFirstWrite() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupStateAndEtag(daprClient, null, string.Empty);
        SetupTrySave(daprClient, true);
        DaprEventStoreDomainEventMarkerStore store = CreateDaprStore(daprClient);

        bool completionPending = await store.MarkDispatchedAsync("message-1");

        completionPending.ShouldBeTrue();
        _ = await daprClient.Received(1).GetStateAndETagAsync<EventStoreDomainEventMarkerRecord?>(
            "markers",
            MarkerKey,
            ConsistencyMode.Strong,
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
        _ = await daprClient.Received(1).TrySaveStateAsync(
            "markers",
            MarkerKey,
            Arg.Is<EventStoreDomainEventMarkerRecord>(record => record.State == EventStoreDomainEventMarkerState.Dispatched),
            string.Empty,
            Arg.Is<StateOptions>(options => options.Concurrency == ConcurrencyMode.FirstWrite),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DaprMarkerStore_MarkCompleted_PersistsAbsentMarkerWithCheckedFirstWrite() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupStateAndEtag(daprClient, null, string.Empty);
        SetupTrySave(daprClient, true);
        DaprEventStoreDomainEventMarkerStore store = CreateDaprStore(daprClient);

        await store.MarkCompletedAsync("message-1");

        _ = await daprClient.Received(1).TrySaveStateAsync(
            "markers",
            MarkerKey,
            Arg.Is<EventStoreDomainEventMarkerRecord>(record => record.State == EventStoreDomainEventMarkerState.Completed),
            string.Empty,
            Arg.Is<StateOptions>(options => options.Concurrency == ConcurrencyMode.FirstWrite),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DaprMarkerStore_MarkCompleted_SaveConflictConvergesWhenFreshReadIsCompleted() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAndETagAsync<EventStoreDomainEventMarkerRecord?>(
                "markers",
                MarkerKey,
                Arg.Any<ConsistencyMode?>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<CancellationToken>())
            .Returns(
                ((EventStoreDomainEventMarkerRecord?, string))(null, string.Empty),
                ((EventStoreDomainEventMarkerRecord?, string))(
                    EventStoreDomainEventMarkerRecord.Completed(DateTimeOffset.UtcNow),
                    "etag-2"));
        SetupTrySave(daprClient, false);
        DaprEventStoreDomainEventMarkerStore store = CreateDaprStore(daprClient);

        await store.MarkCompletedAsync("message-1");

        _ = await daprClient.Received(2).GetStateAndETagAsync<EventStoreDomainEventMarkerRecord?>(
            "markers",
            MarkerKey,
            ConsistencyMode.Strong,
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
        _ = await daprClient.Received(1).TrySaveStateAsync(
            "markers",
            MarkerKey,
            Arg.Any<EventStoreDomainEventMarkerRecord>(),
            string.Empty,
            Arg.Any<StateOptions>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(EventStoreDomainEventMarkerState.Dispatched, true)]
    [InlineData(EventStoreDomainEventMarkerState.Completed, false)]
    public async Task DaprMarkerStore_MarkDispatched_SaveConflictConvergesToAlreadyAdvancedState(
        EventStoreDomainEventMarkerState advancedState,
        bool expectedCompletionPending) {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetStateAndETagAsync<EventStoreDomainEventMarkerRecord?>(
                "markers",
                MarkerKey,
                Arg.Any<ConsistencyMode?>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<CancellationToken>())
            .Returns(
                ((EventStoreDomainEventMarkerRecord?, string))(null, string.Empty),
                ((EventStoreDomainEventMarkerRecord?, string))(
                    new EventStoreDomainEventMarkerRecord(advancedState, DateTimeOffset.UtcNow),
                    "etag-2"));
        SetupTrySave(daprClient, false);
        DaprEventStoreDomainEventMarkerStore store = CreateDaprStore(daprClient);

        bool completionPending = await store.MarkDispatchedAsync("message-1");

        completionPending.ShouldBe(expectedCompletionPending);
        _ = await daprClient.Received(2).GetStateAndETagAsync<EventStoreDomainEventMarkerRecord?>(
            "markers",
            MarkerKey,
            ConsistencyMode.Strong,
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DaprMarkerStore_TransitionPersistentSaveRejectionThrowsContextualFailure() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupStateAndEtag(daprClient, null, string.Empty);
        SetupTrySave(daprClient, false);
        DaprEventStoreDomainEventMarkerStore store = CreateDaprStore(daprClient);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => store.MarkCompletedAsync("message-1"));

        exception.Message.ShouldContain("message-1");
        exception.Message.ShouldContain(nameof(EventStoreDomainEventMarkerState.Completed));
    }

    [Fact]
    public async Task DaprMarkerStore_Release_IsNoOpAndDoesNotDeleteDurableMarker() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        DaprEventStoreDomainEventMarkerStore store = CreateDaprStore(daprClient);

        await store.ReleaseAsync("message-1");

        await daprClient.DidNotReceiveWithAnyArgs().DeleteStateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<StateOptions>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    private static DaprEventStoreDomainEventMarkerStore CreateDaprStore(DaprClient daprClient)
        => new(
            daprClient,
            Options.Create(new EventStoreDomainEventsOptions {
                MarkerStateStoreName = "markers",
                MarkerKeyPrefix = "domain-event:",
                TopicName = "my-domain.events",
                SubscriptionRoute = "/my-domain/events",
            }));

    private static void SetupStateAndEtag(
        DaprClient daprClient,
        EventStoreDomainEventMarkerRecord? record,
        string etag)
        => _ = daprClient.GetStateAndETagAsync<EventStoreDomainEventMarkerRecord?>(
                "markers",
                MarkerKey,
                Arg.Any<ConsistencyMode?>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<CancellationToken>())
            .Returns(((EventStoreDomainEventMarkerRecord?, string))(record, etag));

    private static void SetupTrySave(DaprClient daprClient, bool result)
        => _ = daprClient.TrySaveStateAsync(
                "markers",
                MarkerKey,
                Arg.Any<EventStoreDomainEventMarkerRecord>(),
                Arg.Any<string>(),
                Arg.Any<StateOptions>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<CancellationToken>())
            .Returns(result);

    private sealed class LegacyMarkerStore : IEventStoreDomainEventMarkerStore {
        public int CompletionCount { get; private set; }

        public Task<EventStoreDomainEventMarkerAcquisitionResult> TryAcquireAsync(
            string messageId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(EventStoreDomainEventMarkerAcquisitionResult.Acquired);

        public Task MarkCompletedAsync(string messageId, CancellationToken cancellationToken = default) {
            CompletionCount++;
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(string messageId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
