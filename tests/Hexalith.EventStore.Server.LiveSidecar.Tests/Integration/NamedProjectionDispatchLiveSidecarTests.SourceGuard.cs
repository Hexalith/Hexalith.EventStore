using System.Security.Cryptography;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.Commons.UniqueIds;
using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Sample.Counter.Events;
using Hexalith.EventStore.Sample.Counter.State;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Pipeline;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Testing.Builders;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using IEventPayload = Hexalith.EventStore.Contracts.Events.IEventPayload;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

public sealed partial class NamedProjectionDispatchLiveSidecarTests
{
    private const string GuardFixtureRevision = "e17e7294f540ebba42fe25192998689f0ecd89c1";

    [Fact]
    public async Task SourceGuard_ActivationWorkerClosesActualAppendOutboxWithThreeSourceInspections()
    {
        fixture.ThrowIfHostStopped();
        fixture.ResetTestState();
        IServiceProvider services = fixture.Services;
        var identity = new AggregateIdentity("tenant-a", LiveGuardedCounterProjectionHandler.SourceDomain,
            UniqueIdHelper.GenerateSortableUniqueStringId());
        IAggregateActor source = fixture.CreateObservedActorProxyFactory().CreateActorProxy<IAggregateActor>(
            new ActorId(identity.ActorId), fixture.AggregateActorTypeName);
        await AppendGuardedEventsAsync(identity, source, 1).ConfigureAwait(true);
        var loader = await LoadCatalogAsync(identity).ConfigureAwait(true);
        try
        {
            NamedProjectionRouteCatalogEntry catalog = services.GetRequiredService<INamedProjectionRouteCatalog>().Current.Entries
                .Single(entry => entry.Domain == identity.Domain);
            await SeedGuardedBaselineAsync(identity, source, catalog, 1).ConfigureAwait(true);
            SourceProof baseline = (await source.InspectSourceProofAsync().ConfigureAwait(true)).Proof.ShouldNotBeNull();
            (await services.GetRequiredService<ProjectionSourceGuardQualification>().QualifyAsync(identity,
                LiveGuardedCounterProjectionHandler.Route, 1, baseline.SourceIncarnation, GuardFixtureMaintenance(), true).ConfigureAwait(true))
                .Qualified.ShouldBeTrue();
            IProjectionActivationOutbox outbox = services.GetRequiredService<IProjectionActivationOutbox>();
            // Direct actor calls bypass the command gateway's write-ahead
            // activation. Use the owning submission/router path and leave its
            // optional immediate callback quiesced for the scheduled worker.
            var submit = new SubmitCommandHandler(services.GetRequiredService<ICommandStatusStore>(),
                new DaprCommandArchiveStore(services.GetRequiredService<DaprClient>(), Options.Create(new CommandStatusOptions
                    { StateStoreName = StoreName }), NullLogger<DaprCommandArchiveStore>.Instance),
                new CommandRouter(fixture.CreateObservedActorProxyFactory(), Options.Create(new EventStoreActorOptions
                    { AggregateActorTypeName = fixture.AggregateActorTypeName }), NullLogger<CommandRouter>.Instance),
                null, null, new NoOpProjectionUpdateOrchestrator(), NullLogger<SubmitCommandHandler>.Instance,
                projectionActivationOutbox: outbox);
            string message = UniqueIdHelper.GenerateSortableUniqueStringId();
            await submit.Handle(new SubmitCommand(message, identity.TenantId, identity.Domain, identity.AggregateId,
                "IncrementCounter", "{}"u8.ToArray(), message, "user-1"), CancellationToken.None).ConfigureAwait(true);
            (await source.InspectSourceProofAsync().ConfigureAwait(true)).CurrentSequence.ShouldBe(2);
            (await outbox.GetAsync(identity).ConfigureAwait(true)).ShouldNotBeNull();
            using var worker = new ProjectionActivationWorker(outbox, services.GetRequiredService<IProjectionUpdateOrchestrator>(),
                services.GetRequiredService<IOptions<ProjectionDispatchOptions>>(), services.GetRequiredService<TimeProvider>(),
                services.GetRequiredService<IProjectionSourceGuardDelivery>());
            IReadOnlyDictionary<string, long> nativeBefore = await fixture.CaptureNativeAttemptsAsync().ConfigureAwait(true);
            using var cost = new ProjectionDeliveryCostListener();
            await worker.RunOnceAsync(CancellationToken.None).ConfigureAwait(true);
            cost.Dispose();
            IReadOnlyDictionary<string, long> nativeAfter = await fixture.CaptureNativeAttemptsAsync().ConfigureAwait(true);
            IReadOnlyDictionary<string, long> nativeDelta = DaprTestContainerFixture.NativeAttemptDelta(nativeBefore, nativeAfter);
            DaprTestContainerFixture.AssertActorAttemptCoverage(nativeDelta);
            cost[ProjectionDeliveryCost.SourceInspections].ShouldBe(3);
            cost[ProjectionDeliveryCost.SourceRanges].ShouldBe(3);
            cost[ProjectionDeliveryCost.SourceReturnedEvents].ShouldBe(3);
            cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(1);
            cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(2);
            (await outbox.GetAsync(identity).ConfigureAwait(true)).ShouldBeNull();
            ProjectionDeliveryState completed = (await services.GetRequiredService<IProjectionDeliveryStateStore>()
                .ReadAsync(identity, LiveGuardedCounterProjectionHandler.Route).ConfigureAwait(true)).State.ShouldNotBeNull();
            completed.LastDeliveredSequence.ShouldBe(2);
            completed.ActiveReservation.ShouldBeNull();
            completed.IdleBindingPending.ShouldBeNull();
            TestContext.Current.TestOutputHelper?.WriteLine("ProjectionGuardActivationClosure " + JsonSerializer.Serialize(new
            { head = 2, checkpoint = 1, outboxCompleted = true, attempts = nativeDelta,
                counters = Enum.GetValues<ProjectionDeliveryCost>().ToDictionary(static item => item.ToString(), item => cost[item]) }));
        }
        finally { await loader.StopAsync(CancellationToken.None).ConfigureAwait(true); }
    }

