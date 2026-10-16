using System.Text.Json;

using Dapr.Actors;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Sample.Counter.State;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

public sealed partial class NamedProjectionDispatchLiveSidecarTests
{
    [Theory]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(771)]
    [InlineData(10000)]
    public async Task ReplayRecovery_GenericHandlerReusesOneOriginalManifestAcrossThreeLostDurableResponses(int head)
    {
        fixture.ThrowIfHostStopped();
        fixture.ResetTestState();
        IServiceProvider services = fixture.Services;
        var identity = new AggregateIdentity("tenant-a", LiveReplayCounterProjectionHandler.SourceDomain,
            UniqueIdHelper.GenerateSortableUniqueStringId());
        IAggregateActor source = fixture.CreateObservedActorProxyFactory().CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId), fixture.AggregateActorTypeName);
        await AppendGuardedEventsAsync(identity, source, head).ConfigureAwait(true);
        var loader = await LoadCatalogAsync(identity).ConfigureAwait(true);
        try
        {
            NamedProjectionRouteCatalogEntry catalog = services.GetRequiredService<INamedProjectionRouteCatalog>().Current.Entries
                .Single(entry => entry.Domain == identity.Domain);
            catalog.SourceGuardCapabilities.ShouldBeEmpty();
            catalog.ReplayRecoveryBindings[LiveReplayCounterProjectionHandler.Route].IsSupported.ShouldBeTrue();
            catalog.CatalogFingerprint.ShouldBe(ProjectionRouteCatalogFingerprint.Compute(fixture.AppId, "v1",
                [new(identity.Domain, LiveReplayCounterProjectionHandler.Route)]));
            EventEnvelope[] stored = await ReadGuardedStoredAsync(source, head).ConfigureAwait(true);
            ProjectionEventDto[] events = (await ProjectionEventWireBuilder.BuildAsync(services.GetRequiredService<IEventPayloadProtectionService>(),
                identity, stored, CancellationToken.None).ConfigureAwait(true)).Events.ShouldNotBeNull();
            var faults = services.GetRequiredService<LiveReplayCounterFaultControl>();
            int preparations = faults.Preparations;
            var coordinator = services.GetRequiredService<INamedProjectionDispatchCoordinator>();
            var rows = services.GetRequiredService<IProjectionDeliveryStateStore>();
            var scheduler = services.GetRequiredService<IProjectionDeliveryRetryScheduler>();
            string workId = ProjectionDeliveryRetryWorkItem.CreateWorkId(identity.TenantId, identity.Domain, identity.AggregateId, head);
            foreach (DomainProjectionRebuildBatchAction boundary in new[] { DomainProjectionRebuildBatchAction.Stage,
                DomainProjectionRebuildBatchAction.Commit, DomainProjectionRebuildBatchAction.Verify })
            {
                faults.LoseOnce(boundary);
                using var cost = new ProjectionDeliveryCostListener();
                (await coordinator.TryDispatchHeadAsync(identity, Registration(identity), head, stored[^1].MessageId,
                    events, CancellationToken.None).ConfigureAwait(true)).ShouldBeTrue();
                cost.Dispose();
                faults.Preparations.ShouldBe(preparations + 1);
                (await rows.ReadAsync(identity, LiveReplayCounterProjectionHandler.Route).ConfigureAwait(true)).State!
                    .LastDeliveredSequence.ShouldBe(0);
                ProjectionDeliveryRetryWorkItem original = (await scheduler.GetDueAsync(DateTimeOffset.MaxValue, 64).ConfigureAwait(true))
                    .Single(item => item.WorkId == workId);
                original.DispatchId.ShouldBe(stored[^1].MessageId);
                original.PendingRoutes.ShouldBe([LiveReplayCounterProjectionHandler.Route]);
                original.ReplayRecoveryBindings.ShouldNotBeNull().ShouldHaveSingleItem();
                long size = 16_384L + 80L * head + 512L * Math.Min(head, 256);
                cost[ProjectionDeliveryCost.ProofReads].ShouldBeLessThanOrEqualTo(8);
                cost[ProjectionDeliveryCost.ProofWriteAttempts].ShouldBeLessThanOrEqualTo(4);
                cost[ProjectionDeliveryCost.ProofReadBytes].ShouldBeLessThanOrEqualTo(8 * size);
                cost[ProjectionDeliveryCost.ProofWriteBytes].ShouldBeLessThanOrEqualTo(4 * size);
                cost[ProjectionDeliveryCost.ProofVisits].ShouldBeLessThanOrEqualTo(24L * (2L * head + Math.Min(head, 256)));
                cost[ProjectionDeliveryCost.MetadataReads].ShouldBeLessThanOrEqualTo(128);
                cost[ProjectionDeliveryCost.MutationAttempts].ShouldBeLessThanOrEqualTo(128);
                cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(boundary == DomainProjectionRebuildBatchAction.Stage ? 1 : 0);
                cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(boundary == DomainProjectionRebuildBatchAction.Commit ? 2 : 0);
                TestContext.Current.TestOutputHelper?.WriteLine("ProjectionReplayRecoveryAttempt " + JsonSerializer.Serialize(new
                { head, activation = boundary.ToString(), mode = "generic_full_replay", counters = Enum.GetValues<ProjectionDeliveryCost>()
                    .ToDictionary(static item => item.ToString(), item => cost[item]) }));
            }
            using (var cost = new ProjectionDeliveryCostListener())
            {
                (await coordinator.TryDispatchHeadAsync(identity, Registration(identity), head, stored[^1].MessageId,
                    events, CancellationToken.None).ConfigureAwait(true)).ShouldBeTrue();
                faults.Preparations.ShouldBe(preparations + 1);
                cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(0);
                cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(0);
            }
            ProjectionDeliveryState completed = (await rows.ReadAsync(identity, LiveReplayCounterProjectionHandler.Route).ConfigureAwait(true)).State.ShouldNotBeNull();
            completed.LastDeliveredSequence.ShouldBe(head);
            completed.CompletedMessageIdDigests.ShouldNotBeNull().Count.ShouldBe(head);
            completed.ActiveReservation.ShouldBeNull();
            (await scheduler.GetDueAsync(DateTimeOffset.MaxValue, 64).ConfigureAwait(true)).ShouldNotContain(item => item.WorkId == workId);
            IReadModelStore models = services.GetRequiredService<IReadModelStore>();
            string key = identity.ActorId + ":" + LiveReplayCounterProjectionHandler.Route;
            LiveReplayCounterModel detail = (await models.GetAsync<LiveReplayCounterModel>(StoreName, key).ConfigureAwait(true)).Value.ShouldNotBeNull();
            (await models.GetAsync<LiveReplayCounterModel>(StoreName, key + "-index").ConfigureAwait(true)).Value.ShouldBe(detail);
            detail.ProjectionVersion.ShouldBe(head.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var oracle = AggregateReplayer.Replay<CounterState>(new(identity.TenantId, identity.Domain, "Counter", identity.AggregateId,
                head, [.. stored.Select(item => new ReplayEventEnvelope(item.SequenceNumber, item.EventTypeName, item.Payload,
                    item.SerializationFormat, item.MetadataVersion, item.MessageId, item.CorrelationId, item.CausationId))], false, null));
            oracle.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
            using JsonDocument canonical = JsonDocument.Parse(oracle.StateJson.ShouldNotBeNull());
            detail.Count.ShouldBe(canonical.RootElement.GetProperty("count").GetInt32());
            var request = new ProjectionDispatchRequest(new(identity.TenantId, identity.Domain, identity.AggregateId, events),
                [LiveReplayCounterProjectionHandler.Route], stored[^1].MessageId, catalog.CatalogFingerprint);
            DomainProjectionReplayRecoveryManifest tombstone = (await models.GetAsync<DomainProjectionReplayRecoveryManifest>(StoreName,
                DomainProjectionReplayRecovery.Key(request)).ConfigureAwait(true)).Value.ShouldNotBeNull();
            tombstone.Manifest.ShouldBeEmpty();
        }
        finally { await loader.StopAsync(CancellationToken.None).ConfigureAwait(true); }
    }
}
