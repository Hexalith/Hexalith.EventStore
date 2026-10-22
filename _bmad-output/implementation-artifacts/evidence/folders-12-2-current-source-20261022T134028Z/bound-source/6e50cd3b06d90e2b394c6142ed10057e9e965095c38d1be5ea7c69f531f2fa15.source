using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public partial class ProjectionDeliveryIdempotencyCoordinatorTests
{
    [Theory]
    [InlineData(256)]
    [InlineData(4096)]
    public async Task OwningStorePreservesExactRetainedDuplicatesAndCompleteIdsAcrossRAndRPlusOne(int limit)
    {
        using var backend = new RecordingDaprClient { PersistTypedState = true };
        var store = new DaprProjectionDeliveryStateStore(backend, Options.Create(new ProjectionOptions()),
            Options.Create(new ProjectionDeliveryIdempotencyOptions { CompletedReceiptLimit = limit }));
        (await store.TryActivateWriterProtocolAsync(new(1, 2, "fixture-writer", Now)).ConfigureAwait(true)).ShouldBeTrue();
        ProjectionDeliveryIdempotencyCoordinator coordinator = CreateCoordinator(store, receiptLimit: limit);
        ProjectionEventDto[] events = [.. Enumerable.Range(1, limit + 1).Select(sequence => Event(sequence))];
        ProjectionDeliveryReservation first = (await coordinator.TryAdmitAsync(Identity, "order-detail", events[..limit], true)
            .ConfigureAwait(true)).Reservation.ShouldNotBeNull();
        (await coordinator.CompleteAsync(Identity, "order-detail", events[..limit], first).ConfigureAwait(true))
            .ShouldBe(ProjectionDeliveryCompletion.Completed);
        ProjectionDeliveryState atR = (await store.ReadAsync(Identity, "order-detail").ConfigureAwait(true)).State!;
        atR.CompletedReceipts!.Count.ShouldBe(limit);
        atR.FirstRetainedSequence.ShouldBe(1);
        atR.CompletedMessageIdDigests!.Count.ShouldBe(limit);
        (await coordinator.TryAdmitAsync(Identity, "order-detail", [events[0]], true).ConfigureAwait(true))
            .Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.AlreadyCompleted);
        ProjectionDeliveryReservation next = (await coordinator.TryAdmitAsync(Identity, "order-detail", events, true)
            .ConfigureAwait(true)).Reservation.ShouldNotBeNull();
        (await coordinator.CompleteAsync(Identity, "order-detail", events, next).ConfigureAwait(true))
            .ShouldBe(ProjectionDeliveryCompletion.Completed);
        ProjectionDeliveryState atRPlusOne = (await store.ReadAsync(Identity, "order-detail").ConfigureAwait(true)).State!;
        atRPlusOne.CompletedReceipts!.Count.ShouldBe(limit);
        atRPlusOne.FirstRetainedSequence.ShouldBe(2);
        atRPlusOne.CompletedMessageIdDigests!.Count.ShouldBe(limit + 1);
        (await coordinator.TryAdmitAsync(Identity, "order-detail", events[..2], true).ConfigureAwait(true))
            .Disposition.ShouldBe(ProjectionDeliveryAdmissionDisposition.AlreadyCompleted);
        ProjectionDeliveryAdmissionResult old = await coordinator.TryAdmitAsync(Identity, "order-detail", [events[0]], true).ConfigureAwait(true);
        old.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryReconciliationRequired);
        async IAsyncEnumerable<ProjectionEventDto[]> Reused()
        {
            yield return [Event(limit + 2) with { MessageId = events[0].MessageId }];
            await Task.Yield();
        }
        ProjectionDeliveryFingerprintHistory suffix = await ProjectionDeliveryFingerprint.ComputeSuffixAsync(Identity, "order-detail",
            limit + 1, atRPlusOne.CompletedPrefixFingerprint!, Reused(), 10_000, CancellationToken.None).ConfigureAwait(true);
        (await coordinator.TryAdmitHistoryAsync(Identity, "order-detail", suffix, true).ConfigureAwait(true))
            .ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        ProjectionDeliveryState preserved = (await store.ReadAsync(Identity, "order-detail").ConfigureAwait(true)).State!;
        System.Text.Json.JsonSerializer.Serialize(preserved).ShouldBe(System.Text.Json.JsonSerializer.Serialize(atRPlusOne));
        preserved.CompletedMessageIdDigests.ShouldBe(atRPlusOne.CompletedMessageIdDigests);
        preserved.CompletedReceipts.ShouldBe(atRPlusOne.CompletedReceipts);
    }
}