    [Fact]
    public async Task SourceGuard_RealActorOversizedEnvelopeReturnsTypedPageFailureBeforeAnyEffects()
    {
        fixture.ThrowIfHostStopped();
        fixture.ResetTestState();
        IServiceProvider services = fixture.Services;
        var identity = new AggregateIdentity("tenant-a", LiveGuardedCounterProjectionHandler.SourceDomain,
            UniqueIdHelper.GenerateSortableUniqueStringId());
        var proxies = fixture.CreateObservedActorProxyFactory();
        IAggregateActor source = proxies.CreateActorProxy<IAggregateActor>(new ActorId(identity.ActorId), fixture.AggregateActorTypeName);
        await AppendGuardedEventsAsync(identity, source, 1).ConfigureAwait(true);
        var loader = await LoadCatalogAsync(identity).ConfigureAwait(true);
        try
        {
            NamedProjectionRouteCatalogEntry catalog = services.GetRequiredService<INamedProjectionRouteCatalog>().Current.Entries
                .Single(entry => entry.Domain == identity.Domain);
            await SeedGuardedBaselineAsync(identity, source, catalog, 1).ConfigureAwait(true);
            SourceProof baseline = (await source.InspectSourceProofAsync().ConfigureAwait(true)).Proof.ShouldNotBeNull();
            (await services.GetRequiredService<ProjectionSourceGuardQualification>().QualifyAsync(identity,
                LiveGuardedCounterProjectionHandler.Route, 1, baseline.SourceIncarnation, GuardFixtureMaintenance(), true).ConfigureAwait(true))
                .Qualified.ShouldBeTrue();
            fixture.DomainServiceInvoker.SetupResponse("IncrementCounter", DomainResult.Success([new CounterIncremented()]));
            // Keep the actual source event inside the ordinary actor transport limit while
            // making its stored envelope exceed this story's exact 2 MiB page ceiling.
            (await source.ProcessCommandAsync(new CommandEnvelopeBuilder().WithTenantId(identity.TenantId).WithDomain(identity.Domain)
                .WithAggregateId(identity.AggregateId).WithCommandType("IncrementCounter").WithUserId(new string('u', 2_097_152))
                .Build()).ConfigureAwait(true)).Accepted.ShouldBeTrue();
            IProjectionDeliveryStateStore rows = services.GetRequiredService<IProjectionDeliveryStateStore>();
            ProjectionDeliveryStateReadResult before = await rows.ReadAsync(identity, LiveGuardedCounterProjectionHandler.Route).ConfigureAwait(true);
            var faults = services.GetRequiredService<LiveGuardedCounterFaultControl>();
            int applications = faults.Applications, preparations = faults.Preparations;
            IReadOnlyDictionary<string, long> nativeBefore = await fixture.CaptureNativeAttemptsAsync().ConfigureAwait(true);
            using var cost = new ProjectionDeliveryCostListener();
            SourceEventRange failure = await source.ReadMeasuredEventsBoundedRangeAsync(1, 2, 256, 2_097_152).ConfigureAwait(true);
            failure.ReasonCode.ShouldBe("delivery_page_limit_exceeded");
            failure.Events.ShouldBeEmpty();
            failure.EnvelopeArrayBytes.ShouldBe(2);
            ProjectionSourceGuardDeliveryResult delivered = await ((ProjectionSourceGuardDelivery)services
                .GetRequiredService<IProjectionSourceGuardDelivery>()).TryDeliverAsync(identity, 256, CancellationToken.None).ConfigureAwait(true);
            delivered.ReasonCode.ShouldBe("delivery_page_limit_exceeded");
            delivered.AlreadyCurrent.ShouldBeFalse();
            IReadOnlyDictionary<string, long> nativeAfter = await fixture.CaptureNativeAttemptsAsync().ConfigureAwait(true);
            TestContext.Current.TestOutputHelper?.WriteLine("ProjectionGuardNativeAttempts " + JsonSerializer.Serialize(new
            { head = 2, checkpoint = 1, mode = "oversized_range_rejection",
                attempts = DaprTestContainerFixture.NativeAttemptDelta(nativeBefore, nativeAfter) }));
            nativeAfter.Keys.ShouldContain(key => key.Contains("/native/", StringComparison.Ordinal));
            IReadOnlyDictionary<string, long> nativeDelta = DaprTestContainerFixture.NativeAttemptDelta(nativeBefore, nativeAfter);
            DaprTestContainerFixture.AssertActorAttemptCoverage(nativeDelta);
            // An actual tail keeps its remaining source samples around final
            // completion; rejected admission needs no redundant second sample.
            nativeDelta["primary/sdk_http/actor_method"].ShouldBe(5);
            nativeDelta["primary/sdk_http/actor_state_get"].ShouldBeGreaterThan(0);
            nativeDelta.GetValueOrDefault("primary/sdk_http/actor_state_transaction").ShouldBe(0);
            ProjectionDeliveryStateReadResult after = await rows.ReadAsync(identity, LiveGuardedCounterProjectionHandler.Route).ConfigureAwait(true);
            JsonSerializer.Serialize(after.State, JsonSerializerOptions.Web).ShouldBe(JsonSerializer.Serialize(before.State, JsonSerializerOptions.Web));
            after.Etag.ShouldBe(before.Etag);
            faults.Applications.ShouldBe(applications);
            faults.Preparations.ShouldBe(preparations);
            cost[ProjectionDeliveryCost.SourceRanges].ShouldBe(2);
            cost[ProjectionDeliveryCost.SourceReturnedEvents].ShouldBe(0);
            cost[ProjectionDeliveryCost.SourceDeserializations].ShouldBe(2);
            cost[ProjectionDeliveryCost.SourceDeserializationBytes].ShouldBeGreaterThan(2L * 2_097_152);
            cost[ProjectionDeliveryCost.SourceResponseDeserializationBytes].ShouldBe(2);
            cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(0);
            TestContext.Current.TestOutputHelper?.WriteLine("ProjectionGuardTypedFailure " + JsonSerializer.Serialize(new
            { reason = delivered.ReasonCode, head = 2, checkpoint = 1, maxPageBytes = 2_097_152,
                counters = Enum.GetValues<ProjectionDeliveryCost>().ToDictionary(static item => item.ToString(), item => cost[item]) }));
        }
        finally { await loader.StopAsync(CancellationToken.None).ConfigureAwait(true); }
    }

