using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class DaprProjectionDeliveryRetrySchedulerTests
{
    [Fact]
    public async Task ActualGuardAndOwningSchedulerChargeNineFailedActivationsThenResumeTheOriginalPendingReservation()
    {
        using GuardedTailProductionHarness fixture = await GuardedTailProductionHarness.CreateAsync(3, 1,
            productionScheduler: true).ConfigureAwait(true);
        await fixture.UseProductionOwnersAsync().ConfigureAwait(true);
        DaprClient backend = fixture.Backend;
        string route = fixture.Catalog.OrdinaryProjectionTypes.Single();
        string workId = ProjectionDeliveryRetryWorkItem.CreateWorkId(fixture.Identity.TenantId, fixture.Identity.Domain,
            fixture.Identity.AggregateId, fixture.Head.HeadSequence);
        var original = new ProjectionDeliveryRetryWorkItem(workId, fixture.Identity.TenantId, fixture.Identity.Domain,
            fixture.Identity.AggregateId, fixture.Catalog.AppId, fixture.Catalog.ServiceVersion, fixture.Head.HeadSequence,
            fixture.Head.HeadMessageId!, [route], [], fixture.Head.HeadMessageId!, fixture.Catalog.CatalogFingerprint,
            0, fixture.Clock.GetUtcNow(), null);
        await fixture.Scheduler.ScheduleAsync(original).ConfigureAwait(true);
        string key = "projection-delivery-retry:ledger:v2:" + (SHA256.HashData(Encoding.UTF8.GetBytes(workId))[0] % 64)
            .ToString("x2", System.Globalization.CultureInfo.InvariantCulture);
        fixture.Http.RejectDispatch = true;
        var measurements = new List<Dictionary<string, long>>();
        EventEnvelope[] storedTail = [.. fixture.Events.Skip(1)];
        long sourceBody = JsonSerializer.SerializeToUtf8Bytes(storedTail, JsonSerializerOptions.Web).Length;
        long envelopeBytes = storedTail.Sum(static item => (long)JsonSerializer.SerializeToUtf8Bytes(item, JsonSerializerOptions.Web).Length);
        var readable = await ProjectionEventWireBuilder.BuildAsync(fixture.Protection, fixture.Identity,
            storedTail, CancellationToken.None).ConfigureAwait(true);
        long wireBody = JsonSerializer.SerializeToUtf8Bytes(readable.Events, JsonSerializerOptions.Web).Length;
        long payloadBytes = storedTail.Sum(static item => (long)item.Payload.Length);
        var owningRows = new DaprProjectionDeliveryStateStore(fixture.Backend, Options.Create(new ProjectionOptions()));
        for (int activation = 1; activation <= 9; activation++)
        {
            using (ProjectionDeliveryCostScope attempt = ProjectionDeliveryCostDiagnostics.BeginScope())
            {
                await fixture.RunPersistedRetryWorkerAsync().ConfigureAwait(true);
                attempt[ProjectionDeliveryCost.RetryActivations].ShouldBe(1);
                attempt[ProjectionDeliveryCost.SchedulerScans].ShouldBe(1);
                attempt[ProjectionDeliveryCost.SourceInspections].ShouldBeLessThanOrEqualTo(3);
                attempt[ProjectionDeliveryCost.SourceRanges].ShouldBeLessThanOrEqualTo(3);
                attempt[ProjectionDeliveryCost.SourceReturnedEvents].ShouldBeLessThanOrEqualTo(6);
                attempt[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(0);
                attempt[ProjectionDeliveryCost.ModelMutations].ShouldBe(0);
                attempt[ProjectionDeliveryCost.ProofReads].ShouldBeLessThanOrEqualTo(29);
                attempt[ProjectionDeliveryCost.ProofWriteAttempts].ShouldBeLessThanOrEqualTo(25);
                measurements.Add(Enum.GetValues<ProjectionDeliveryCost>().ToDictionary(static category => category.ToString(), category => attempt[category]));
                AssertAttemptCeilings(measurements[^1], sourceBody, envelopeBytes, wireBody, payloadBytes, 1);
            }
            // Persisted assertions use the owning SDK outside the attempt's
            // measurement interval, with no raw backend state seeding/inspection.
            ProjectionDeliveryRetryWorkItem pending = (await backend.GetStateAndETagAsync<ProjectionDeliveryRetryLedger>(
                "statestore", key).ConfigureAwait(true)).Item1.Items.Single();
            pending.Attempt.ShouldBe(Math.Min(activation, 8));
            pending.Revision.ShouldBe(activation * 2);
            pending.DispatchId.ShouldBe(original.DispatchId);
            pending.HeadMessageId.ShouldBe(original.HeadMessageId);
            pending.PendingRoutes.ShouldBe(original.PendingRoutes);
            pending.LeaseOwner.ShouldBeNull();
            pending.LastReasonCode.ShouldBe("handler_failure");
            ProjectionDeliveryState row = (await owningRows.ReadAsync(fixture.Identity, route).ConfigureAwait(true)).State.ShouldNotBeNull();
            row.LastDeliveredSequence.ShouldBe(1);
            row.CompletedMessageIdDigests.ShouldNotBeNull().Count.ShouldBe(1);
            row.ActiveReservation.ShouldNotBeNull().DispatchId.ShouldBe(original.DispatchId);
            pending.ReservationFencingTokens[route].ShouldBe(row.ActiveReservation.FencingToken);
            fixture.Clock.Advance(pending.NextDueUtc - fixture.Clock.GetUtcNow());
        }
        measurements.Count.ShouldBe(9);
        Dictionary<string, long> firstEight = measurements[0].Keys.ToDictionary(static category => category,
            category => measurements.Take(8).Sum(values => values[category]));
        Dictionary<string, long> firstNine = measurements[0].Keys.ToDictionary(static category => category,
            category => measurements.Sum(values => values[category]));
        firstEight[nameof(ProjectionDeliveryCost.RetryActivations)].ShouldBe(8);
        firstNine[nameof(ProjectionDeliveryCost.RetryActivations)].ShouldBe(9);
        AssertAttemptCeilings(firstEight, sourceBody, envelopeBytes, wireBody, payloadBytes, 8);
        AssertAttemptCeilings(firstNine, sourceBody, envelopeBytes, wireBody, payloadBytes, 9);
        fixture.Http.RejectDispatch = false;
        using (ProjectionDeliveryCostScope final = ProjectionDeliveryCostDiagnostics.BeginScope())
        {
            await fixture.RunPersistedRetryWorkerAsync().ConfigureAwait(true);
            final[ProjectionDeliveryCost.RetryActivations].ShouldBe(1);
            final[ProjectionDeliveryCost.SourceInspections].ShouldBe(3);
            final[ProjectionDeliveryCost.SourceRanges].ShouldBe(3);
            final[ProjectionDeliveryCost.SourceReturnedEvents].ShouldBe(6);
            final[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(2);
            final[ProjectionDeliveryCost.ModelMutations].ShouldBe(4);
        }
        (await backend.GetStateAndETagAsync<ProjectionDeliveryRetryLedger>("statestore", key).ConfigureAwait(true)).Item1.Items.ShouldBeEmpty();
        ProjectionDeliveryState complete = (await owningRows.ReadAsync(fixture.Identity, route).ConfigureAwait(true)).State.ShouldNotBeNull();
        complete.LastDeliveredSequence.ShouldBe(3);
        complete.CompletedMessageIdDigests.ShouldNotBeNull().Count.ShouldBe(3);
        complete.ActiveReservation.ShouldBeNull();
        complete.IdleBindingPending.ShouldBeNull();
        GuardedTailModel detail = (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route)
            .ConfigureAwait(true)).Value.ShouldNotBeNull();
        detail.Count.ShouldBe(3);
        (await fixture.Models.GetAsync<GuardedTailModel>("models", fixture.Identity.ActorId + ":" + route + "-index")
            .ConfigureAwait(true)).Value.ShouldBe(detail);
        TestContext.Current.TestOutputHelper?.WriteLine("ProjectionActualGuardRetryCosts " + JsonSerializer.Serialize(new
        { activations = 9, saturatedAttempt = 8, head = 3, checkpoint = 1, pageSize = 256,
            sourceBody, wireBody, envelopeBytes, payloadBytes, firstEight, firstNine, measurements }));
    }

    private static void AssertAttemptCeilings(IReadOnlyDictionary<string, long> values, long sourceBody,
        long envelopeBytes, long wireBody, long payloadBytes, int activations)
    {
        long Cost(ProjectionDeliveryCost category) => values[category.ToString()];
        const long proofSize = 16_384 + 80 * 3 + 512 * 3;
        const long control = 64 + 32 * 8;
        Cost(ProjectionDeliveryCost.SourceInspections).ShouldBeLessThanOrEqualTo(3L * activations);
        Cost(ProjectionDeliveryCost.SourceRanges).ShouldBeLessThanOrEqualTo(3L * activations);
        Cost(ProjectionDeliveryCost.SourceMetadataReads).ShouldBeLessThanOrEqualTo(9L * activations);
        Cost(ProjectionDeliveryCost.SourceMetadataBytes).ShouldBeLessThanOrEqualTo(9L * 4096 * activations);
        foreach (ProjectionDeliveryCost category in new[] { ProjectionDeliveryCost.SourceEventReads,
            ProjectionDeliveryCost.SourceDeserializations, ProjectionDeliveryCost.SourceReturnedEvents,
            ProjectionDeliveryCost.SourceEventHashes, ProjectionDeliveryCost.SourceChainExtensions,
            ProjectionDeliveryCost.RouteEventHashes, ProjectionDeliveryCost.WireReadabilityAttempts,
            ProjectionDeliveryCost.WireDecodedEvents, ProjectionDeliveryCost.WireEvents })
            Cost(category).ShouldBeLessThanOrEqualTo(6L * activations);
        Cost(ProjectionDeliveryCost.SourceLookaheadEvents).ShouldBe(0);
        Cost(ProjectionDeliveryCost.SourceLookaheadBytes).ShouldBe(0);
        foreach (ProjectionDeliveryCost category in new[] { ProjectionDeliveryCost.SourceResponseBytes,
            ProjectionDeliveryCost.SourceResponseSerializationBytes, ProjectionDeliveryCost.SourceResponseDeserializationBytes })
            Cost(category).ShouldBeLessThanOrEqualTo(3 * sourceBody * activations);
        Cost(ProjectionDeliveryCost.SourceDeserializationBytes).ShouldBeLessThanOrEqualTo(3 * envelopeBytes * activations);
        Cost(ProjectionDeliveryCost.SourceSizingBytes).ShouldBe(Cost(ProjectionDeliveryCost.SourceDeserializationBytes));
        Cost(ProjectionDeliveryCost.WireUnprotectionAttempts).ShouldBe(0);
        Cost(ProjectionDeliveryCost.WireReadabilityInputBytes).ShouldBeLessThanOrEqualTo(3 * payloadBytes * activations);
        Cost(ProjectionDeliveryCost.WireDecodedPayloadBytes).ShouldBeLessThanOrEqualTo(3 * payloadBytes * activations);
        Cost(ProjectionDeliveryCost.WireBytes).ShouldBeLessThanOrEqualTo(3 * wireBody * activations);
        Cost(ProjectionDeliveryCost.WireSizingBytes).ShouldBe(Cost(ProjectionDeliveryCost.WireBytes));
        Cost(ProjectionDeliveryCost.ProofReads).ShouldBeLessThanOrEqualTo(29L * activations);
        Cost(ProjectionDeliveryCost.ProofWriteAttempts).ShouldBeLessThanOrEqualTo(25L * activations);
        Cost(ProjectionDeliveryCost.ProofReadBytes).ShouldBeLessThanOrEqualTo(29 * proofSize * activations);
        Cost(ProjectionDeliveryCost.ProofWriteBytes).ShouldBeLessThanOrEqualTo(25 * proofSize * activations);
        Cost(ProjectionDeliveryCost.MarkerReads).ShouldBe(2 * Cost(ProjectionDeliveryCost.ProofReads));
        Cost(ProjectionDeliveryCost.MarkerReadBytes).ShouldBeLessThanOrEqualTo(58L * 4096 * activations);
        Cost(ProjectionDeliveryCost.ProofVisits).ShouldBeLessThanOrEqualTo(80L * (3 + 3 + 2) * activations);
        (Cost(ProjectionDeliveryCost.MetadataReads) + Cost(ProjectionDeliveryCost.SchedulerShardReads) - 64L * activations)
            .ShouldBeLessThanOrEqualTo(control * activations);
        (Cost(ProjectionDeliveryCost.MutationAttempts) + Cost(ProjectionDeliveryCost.SchedulerWriteAttempts))
            .ShouldBeLessThanOrEqualTo(control * activations);
        Cost(ProjectionDeliveryCost.MetadataReadBytes).ShouldBeLessThanOrEqualTo(16_384 * Cost(ProjectionDeliveryCost.MetadataReads));
        Cost(ProjectionDeliveryCost.MutationBytes).ShouldBeLessThanOrEqualTo(16_384 * Cost(ProjectionDeliveryCost.MutationAttempts));
        Cost(ProjectionDeliveryCost.SchedulerScans).ShouldBe(activations);
        Cost(ProjectionDeliveryCost.SchedulerShardReads).ShouldBeLessThanOrEqualTo(84L * activations);
        Cost(ProjectionDeliveryCost.SchedulerReadBytes).ShouldBeLessThanOrEqualTo(21L * (512 + 4096) * activations);
        Cost(ProjectionDeliveryCost.SchedulerWriteBytes).ShouldBeLessThanOrEqualTo(20L * (512 + 4096) * activations);
        Cost(ProjectionDeliveryCost.HandlerAttempts).ShouldBe(0);
        Cost(ProjectionDeliveryCost.FoldAttempts).ShouldBe(0);
        Cost(ProjectionDeliveryCost.BatchExecutions).ShouldBe(0);
        Cost(ProjectionDeliveryCost.ModelMutations).ShouldBe(0);
    }
}
