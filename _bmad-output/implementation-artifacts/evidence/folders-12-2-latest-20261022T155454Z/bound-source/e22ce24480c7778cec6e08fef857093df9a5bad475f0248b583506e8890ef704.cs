using System.Text.Json;

using Dapr.Actors;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Sample.Counter.State;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

public sealed partial class NamedProjectionDispatchLiveSidecarTests
{
    [Fact]
    public async Task SourceGuard_QualifiedTouchedEmptySourceStaysUnknownAndCreatesNoReceiptOrDeliveryEffects()
    {
        fixture.ThrowIfHostStopped();
        fixture.ResetTestState();
        IServiceProvider services = fixture.Services;
        var identity = new AggregateIdentity("tenant-a", LiveGuardedCounterProjectionHandler.SourceDomain,
            UniqueIdHelper.GenerateSortableUniqueStringId());
        IAggregateActor source = fixture.CreateObservedActorProxyFactory().CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId), fixture.AggregateActorTypeName);
        SourceProofInspection absent = await source.InspectSourceProofAsync().ConfigureAwait(true);
        absent.Exists.ShouldBeFalse();
        absent.Proof.ShouldBeNull();
        var loader = await LoadCatalogAsync(identity).ConfigureAwait(true);
        try
        {
            IProjectionDeliveryStateStore states = services.GetRequiredService<IProjectionDeliveryStateStore>();
            if (await states.ReadWriterProtocolAsync().ConfigureAwait(true) is null)
                (await states.TryActivateWriterProtocolAsync(new(1, 2, GuardFixtureRevision, DateTimeOffset.UtcNow)).ConfigureAwait(true)).ShouldBeTrue();
            ProjectionSourceGuardMaintenance authority = GuardFixtureMaintenance();
            SourceProofBootstrapRequest signed = await services.GetRequiredService<SourceProofBootstrapAuthority>()
                .AuthorizeAsync(new(identity.TenantId, identity.Domain, identity.AggregateId, 0, authority.OperatorId,
                    authority.CutoverCommit, authority.BackupReference, authority.SpecSha256, true, true,
                    DateTimeOffset.UtcNow, string.Empty, string.Empty), CancellationToken.None).ConfigureAwait(true);
            SourceProofInspection touched = await source.BootstrapSourceProofAsync(signed).ConfigureAwait(true);
            touched.Exists.ShouldBeTrue();
            touched.Proof.ShouldNotBeNull().HeadSequence.ShouldBe(0);
            ProjectionSourceGuardQualificationResult qualified = await services.GetRequiredService<ProjectionSourceGuardQualification>()
                .QualifyAsync(identity, LiveGuardedCounterProjectionHandler.Route, 0, touched.Proof.SourceIncarnation,
                    authority, true).ConfigureAwait(true);
            qualified.Qualified.ShouldBeTrue(qualified.ReasonCode);
            qualified.Activated.ShouldBeTrue();
            ProjectionDeliveryStateReadResult before = await states.ReadAsync(identity, LiveGuardedCounterProjectionHandler.Route).ConfigureAwait(true);
            using var cost = new ProjectionDeliveryCostListener();
            ProjectionSourceGuardDeliveryResult result = await ((ProjectionSourceGuardDelivery)services.GetRequiredService<IProjectionSourceGuardDelivery>())
                .TryDeliverAsync(identity, 256, CancellationToken.None).ConfigureAwait(true);
            cost.Dispose();
            result.Owned.ShouldBeTrue();
            result.AlreadyCurrent.ShouldBeFalse();
            result.ReasonCode.ShouldBe("empty_no_work");
            result.ProvenFreshnessVersions.ShouldBeEmpty();
            foreach (ProjectionDeliveryCost zero in new[] { ProjectionDeliveryCost.SourceRanges, ProjectionDeliveryCost.SourceEventReads,
                ProjectionDeliveryCost.HandlerAttempts, ProjectionDeliveryCost.BatchExecutions, ProjectionDeliveryCost.ModelMutations,
                ProjectionDeliveryCost.ProofWriteAttempts, ProjectionDeliveryCost.MutationAttempts,
                ProjectionDeliveryCost.SchedulerWriteAttempts, ProjectionDeliveryCost.ActivationWriteAttempts }) cost[zero].ShouldBe(0);
            ProjectionDeliveryStateReadResult after = await states.ReadAsync(identity, LiveGuardedCounterProjectionHandler.Route).ConfigureAwait(true);
            after.Etag.ShouldBe(before.Etag);
            after.State!.LastDeliveredSequence.ShouldBe(0);
            after.State.LastCompletedMessageId.ShouldBeNull();
            after.State.GuardEvidence.ShouldBeNull();
            after.State.CompletedReceipts.ShouldBeEmpty();
            after.State.CompletedMessageIdDigests.ShouldBeEmpty();
            TestContext.Current.TestOutputHelper?.WriteLine("ProjectionGuardEmptyCost " + JsonSerializer.Serialize(new
            { head = 0, checkpoint = 0, pageSize = 256, reason = result.ReasonCode,
                cost = Enum.GetValues<ProjectionDeliveryCost>().ToDictionary(static category => category.ToString(), category => cost[category]) }));
        }
        finally { await loader.StopAsync(CancellationToken.None).ConfigureAwait(true); }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(9999)]
    [InlineData(10000)]
    public async Task SourceGuard_CurrentMatrixUsesExactlyElevenMetadataReadsAndPreservesEveryOwningSurface(int head)
    {
        fixture.ThrowIfHostStopped();
        fixture.ResetTestState();
        IServiceProvider services = fixture.Services;
        int receiptLimit = services.GetRequiredService<IOptions<ProjectionDeliveryIdempotencyOptions>>().Value.CompletedReceiptLimit;
        var identity = new AggregateIdentity("tenant-a", LiveGuardedCounterProjectionHandler.SourceDomain,
            UniqueIdHelper.GenerateSortableUniqueStringId());
        IAggregateActor source = fixture.CreateObservedActorProxyFactory().CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId), fixture.AggregateActorTypeName);
        await AppendGuardedEventsAsync(identity, source, head).ConfigureAwait(true);
        var loader = await LoadCatalogAsync(identity).ConfigureAwait(true);
        try
        {
            NamedProjectionRouteCatalogEntry catalog = services.GetRequiredService<INamedProjectionRouteCatalog>().Current.Entries
                .Single(entry => entry.Domain == identity.Domain);
            await SeedGuardedBaselineAsync(identity, source, catalog, head).ConfigureAwait(true);
            SourceProof frozen = (await source.InspectSourceProofAsync().ConfigureAwait(true)).Proof.ShouldNotBeNull();
            (await services.GetRequiredService<ProjectionSourceGuardQualification>().QualifyAsync(identity,
                LiveGuardedCounterProjectionHandler.Route, head, frozen.SourceIncarnation, GuardFixtureMaintenance(), true)
                .ConfigureAwait(true)).Qualified.ShouldBeTrue();
            const string route = LiveGuardedCounterProjectionHandler.Route;
            IProjectionDeliveryStateStore states = services.GetRequiredService<IProjectionDeliveryStateStore>();
            ProjectionDeliveryStateReadResult original = await states.ReadAsync(identity, route).ConfigureAwait(true);
            original.State!.CompletedMessageIdDigests!.Count.ShouldBe(head);
            original.State.CompletedReceipts!.Count.ShouldBe(Math.Min(head, receiptLimit));
            IReadModelStore models = services.GetRequiredService<IReadModelStore>();
            string key = identity.ActorId + ":" + route;
            LiveGuardedCounterModel selected = (await models.GetAsync<LiveGuardedCounterModel>(StoreName, key).ConfigureAwait(true)).Value.ShouldNotBeNull();
            selected.Count.ShouldBe(head);
            EventEnvelope[] stored = await ReadGuardedStoredAsync(source, head).ConfigureAwait(true);
            var oracle = AggregateReplayer.Replay<CounterState>(new(identity.TenantId, identity.Domain, "Counter", identity.AggregateId,
                head, [.. stored.Select(item => new ReplayEventEnvelope(item.SequenceNumber, item.EventTypeName,
                    item.Payload, item.SerializationFormat, item.MetadataVersion, item.MessageId, item.CorrelationId, item.CausationId))], false, null));
            oracle.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
            using JsonDocument canonical = JsonDocument.Parse(oracle.StateJson.ShouldNotBeNull());
            selected.Count.ShouldBe(canonical.RootElement.GetProperty("count").GetInt32());
            var faults = services.GetRequiredService<LiveGuardedCounterFaultControl>();
            int applications = faults.Applications, preparations = faults.Preparations;
            var delivery = (ProjectionSourceGuardDelivery)services.GetRequiredService<IProjectionSourceGuardDelivery>();
            foreach (int pageSize in new[] { 1, 16, 256 })
            {
                IReadOnlyDictionary<string, long> before = await fixture.CaptureNativeAttemptsAsync().ConfigureAwait(true);
                using var cost = new ProjectionDeliveryCostListener();
                ProjectionSourceGuardDeliveryResult observed = await delivery.TryDeliverAsync(identity, pageSize, CancellationToken.None).ConfigureAwait(true);
                cost.Dispose();
                IReadOnlyDictionary<string, long> after = await fixture.CaptureNativeAttemptsAsync().ConfigureAwait(true);
                IReadOnlyDictionary<string, long> native = DaprTestContainerFixture.NativeAttemptDelta(before, after);
                DaprTestContainerFixture.AssertActorAttemptCoverage(native);
                observed.AlreadyCurrent.ShouldBeTrue(observed.ReasonCode);
                cost[ProjectionDeliveryCost.SourceInspections].ShouldBe(2);
                cost[ProjectionDeliveryCost.SourceMetadataReads].ShouldBe(2);
                cost[ProjectionDeliveryCost.ProofReads].ShouldBe(1);
                cost[ProjectionDeliveryCost.MarkerReads].ShouldBe(2);
                cost[ProjectionDeliveryCost.MetadataReads].ShouldBe(6);
                (cost[ProjectionDeliveryCost.SourceMetadataReads] + cost[ProjectionDeliveryCost.ProofReads]
                    + cost[ProjectionDeliveryCost.MarkerReads] + cost[ProjectionDeliveryCost.MetadataReads]).ShouldBe(11);
                foreach (ProjectionDeliveryCost zero in new[] { ProjectionDeliveryCost.SourceRanges, ProjectionDeliveryCost.SourceEventReads,
                    ProjectionDeliveryCost.SourceDeserializations, ProjectionDeliveryCost.HandlerAttempts, ProjectionDeliveryCost.FoldAttempts,
                    ProjectionDeliveryCost.BatchExecutions, ProjectionDeliveryCost.ModelMutations, ProjectionDeliveryCost.ProofWriteAttempts,
                    ProjectionDeliveryCost.MutationAttempts, ProjectionDeliveryCost.SchedulerWriteAttempts, ProjectionDeliveryCost.ActivationWriteAttempts })
                    cost[zero].ShouldBe(0);
                long size = 16_384L + 80L * head + 512L * Math.Min(head, receiptLimit);
                (cost[ProjectionDeliveryCost.SourceMetadataBytes] + cost[ProjectionDeliveryCost.ProofReadBytes]
                    + cost[ProjectionDeliveryCost.MarkerReadBytes] + cost[ProjectionDeliveryCost.MetadataReadBytes]).ShouldBeLessThanOrEqualTo(size + 114_688);
                native.GetValueOrDefault("primary/caller_actor_http/InspectSourceProofAsync").ShouldBe(2);
                native.GetValueOrDefault("primary/caller_actor_http/ReadMeasuredEventsBoundedRangeAsync").ShouldBe(0);
                TestContext.Current.TestOutputHelper?.WriteLine("ProjectionGuardCurrentMatrix " + JsonSerializer.Serialize(new
                { head, checkpoint = head, pageSize, receiptLimit,
                    cost = Enum.GetValues<ProjectionDeliveryCost>().ToDictionary(static category => category.ToString(), category => cost[category]), native }));
                (await states.ReadAsync(identity, route).ConfigureAwait(true)).Etag.ShouldBe(original.Etag);
                (await models.GetAsync<LiveGuardedCounterModel>(StoreName, key).ConfigureAwait(true)).Value.ShouldBe(selected);
                (await models.GetAsync<LiveGuardedCounterModel>(StoreName, key + "-index").ConfigureAwait(true)).Value.ShouldBe(selected);
                var idle = await services.GetRequiredService<IProjectionLifecycleGateway>().ReadSnapshotAsync(identity, route, CancellationToken.None).ConfigureAwait(true);
                idle.Revision.ShouldBe(original.State.GuardEvidence!.IdleLifecycleRevision);
                faults.Applications.ShouldBe(applications);
                faults.Preparations.ShouldBe(preparations);
            }
            (await source.InspectSourceProofAsync().ConfigureAwait(true)).Proof.ShouldBe(frozen);
        }
        finally { await loader.StopAsync(CancellationToken.None).ConfigureAwait(true); }
    }
}