    [Fact]
    public async Task SourceGuard_TwoRealReplicasResumeOriginalCommittedEventReceiptsWithoutRepeatingEffects()
    {
        fixture.ThrowIfHostStopped();
        fixture.ResetTestState();
        IServiceProvider primary = fixture.Services;
        var identity = new AggregateIdentity("tenant-a", LiveGuardedCounterProjectionHandler.SourceDomain,
            UniqueIdHelper.GenerateSortableUniqueStringId());
        var proxies = fixture.CreateObservedActorProxyFactory();
        IAggregateActor source = proxies.CreateActorProxy<IAggregateActor>(new ActorId(identity.ActorId), fixture.AggregateActorTypeName);
        await AppendGuardedEventsAsync(identity, source, 1).ConfigureAwait(true);
        var loader = await LoadCatalogAsync(identity).ConfigureAwait(true);
        bool replicaStarted = false;
        try
        {
            NamedProjectionRouteCatalogEntry catalog = primary.GetRequiredService<INamedProjectionRouteCatalog>().Current.Entries
                .Single(entry => entry.Domain == identity.Domain);
            await SeedGuardedBaselineAsync(identity, source, catalog, 1).ConfigureAwait(true);
            SourceProof baseline = (await source.InspectSourceProofAsync().ConfigureAwait(true)).Proof.ShouldNotBeNull();
            (await primary.GetRequiredService<ProjectionSourceGuardQualification>().QualifyAsync(identity,
                LiveGuardedCounterProjectionHandler.Route, 1, baseline.SourceIncarnation, GuardFixtureMaintenance(), true).ConfigureAwait(true))
                .Qualified.ShouldBeTrue();
            await fixture.EnsureReplicaAsync().ConfigureAwait(true);
            replicaStarted = true;
            IServiceProvider replica = fixture.ReplicaServices;
            replica.GetRequiredService<INamedProjectionRouteCatalog>().Upsert([catalog]);
            await AppendGuardedEventsAsync(identity, source, 3).ConfigureAwait(true);
            var primaryFaults = primary.GetRequiredService<LiveGuardedCounterFaultControl>();
            var replicaFaults = replica.GetRequiredService<LiveGuardedCounterFaultControl>();
            int calls = primaryFaults.Applications + replicaFaults.Applications;
            primaryFaults.LoseResponseAfter(3);
            replicaFaults.LoseResponseAfter(3);
            IReadOnlyDictionary<string, long> nativeBefore = await fixture.CaptureNativeAttemptsAsync().ConfigureAwait(true);
            var first = (ProjectionSourceGuardDelivery)primary.GetRequiredService<IProjectionSourceGuardDelivery>();
            (await first.TryDeliverAsync(identity, 256, CancellationToken.None).ConfigureAwait(true)).AlreadyCurrent.ShouldBeFalse();
            const string route = LiveGuardedCounterProjectionHandler.Route;
            IProjectionDeliveryStateStore rows = primary.GetRequiredService<IProjectionDeliveryStateStore>();
            ProjectionDeliveryState pending = (await rows.ReadAsync(identity, route).ConfigureAwait(true)).State.ShouldNotBeNull();
            pending.LastDeliveredSequence.ShouldBe(1);
            ProjectionDeliveryReservation original = pending.ActiveReservation.ShouldNotBeNull();
            IReadModelStore models = primary.GetRequiredService<IReadModelStore>();
            string key = identity.ActorId + ":" + route;
            LiveGuardedCounterModel selected = (await models.GetAsync<LiveGuardedCounterModel>(StoreName, key).ConfigureAwait(true)).Value.ShouldNotBeNull();
            selected.Count.ShouldBe(3);
            selected.ProjectionOwner.AppliedSequence.ShouldBe(3);
            var scope = new ProjectionOwnerProofRequest(identity.TenantId, identity.Domain, identity.AggregateId, route, catalog.CatalogFingerprint);
            var receiptRequest = new ProjectionAppliedReceiptRequest(scope, 3, selected.ProjectionOwner.MessageId,
                selected.ProjectionOwner.OwnerIdentity, selected.ProjectionOwner.OwnerGeneration);
            var transport = primary.GetRequiredService<IProjectionSourceGuardTransport>();
            ProjectionOwnerProof originalReceipt = (await transport.ReadAppliedReceiptAsync(catalog, receiptRequest, CancellationToken.None)
                .ConfigureAwait(true)).Proof.ShouldNotBeNull();
            primaryFaults.Applications.ShouldBeLessThanOrEqualTo(calls + 2);
            var second = (ProjectionSourceGuardDelivery)replica.GetRequiredService<IProjectionSourceGuardDelivery>();
            using var recoveryCost = new ProjectionDeliveryCostListener();
            ProjectionSourceGuardDeliveryResult recovered = await second.TryDeliverAsync(identity, 256, CancellationToken.None).ConfigureAwait(true);
            recoveryCost.Dispose();
            recovered.AlreadyCurrent.ShouldBeTrue(recovered.ReasonCode);
            recoveryCost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(1);
            recoveryCost[ProjectionDeliveryCost.ModelMutations].ShouldBe(2);
            (primaryFaults.Applications + replicaFaults.Applications).ShouldBe(calls + 3);
            ProjectionDeliveryState completed = (await rows.ReadAsync(identity, route).ConfigureAwait(true)).State.ShouldNotBeNull();
            completed.LastDeliveredSequence.ShouldBe(4);
            completed.LastCompletedMessageId.ShouldBe(original.DispatchId);
            completed.ActiveReservation.ShouldBeNull();
            completed.IdleBindingPending.ShouldBeNull();
            (await transport.ReadAppliedReceiptAsync(catalog, receiptRequest, CancellationToken.None).ConfigureAwait(true)).Proof.ShouldBe(originalReceipt);
            LiveGuardedCounterModel detail = (await models.GetAsync<LiveGuardedCounterModel>(StoreName, key).ConfigureAwait(true)).Value.ShouldNotBeNull();
            detail.Count.ShouldBe(4);
            (await models.GetAsync<LiveGuardedCounterModel>(StoreName, key + "-index").ConfigureAwait(true)).Value.ShouldBe(detail);
            (await first.InspectCurrentAsync(identity, CancellationToken.None).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
            (await second.InspectCurrentAsync(identity, CancellationToken.None).ConfigureAwait(true)).AlreadyCurrent.ShouldBeTrue();
            IReadOnlyDictionary<string, long> nativeAfter = await fixture.CaptureNativeAttemptsAsync().ConfigureAwait(true);
            IReadOnlyDictionary<string, long> nativeDelta = DaprTestContainerFixture.NativeAttemptDelta(nativeBefore, nativeAfter);
            DaprTestContainerFixture.AssertActorAttemptCoverage(nativeDelta);
            TestContext.Current.TestOutputHelper?.WriteLine("ProjectionGuardNativeAttempts " + JsonSerializer.Serialize(new
            { head = 4, checkpoint = 1, mode = "two_replica_original_receipt_recovery", attempts = nativeDelta }));
        }
        finally
        {
            if (replicaStarted) await fixture.StopReplicaHostAndSidecarAsync().ConfigureAwait(true);
            await loader.StopAsync(CancellationToken.None).ConfigureAwait(true);
        }
    }

    [Theory]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 1)]
    [InlineData(1, 256, 256)]
    [InlineData(1, 257, 256)]
    [InlineData(1, 771, 256)]
    [InlineData(9000, 771, 256)]
    [InlineData(9999, 1, 256)]
    public async Task SourceGuard_RealActorHandlerAndStores_ProveTailThenReadOnlyCurrent(int completed, int delta, int pageSize)
    {
        fixture.ThrowIfHostStopped();
        fixture.ResetTestState();
        IServiceProvider services = fixture.Services;
        int receiptLimit = services.GetRequiredService<IOptions<ProjectionDeliveryIdempotencyOptions>>().Value.CompletedReceiptLimit;
        var identity = new AggregateIdentity("tenant-a", LiveGuardedCounterProjectionHandler.SourceDomain,
            UniqueIdHelper.GenerateSortableUniqueStringId());
        var proxies = fixture.CreateObservedActorProxyFactory();
        IAggregateActor source = proxies.CreateActorProxy<IAggregateActor>(new ActorId(identity.ActorId), fixture.AggregateActorTypeName);
        await AppendGuardedEventsAsync(identity, source, completed).ConfigureAwait(true);
        var loader = await LoadCatalogAsync(identity).ConfigureAwait(true);
        try
        {
            NamedProjectionRouteCatalogEntry catalog = services.GetRequiredService<INamedProjectionRouteCatalog>().Current.Entries
                .Single(entry => entry.Domain == identity.Domain);
            catalog.SourceGuardCapabilities[LiveGuardedCounterProjectionHandler.Route].ShouldBe(LiveGuardedCounterProjectionHandler.Capability);
            await SeedGuardedBaselineAsync(identity, source, catalog, completed).ConfigureAwait(true);
            SourceProof baseline = (await source.InspectSourceProofAsync().ConfigureAwait(true)).Proof.ShouldNotBeNull();
            ProjectionSourceGuardQualificationResult qualified = await services.GetRequiredService<ProjectionSourceGuardQualification>()
                .QualifyAsync(identity, LiveGuardedCounterProjectionHandler.Route, completed, baseline.SourceIncarnation,
                    GuardFixtureMaintenance(), true).ConfigureAwait(true);
            qualified.Qualified.ShouldBeTrue(qualified.ReasonCode);
            qualified.Activated.ShouldBeTrue();
            if (delta > 0) await AppendGuardedEventsAsync(identity, source, delta).ConfigureAwait(true);
            SourceProof frozen = (await source.InspectSourceProofAsync().ConfigureAwait(true)).Proof.ShouldNotBeNull();
            var faults = services.GetRequiredService<LiveGuardedCounterFaultControl>();
            int applications = faults.Applications, preparations = faults.Preparations;
            var delivery = (ProjectionSourceGuardDelivery)services.GetRequiredService<IProjectionSourceGuardDelivery>();
            IReadOnlyDictionary<string, long> nativeBefore = await fixture.CaptureNativeAttemptsAsync().ConfigureAwait(true);
            using var tailCost = new ProjectionDeliveryCostListener();
            ProjectionSourceGuardDeliveryResult delivered = await delivery.TryDeliverAsync(identity, pageSize, CancellationToken.None).ConfigureAwait(true);
            tailCost.Dispose();
            IReadOnlyDictionary<string, long> nativeAfter = await fixture.CaptureNativeAttemptsAsync().ConfigureAwait(true);
            IReadOnlyDictionary<string, long> nativeDelta = DaprTestContainerFixture.NativeAttemptDelta(nativeBefore, nativeAfter);
            DaprTestContainerFixture.AssertActorAttemptCoverage(nativeDelta);
            nativeDelta.GetValueOrDefault("primary/caller_actor_http/ReadMeasuredEventsBoundedRangeAsync")
                .ShouldBe(tailCost[ProjectionDeliveryCost.SourceRanges]);
            nativeDelta.GetValueOrDefault("primary/caller_actor_http/InspectSourceProofAsync")
                .ShouldBe(tailCost[ProjectionDeliveryCost.SourceInspections]);
            TestContext.Current.TestOutputHelper?.WriteLine("ProjectionGuardNativeAttempts " + JsonSerializer.Serialize(new
            { head = completed + delta, checkpoint = completed, pageSize,
                attempts = nativeDelta }));
            delivered.AlreadyCurrent.ShouldBeTrue(delivered.ReasonCode);
            faults.Applications.ShouldBe(applications + delta);
            faults.Preparations.ShouldBe(preparations);
            int pages = (delta + pageSize - 1) / pageSize;
            tailCost[ProjectionDeliveryCost.SourceRanges].ShouldBeLessThanOrEqualTo(3L * pages);
            tailCost[ProjectionDeliveryCost.SourceReturnedEvents].ShouldBeLessThanOrEqualTo(3L * delta);
            tailCost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(delta);
            tailCost[ProjectionDeliveryCost.ModelMutations].ShouldBe(2L * delta);
            tailCost[ProjectionDeliveryCost.SourceLookaheadEvents].ShouldBe(0);
            tailCost[ProjectionDeliveryCost.ProofWriteAttempts].ShouldBe(delta == 0 ? 0 : 3);
            tailCost[ProjectionDeliveryCost.SourceInspections].ShouldBeLessThanOrEqualTo(3);
            tailCost[ProjectionDeliveryCost.SourceMetadataReads].ShouldBeLessThanOrEqualTo(3 + 6L * pages);
            tailCost[ProjectionDeliveryCost.SourceEventReads].ShouldBeLessThanOrEqualTo(3L * delta);
            tailCost[ProjectionDeliveryCost.SourceDeserializations].ShouldBeLessThanOrEqualTo(3L * delta);
            tailCost[ProjectionDeliveryCost.SourceEventHashes].ShouldBeLessThanOrEqualTo(3L * delta);
            tailCost[ProjectionDeliveryCost.SourceChainExtensions].ShouldBeLessThanOrEqualTo(3L * delta);
            tailCost[ProjectionDeliveryCost.RouteEventHashes].ShouldBeLessThanOrEqualTo(3L * delta);
            tailCost[ProjectionDeliveryCost.BatchExecutions].ShouldBe(delta);
            tailCost[ProjectionDeliveryCost.ProofReads].ShouldBeLessThanOrEqualTo(8);
            tailCost[ProjectionDeliveryCost.MarkerReads].ShouldBe(2 * tailCost[ProjectionDeliveryCost.ProofReads]);
            long control = 64 + 32 + 32L * delta;
            tailCost[ProjectionDeliveryCost.MetadataReads].ShouldBeLessThanOrEqualTo(control);
            tailCost[ProjectionDeliveryCost.MutationAttempts].ShouldBeLessThanOrEqualTo(control);
            long size = 16_384L + 80L * (completed + delta) + 512L * Math.Min(completed + delta, receiptLimit);
            tailCost[ProjectionDeliveryCost.ProofReadBytes].ShouldBeLessThanOrEqualTo(8 * size);
            tailCost[ProjectionDeliveryCost.ProofWriteBytes].ShouldBeLessThanOrEqualTo(4 * size);
            tailCost[ProjectionDeliveryCost.ProofVisits].ShouldBeLessThanOrEqualTo(24L * (completed + 2L * delta + Math.Min(completed + delta, receiptLimit)));
            TestContext.Current.TestOutputHelper?.WriteLine("ProjectionGuardLiveCost " + JsonSerializer.Serialize(new
            {
                head = completed + delta, checkpoint = completed, delta, pageSize, pages, receiptLimit,
                ranges = tailCost[ProjectionDeliveryCost.SourceRanges], returned = tailCost[ProjectionDeliveryCost.SourceReturnedEvents],
                sourceBytes = tailCost[ProjectionDeliveryCost.SourceResponseBytes], wireBytes = tailCost[ProjectionDeliveryCost.WireBytes],
                proofBytes = tailCost[ProjectionDeliveryCost.ProofReadBytes] + tailCost[ProjectionDeliveryCost.ProofWriteBytes],
                modelBytes = tailCost[ProjectionDeliveryCost.ModelBytes], proofVisits = tailCost[ProjectionDeliveryCost.ProofVisits],
                handlers = tailCost[ProjectionDeliveryCost.HandlerAttempts], batches = tailCost[ProjectionDeliveryCost.BatchExecutions],
                cost = Enum.GetValues<ProjectionDeliveryCost>().ToDictionary(static category => category.ToString(), category => tailCost[category]),
            }));
            var states = services.GetRequiredService<IProjectionDeliveryStateStore>();
            ProjectionDeliveryStateReadResult completedRow = await states.ReadAsync(identity, LiveGuardedCounterProjectionHandler.Route).ConfigureAwait(true);
            completedRow.State!.LastDeliveredSequence.ShouldBe(completed + delta);
            completedRow.State.GuardEvidence!.Source.ShouldBe(frozen);
            completedRow.State.CompletedMessageIdDigests!.Count.ShouldBe(completed + delta);
            completedRow.State.ActiveReservation.ShouldBeNull();
            completedRow.State.IdleBindingPending.ShouldBeNull();
            var lifecycle = services.GetRequiredService<IProjectionLifecycleGateway>();
            var idle = await lifecycle.ReadSnapshotAsync(identity, LiveGuardedCounterProjectionHandler.Route, CancellationToken.None).ConfigureAwait(true);
            idle.Revision.ShouldBe(completedRow.State.GuardEvidence.IdleLifecycleRevision);
            IReadModelStore models = services.GetRequiredService<IReadModelStore>();
            string key = identity.ActorId + ":" + LiveGuardedCounterProjectionHandler.Route;
            LiveGuardedCounterModel detail = (await models.GetAsync<LiveGuardedCounterModel>(StoreName, key).ConfigureAwait(true)).Value.ShouldNotBeNull();
            (await models.GetAsync<LiveGuardedCounterModel>(StoreName, key + "-index").ConfigureAwait(true)).Value.ShouldBe(detail);
            EventEnvelope[] stored = await ReadGuardedStoredAsync(source, frozen.HeadSequence).ConfigureAwait(true);
            SourceProof chain = SourceProofCodec.CreateSeed(identity, frozen.SourceIncarnation);
            foreach (EventEnvelope item in stored) chain = SourceProofCodec.Extend(chain, item);
            chain.ShouldBe(frozen);
            var oracle = AggregateReplayer.Replay<CounterState>(new(identity.TenantId, identity.Domain, "Counter", identity.AggregateId,
                frozen.HeadSequence, [.. stored.Select(item => new ReplayEventEnvelope(item.SequenceNumber, item.EventTypeName,
                    item.Payload, item.SerializationFormat, item.MetadataVersion, item.MessageId, item.CorrelationId, item.CausationId))], false, null));
            oracle.Status.ShouldBe(AggregateReconstructionStatus.Succeeded);
            using JsonDocument canonical = JsonDocument.Parse(oracle.StateJson.ShouldNotBeNull());
            detail.Count.ShouldBe(canonical.RootElement.GetProperty("count").GetInt32());
            using var currentCost = new ProjectionDeliveryCostListener();
            ProjectionSourceGuardDeliveryResult current = await delivery.InspectCurrentAsync(identity, CancellationToken.None).ConfigureAwait(true);
            currentCost.Dispose();
            current.AlreadyCurrent.ShouldBeTrue(current.ReasonCode);
            currentCost[ProjectionDeliveryCost.SourceInspections].ShouldBe(2);
            currentCost[ProjectionDeliveryCost.ProofReads].ShouldBe(1);
            currentCost[ProjectionDeliveryCost.MarkerReads].ShouldBe(2);
            currentCost[ProjectionDeliveryCost.SourceRanges].ShouldBe(0);
            currentCost[ProjectionDeliveryCost.SourceEventReads].ShouldBe(0);
            currentCost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(0);
            currentCost[ProjectionDeliveryCost.ModelMutations].ShouldBe(0);
            currentCost[ProjectionDeliveryCost.MutationAttempts].ShouldBe(0);
            (await states.ReadAsync(identity, LiveGuardedCounterProjectionHandler.Route).ConfigureAwait(true)).Etag.ShouldBe(completedRow.Etag);
            (await models.GetAsync<LiveGuardedCounterModel>(StoreName, key).ConfigureAwait(true)).Value.ShouldBe(detail);
        }
        finally { await loader.StopAsync(CancellationToken.None).ConfigureAwait(true); }
    }

    private async Task AppendGuardedEventsAsync(AggregateIdentity identity, IAggregateActor source, int count)
    {
        for (int appended = 0; appended < count;)
        {
            int batch = Math.Min(256, count - appended);
            fixture.DomainServiceInvoker.SetupResponse("IncrementCounter", DomainResult.Success(
                [.. Enumerable.Range(0, batch).Select(static _ => (IEventPayload)new CounterIncremented())]));
            (await source.ProcessCommandAsync(new CommandEnvelopeBuilder().WithTenantId(identity.TenantId).WithDomain(identity.Domain)
                .WithAggregateId(identity.AggregateId).WithCommandType("IncrementCounter").Build()).ConfigureAwait(true)).Accepted.ShouldBeTrue();
            appended += batch;
        }
    }

    private static async Task<EventEnvelope[]> ReadGuardedStoredAsync(IAggregateActor source, long head)
    {
        var result = new List<EventEnvelope>();
        long cursor = 0;
        while (cursor < head)
        {
            SourceEventRange page = await source.ReadMeasuredEventsBoundedRangeAsync(cursor, head, 256, 2_097_152).ConfigureAwait(false);
            result.AddRange(page.Events);
            cursor = page.Events[^1].SequenceNumber;
        }
        return [.. result];
    }

    private async Task SeedGuardedBaselineAsync(AggregateIdentity identity, IAggregateActor source, NamedProjectionRouteCatalogEntry catalog, int count)
    {
        IServiceProvider services = fixture.Services;
        var states = services.GetRequiredService<IProjectionDeliveryStateStore>();
        if (await states.ReadWriterProtocolAsync().ConfigureAwait(true) is null)
            (await states.TryActivateWriterProtocolAsync(new(1, 2, GuardFixtureRevision, DateTimeOffset.UtcNow)).ConfigureAwait(true)).ShouldBeTrue();
        EventEnvelope[] stored = await ReadGuardedStoredAsync(source, count).ConfigureAwait(true);
        ProjectionEventDto[] events = (await ProjectionEventWireBuilder.BuildAsync(services.GetRequiredService<IEventPayloadProtectionService>(),
            identity, stored, CancellationToken.None).ConfigureAwait(true)).Events!;
        string operation = events[^1].MessageId!;
        const string route = LiveGuardedCounterProjectionHandler.Route;
        var coordinator = services.GetRequiredService<IProjectionDeliveryIdempotencyCoordinator>();
        var reservation = (await coordinator.TryAdmitAsync(identity, route, events, true).ConfigureAwait(true)).Reservation.ShouldNotBeNull();
        var lifecycle = services.GetRequiredService<IProjectionLifecycleGateway>();
        string lease = UniqueIdHelper.GenerateSortableUniqueStringId();
        (await lifecycle.BeginDeliveryWriteAsync(identity, route, lease, CancellationToken.None).ConfigureAwait(true)).ShouldBeTrue();
        try
        {
            var request = new ProjectionDispatchRequest(new(identity.TenantId, identity.Domain, identity.AggregateId, events)
            { RebuildSourceHeadSequence = count, RebuildSourceHeadMessageId = operation,
                RebuildOwnerGenerations = new Dictionary<string, string> { [route] = events[0].MessageId! },
                RebuildOwnerIdentities = new Dictionary<string, string> { [route] = identity.AggregateId } },
                [route], operation, catalog.CatalogFingerprint);
            var transport = services.GetRequiredService<IProjectionSourceGuardTransport>();
            foreach (var action in new[] { ProjectionMappedReplayAction.Stage, ProjectionMappedReplayAction.Commit, ProjectionMappedReplayAction.Verify })
            {
                ProjectionDispatchResponse result = (await transport.InvokeReplayAsync(catalog, request, action, CancellationToken.None).ConfigureAwait(true)).ShouldNotBeNull();
                result.Outcomes.ShouldHaveSingleItem().Status.ShouldBe(ProjectionDispatchStatus.Completed);
            }
            (await coordinator.CompleteAsync(identity, route, events, reservation).ConfigureAwait(true)).ShouldBe(ProjectionDeliveryCompletion.Completed);
        }
        finally { (await lifecycle.CompleteDeliveryWriteAsync(identity, route, lease, CancellationToken.None).ConfigureAwait(true)).ShouldBeTrue(); }
    }

    private static ProjectionSourceGuardMaintenance GuardFixtureMaintenance()
    {
        string asset = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(ProjectionSourceGuardDelivery).Assembly.Location)));
        return new("fixture-operator", GuardFixtureRevision, "isolated-fixture-backup", ProjectionSourceGuard.ApprovedSpecSha256,
            true, true, true, [.. new[] { "append", "immediate", "poller", "retry", "reconciliation", "rebuild", "erasure" }
                .Select(role => new ProjectionSourceGuardWriterInventory(role, GuardFixtureRevision, "fixture-assembly-sha256:" + asset, true))]);
    }
}
