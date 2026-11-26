using System.Globalization;

using Dapr;
using Dapr.Client;

using Grpc.Core;

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
    public async Task DevelopmentCapacityAndExpiryProtectActiveAndAmbiguousMarkers()
    {
        var clock = new ManualClock();
        var store = new InMemoryEventStoreDomainEventMarkerStore(new EventStoreDomainEventsOptions
        {
            DevelopmentMarkerCapacity = 3,
            DevelopmentMarkerRetention = TimeSpan.FromMinutes(1),
        }, clock);

        EventStoreDomainEventMarkerLease active = (await store.TryAcquireContentAsync("active", "a")).Lease.ShouldNotBeNull();
        EventStoreDomainEventMarkerLease dispatched = (await store.TryAcquireContentAsync("dispatched", "b")).Lease.ShouldNotBeNull();
        EventStoreDomainEventMarkerLease completed = (await store.TryAcquireContentAsync("completed", "c")).Lease.ShouldNotBeNull();
        (await store.MarkFencedDispatchedAsync(dispatched)).ShouldBeTrue();
        (await store.MarkFencedDispatchedAsync(completed)).ShouldBeTrue();
        await store.MarkFencedCompletedAsync(completed);

        clock.Now += TimeSpan.FromMinutes(1) - TimeSpan.FromTicks(1);
        (await store.TryAcquireContentAsync("pressure", "d")).Result.ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.InProgress);

        clock.Now += TimeSpan.FromTicks(1);
        (await store.TryAcquireContentAsync("pressure", "d")).Result.ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.Acquired);
        (await store.TryAcquireContentAsync(active.MessageId, active.Fingerprint)).Result.ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.InProgress);
        (await store.TryAcquireContentAsync(dispatched.MessageId, dispatched.Fingerprint)).Result.ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.CompletionPending);
        await store.ReleaseFenceAsync(active);
        EventStoreDomainEventMarkerLease replacement = (await store.TryAcquireContentAsync(active.MessageId, active.Fingerprint)).Lease.ShouldNotBeNull();
        await store.ReleaseFenceAsync(active);
        (await store.TryAcquireContentAsync(replacement.MessageId, replacement.Fingerprint))
            .Result.ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.InProgress);
    }

    [Fact]
    public async Task DevelopmentConcurrentAcquisitionEnforcesCapacityAndSingleOwner()
    {
        var store = new InMemoryEventStoreDomainEventMarkerStore(new EventStoreDomainEventsOptions
        {
            DevelopmentMarkerCapacity = 4,
        });

        EventStoreDomainEventMarkerAdmission[] distinct = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(index => Task.Run(() => store.TryAcquireContentAsync($"distinct-{index}", "fingerprint"))));

        distinct.Count(admission => admission.Result == EventStoreDomainEventMarkerAcquisitionResult.Acquired).ShouldBe(4);
        distinct.Where(admission => admission.Result != EventStoreDomainEventMarkerAcquisitionResult.Acquired)
            .ShouldAllBe(admission => admission.Result == EventStoreDomainEventMarkerAcquisitionResult.InProgress && admission.Lease == null);

        var contended = new InMemoryEventStoreDomainEventMarkerStore();
        EventStoreDomainEventMarkerAdmission[] same = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => contended.TryAcquireContentAsync("shared", "fingerprint"))));

        same.Count(admission => admission.Result == EventStoreDomainEventMarkerAcquisitionResult.Acquired).ShouldBe(1);
        same.Where(admission => admission.Result != EventStoreDomainEventMarkerAcquisitionResult.Acquired)
            .ShouldAllBe(admission => admission.Result == EventStoreDomainEventMarkerAcquisitionResult.InProgress && admission.Lease == null);
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => Now;
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

    [Fact]
    public async Task DaprMarkerStore_QualificationRequiresAnExplicitFirstWriteRejection()
    {
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(new DaprMetadata(
            "subscriber", [], new Dictionary<string, string>(),
            [new DaprComponentsMetadata("markers", "state.redis", "v1", ["ETAG"])]));
        _ = daprClient.TrySaveStateAsync("markers", Arg.Any<string>(), Arg.Any<EventStoreDomainEventMarkerRecord>(),
            string.Empty, Arg.Any<StateOptions>(), Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>()).Returns(
                _ => Task.FromResult(true),
                _ => Task.FromException<bool>(new DaprException("State store unavailable.")));

        DaprEventStoreDomainEventMarkerStore store = CreateDaprStore(daprClient);

        await Should.ThrowAsync<DaprException>(() => store.TryAcquireContentAsync("message-1", "fingerprint"));
        await Should.ThrowAsync<DaprException>(() => store.TryAcquireContentAsync("message-1", "fingerprint"));
        _ = await daprClient.Received(2).GetMetadataAsync(Arg.Any<CancellationToken>());
        _ = await daprClient.DidNotReceiveWithAnyArgs().GetStateAndETagAsync<EventStoreDomainEventMarkerRecord?>(
            default!, default!, default, default!, default);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DaprMarkerStore_QualifiesTheObservedRedisFirstWriteRejection(bool withScriptHash)
    {
        // Dapr 1.19.1 Redis refuses a second create-if-absent from its Lua script and reports it as gRPC Internal,
        // with or without the Redis 7 script-hash suffix, for the key under the app ID prefix.
        DaprClient daprClient = CreateRedisFirstWriteClient(key => withScriptHash
            ? $"failed saving state in state store markers: user_script:14: failed to set key subscriber||{key} script: {RedisScriptHash}, on @user_script:14."
            : $"failed saving state in state store markers: user_script:14: failed to set key subscriber||{key}");
        DaprEventStoreDomainEventMarkerStore store = CreateDaprStore(daprClient);

        EventStoreDomainEventMarkerAdmission admission = await store.TryAcquireContentAsync("message-1", "fingerprint");

        admission.Result.ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.Acquired);
        _ = await daprClient.Received(1).TryDeleteStateAsync("markers", Arg.Is<string>(key => key.Contains(":probe:", StringComparison.Ordinal)),
            Arg.Any<string>(), Arg.Any<StateOptions>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("failed saving state in state store markers: user_script:15: failed to set key subscriber||{0}")]
    [InlineData("failed saving state in state store markers: user_script:14: failed to set key subscriber||{0}-other")]
    [InlineData("failed saving state in state store markers: user_script:14: failed to set key subscriber||{0} script: 0123456789abcdef0123456789abcdef0123456, on @user_script:14.")]
    public async Task DaprMarkerStore_RefusesANearMissRedisRejection(string detailFormat)
    {
        DaprClient daprClient = CreateRedisFirstWriteClient(key => string.Format(CultureInfo.InvariantCulture, detailFormat, key));
        DaprEventStoreDomainEventMarkerStore store = CreateDaprStore(daprClient);

        _ = await Should.ThrowAsync<DaprException>(() => store.TryAcquireContentAsync("message-1", "fingerprint"));

        _ = await daprClient.DidNotReceiveWithAnyArgs().TryDeleteStateAsync(default!, default!, default!, default!, default!, default);
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

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => store.TryAcquireAsync("message-1"));

        exception.Message.ShouldContain("message-1");
        exception.Message.ShouldContain("999");
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
    public async Task DaprMarkerStore_MarkCompleted_TransitionsDispatchedEtagWithCheckedFirstWrite() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupStateAndEtag(
            daprClient,
            EventStoreDomainEventMarkerRecord.Dispatched(DateTimeOffset.UtcNow),
            "etag-dispatched");
        SetupTrySave(daprClient, true);
        DaprEventStoreDomainEventMarkerStore store = CreateDaprStore(daprClient);

        await store.MarkCompletedAsync("message-1");

        _ = await daprClient.Received(1).GetStateAndETagAsync<EventStoreDomainEventMarkerRecord?>(
            "markers",
            MarkerKey,
            ConsistencyMode.Strong,
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
        _ = await daprClient.Received(1).TrySaveStateAsync(
            "markers",
            MarkerKey,
            Arg.Is<EventStoreDomainEventMarkerRecord>(record => record.State == EventStoreDomainEventMarkerState.Completed),
            "etag-dispatched",
            Arg.Is<StateOptions>(options => options.Concurrency == ConcurrencyMode.FirstWrite),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DaprMarkerStore_TransitionRejectsPersistedInProgressOwnerWithoutSave() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupStateAndEtag(
            daprClient,
            EventStoreDomainEventMarkerRecord.InProgress(DateTimeOffset.UtcNow),
            "etag-in-progress");
        DaprEventStoreDomainEventMarkerStore store = CreateDaprStore(daprClient);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => store.MarkCompletedAsync("message-1"));

        exception.Message.ShouldContain("message-1");
        exception.Message.ShouldContain("in-progress");
        exception.Message.ShouldContain(nameof(EventStoreDomainEventMarkerState.Completed));
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
    public async Task DaprMarkerStore_TransitionExhaustion_FinalConvergenceReadIsStrong() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        (EventStoreDomainEventMarkerRecord?, string) absent = (null, string.Empty);
        (EventStoreDomainEventMarkerRecord?, string) completed = (
            EventStoreDomainEventMarkerRecord.Completed(DateTimeOffset.UtcNow),
            "etag-completed");
        _ = daprClient.GetStateAndETagAsync<EventStoreDomainEventMarkerRecord?>(
                "markers",
                MarkerKey,
                Arg.Any<ConsistencyMode?>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<CancellationToken>())
            .Returns(absent, absent, absent, absent, absent, completed);
        SetupTrySave(daprClient, false);
        DaprEventStoreDomainEventMarkerStore store = CreateDaprStore(daprClient);

        await store.MarkCompletedAsync("message-1");

        _ = await daprClient.Received(6).GetStateAndETagAsync<EventStoreDomainEventMarkerRecord?>(
            "markers",
            MarkerKey,
            ConsistencyMode.Strong,
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
        _ = await daprClient.Received(5).TrySaveStateAsync(
            "markers",
            MarkerKey,
            Arg.Any<EventStoreDomainEventMarkerRecord>(),
            string.Empty,
            Arg.Any<StateOptions>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
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

    private const string RedisScriptHash = "0123456789abcdef0123456789abcdef01234567";

    private static DaprClient CreateRedisFirstWriteClient(Func<string, string> rejectionDetail)
    {
        var state = new Dictionary<string, (EventStoreDomainEventMarkerRecord Record, string ETag)>(StringComparer.Ordinal);
        int version = 0;
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(new DaprMetadata(
            "subscriber", [], new Dictionary<string, string>(),
            [new DaprComponentsMetadata("markers", "state.redis", "v1", ["ETAG"])]));
        _ = daprClient.TrySaveStateAsync("markers", Arg.Any<string>(), Arg.Any<EventStoreDomainEventMarkerRecord>(),
            Arg.Any<string>(), Arg.Any<StateOptions>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string key = call.ArgAt<string>(1);
                if (state.ContainsKey(key) && string.IsNullOrEmpty(call.ArgAt<string>(3)))
                {
                    return Task.FromException<bool>(new DaprException("State save failed.",
                        new RpcException(new Status(StatusCode.Internal, rejectionDetail(key)))));
                }

                state[key] = (call.ArgAt<EventStoreDomainEventMarkerRecord>(2), (++version).ToString(CultureInfo.InvariantCulture));
                return Task.FromResult(true);
            });
        _ = daprClient.GetStateAndETagAsync<EventStoreDomainEventMarkerRecord?>("markers", Arg.Any<string>(),
                Arg.Any<ConsistencyMode?>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => state.TryGetValue(call.ArgAt<string>(1), out var current)
                ? ((EventStoreDomainEventMarkerRecord?, string))(current.Record, current.ETag)
                : ((EventStoreDomainEventMarkerRecord?, string))(null, string.Empty));
        _ = daprClient.TryDeleteStateAsync("markers", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<StateOptions>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(call => state.TryGetValue(call.ArgAt<string>(1), out var current) && current.ETag == call.ArgAt<string>(2)
                && state.Remove(call.ArgAt<string>(1)));
        return daprClient;
    }

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
