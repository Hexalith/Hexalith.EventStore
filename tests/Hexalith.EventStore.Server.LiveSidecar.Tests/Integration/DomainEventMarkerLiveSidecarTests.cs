using System.Text.Json;

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
        using DaprClient dispatchClient = CreateClient();
        using DaprClient recoveryClient = CreateClient();
        using DaprClient verificationClient = CreateClient();
        var dispatchStore = new DaprEventStoreDomainEventMarkerStore(dispatchClient, Options.Create(markerOptions));
        var recoveryStore = new DaprEventStoreDomainEventMarkerStore(recoveryClient, Options.Create(markerOptions));
        var verificationStore = new DaprEventStoreDomainEventMarkerStore(verificationClient, Options.Create(markerOptions));

        DaprMetadata metadata = await dispatchClient.GetMetadataAsync();
        string scopeHash = EventStorePayloadFingerprint.Compute(JsonSerializer.SerializeToUtf8Bytes(
            new[] { metadata.Id, markerOptions.PubSubName, markerOptions.TopicName, markerOptions.SubscriptionRoute }));
        string markerKey = $"{markerPrefix}v2:{scopeHash}:{messageId}";
        string releasedMessageId = messageId + "-released";
        string releasedKey = $"{markerPrefix}v2:{scopeHash}:{releasedMessageId}";
        string legacyKey = $"{markerPrefix}{markerOptions.TopicName}:{Uri.EscapeDataString(markerOptions.SubscriptionRoute)}:{messageId}-legacy";
        string fingerprint = EventStorePayloadFingerprint.Compute("persisted-content"u8);

        try
        {
            var original = (await dispatchStore.TryAcquireContentAsync(releasedMessageId, fingerprint)).Lease.ShouldNotBeNull();
            await dispatchStore.ReleaseFenceAsync(original);
            (await recoveryStore.TryAcquireContentAsync(releasedMessageId, EventStorePayloadFingerprint.Compute("different"u8))).Result
                .ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.ContentConflict);
            var released = (await verificationClient.GetStateAsync<EventStoreDomainEventMarkerRecord>(
                StoreName, releasedKey, ConsistencyMode.Strong)).ShouldNotBeNull();
            released.State.ShouldBe(EventStoreDomainEventMarkerState.Released);
            released.Fence.ShouldBe(original.Fence);
            var nextOwner = (await recoveryStore.TryAcquireContentAsync(releasedMessageId, fingerprint)).Lease.ShouldNotBeNull();
            nextOwner.Fence.ShouldNotBe(original.Fence);
            await dispatchStore.ReleaseFenceAsync(original);
            (await verificationStore.TryAcquireContentAsync(releasedMessageId, fingerprint)).Result
                .ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.InProgress);

            EventStoreDomainEventMarkerAdmission[] contenders = await Task.WhenAll(
                dispatchStore.TryAcquireContentAsync(messageId, fingerprint),
                recoveryStore.TryAcquireContentAsync(messageId, fingerprint));
            EventStoreDomainEventMarkerLease owner = contenders.Single(
                entry => entry.Result == EventStoreDomainEventMarkerAcquisitionResult.Acquired).Lease.ShouldNotBeNull();
            contenders.Single(entry => entry.Lease is null).Result.ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.InProgress);

            EventStoreDomainEventMarkerRecord owned = (await dispatchClient.GetStateAsync<EventStoreDomainEventMarkerRecord>(
                StoreName, markerKey, ConsistencyMode.Strong)).ShouldNotBeNull();
            owned.State.ShouldBe(EventStoreDomainEventMarkerState.InProgress);
            owned.Fence.ShouldBe(owner.Fence);
            owned.Fingerprint.ShouldBe(fingerprint);

            (await dispatchStore.MarkFencedDispatchedAsync(owner)).ShouldBeTrue();
            EventStoreDomainEventMarkerAdmission recovered = await recoveryStore.TryAcquireContentAsync(messageId, fingerprint);
            recovered.Result.ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.CompletionPending);
            recovered.Lease.ShouldBe(owner);
            await recoveryStore.MarkFencedCompletedAsync(recovered.Lease.ShouldNotBeNull());

            (await verificationStore.TryAcquireContentAsync(messageId, fingerprint)).Result
                .ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.Completed);
            (await verificationStore.TryAcquireContentAsync(messageId, EventStorePayloadFingerprint.Compute("different"u8))).Result
                .ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.ContentConflict);
            EventStoreDomainEventMarkerRecord completed = (await verificationClient.GetStateAsync<EventStoreDomainEventMarkerRecord>(
                StoreName, markerKey, ConsistencyMode.Strong)).ShouldNotBeNull();
            completed.State.ShouldBe(EventStoreDomainEventMarkerState.Completed);
            completed.Fence.ShouldBe(owner.Fence);

            await dispatchClient.SaveStateAsync(StoreName, legacyKey, EventStoreDomainEventMarkerRecord.Completed(DateTimeOffset.UtcNow));
            (await verificationStore.TryAcquireContentAsync(messageId + "-legacy", fingerprint)).Result
                .ShouldBe(EventStoreDomainEventMarkerAcquisitionResult.ContentConflict);
        }
        finally
        {
            await dispatchClient.DeleteStateAsync(StoreName, markerKey);
            await dispatchClient.DeleteStateAsync(StoreName, releasedKey);
            await dispatchClient.DeleteStateAsync(StoreName, legacyKey);
        }

        DaprClient CreateClient()
            => new DaprClientBuilder()
                .UseHttpEndpoint(fixture.DaprHttpEndpoint)
                .UseGrpcEndpoint(fixture.DaprGrpcEndpoint)
                .Build();
    }
}
