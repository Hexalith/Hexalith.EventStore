using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class ProjectionReplayRecoveryTests
{
    [Fact]
    public async Task Coordinator_PartialRouteCheckpointCompletionRetainsTheOriginalCoordinatedGroupAndManifest()
    {
        using var fixture = new ReplayRecoveryCoordinatorFixture(routeCount: 2);
        _ = await fixture.ActivateFixtureWriterAsync();
        string[] routes = [.. fixture.Host.Request.ProjectionTypes];
        string refused = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, routes[1]);
        fixture.Host.Backend.BeforeTrySaveByteState = (key, bytes) =>
        {
            if (key == refused && JsonSerializer.Deserialize<ProjectionDeliveryState>(bytes.Span,
                JsonSerializerOptions.Web)?.LastDeliveredSequence == 3) throw new IOException("fixture completion outage");
        };
        _ = await fixture.DispatchAsync();
        ProjectionDeliveryRetryWorkItem original = fixture.Work.ShouldNotBeNull();
        original.PendingRoutes.ShouldBe(routes.Order());
        ProjectionDeliveryStateReadResult first = await fixture.States.ReadAsync(fixture.Identity, routes[0]);
        first.State!.LastDeliveredSequence.ShouldBe(3);
        (await fixture.States.ReadAsync(fixture.Identity, routes[1])).State!.LastDeliveredSequence.ShouldBe(0);
        byte[] firstBytes = fixture.Host.Backend.ByteStoreValue(ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, routes[0]));
        fixture.Host.Preparations.ShouldBe(2);
        fixture.Host.Backend.BeforeTrySaveByteState = null!;
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        _ = await fixture.DispatchAsync();
        fixture.Work.ShouldBeNull();
        fixture.Host.Preparations.ShouldBe(2);
        fixture.Http.Calls.ShouldAllBe(call => call.DispatchId == original.DispatchId && call.Header == fixture.Host.Header);
        (await fixture.States.ReadAsync(fixture.Identity, routes[1])).State!.LastDeliveredSequence.ShouldBe(3);
        (await fixture.States.ReadAsync(fixture.Identity, routes[0])).Etag.ShouldBe(first.Etag);
        fixture.Host.Backend.ByteStoreValue(ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, routes[0])).ShouldBe(firstBytes);
        cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(0);
    }

    [Theory]
    [InlineData("stage")]
    [InlineData("commit")]
    [InlineData("verify")]
    public async Task Coordinator_LostResponseKeepsOriginalWorkAndConvergesWithoutNewPreparation(string action)
    {
        using var fixture = new ReplayRecoveryCoordinatorFixture();
        (await fixture.ActivateFixtureWriterAsync()).ShouldBe(ProjectionDeliveryCutoverStatus.Activated);
        fixture.Http.LoseAfterAction = action;
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await fixture.DispatchAsync()).ShouldBeTrue();
        ProjectionDeliveryRetryWorkItem originalWork = fixture.Work.ShouldNotBeNull();
        originalWork.ReplayRecoveryBindings.ShouldNotBeNull().ShouldHaveSingleItem();
        ProjectionDeliveryState row = (await fixture.States.ReadAsync(fixture.Identity, ReplayRecoveryProductionFixture.Route)).State.ShouldNotBeNull();
        row.LastDeliveredSequence.ShouldBe(0);
        ProjectionDeliveryReservation reservation = row.ActiveReservation.ShouldNotBeNull();
        byte[] original = fixture.Host.Backend.ByteStoreValue(DomainProjectionReplayRecovery.Key(fixture.Host.Request));
        (await fixture.DispatchAsync()).ShouldBeTrue();
        fixture.Work.ShouldBeNull();
        fixture.Host.Preparations.ShouldBe(1);
        fixture.Http.Calls.ShouldAllBe(call => call.DispatchId == originalWork.DispatchId && call.Header == fixture.Host.Header);
        ProjectionDeliveryState completed = (await fixture.States.ReadAsync(fixture.Identity, ReplayRecoveryProductionFixture.Route)).State.ShouldNotBeNull();
        completed.LastDeliveredSequence.ShouldBe(3);
        completed.LastCompletedMessageId.ShouldBe(originalWork.HeadMessageId);
        completed.CompletedPrefixFingerprint.ShouldBe(reservation.ManifestFingerprint);
        completed.ActiveReservation.ShouldBeNull();
        (await fixture.Host.Models.GetAsync<string>(ReplayRecoveryProductionFixture.StoreName, "detail")).Value.ShouldBe("count:3");
        (await fixture.Host.Models.GetAsync<string>(ReplayRecoveryProductionFixture.StoreName, "index")).Value.ShouldBe("index:3");
        cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(2);
        fixture.Host.Backend.ByteStoreValue(DomainProjectionReplayRecovery.Key(fixture.Host.Request)).Length.ShouldBeLessThan(original.Length);
    }

    [Fact]
    public async Task Coordinator_CleanupFailureRetainsCompletedRowAndOriginalWorkUntilReceiptProvenCompaction()
    {
        using var fixture = new ReplayRecoveryCoordinatorFixture();
        _ = await fixture.ActivateFixtureWriterAsync();
        fixture.Http.RejectCleanup = true;
        _ = await fixture.DispatchAsync();
        ProjectionDeliveryRetryWorkItem original = fixture.Work.ShouldNotBeNull();
        ProjectionDeliveryStateReadResult row = await fixture.States.ReadAsync(fixture.Identity, ReplayRecoveryProductionFixture.Route);
        row.State.ShouldNotBeNull().LastDeliveredSequence.ShouldBe(3);
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, ReplayRecoveryProductionFixture.Route);
        byte[] completedBytes = fixture.Host.Backend.ByteStoreValue(key);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        fixture.Http.RejectCleanup = false;
        _ = await fixture.DispatchAsync();
        fixture.Work.ShouldBeNull();
        fixture.Host.Preparations.ShouldBe(1);
        fixture.Host.Backend.ByteStoreValue(key).ShouldBe(completedBytes);
        (await fixture.States.ReadAsync(fixture.Identity, ReplayRecoveryProductionFixture.Route)).Etag.ShouldBe(row.Etag);
        cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(0);
        fixture.Http.Calls.ShouldAllBe(call => call.DispatchId == original.DispatchId);
    }

    [Fact]
    public async Task Coordinator_CheckpointFailureResumesOriginalManifestInsteadOfRepreparingAgainstAdvancedModel()
    {
        using var fixture = new ReplayRecoveryCoordinatorFixture();
        _ = await fixture.ActivateFixtureWriterAsync();
        string key = ProjectionDeliveryStateKeys.GetStateKey(fixture.Identity, ReplayRecoveryProductionFixture.Route);
        fixture.Host.Backend.BeforeTrySaveByteState = (stateKey, bytes) =>
        {
            if (stateKey == key && JsonSerializer.Deserialize<ProjectionDeliveryState>(bytes.Span,
                JsonSerializerOptions.Web)?.LastDeliveredSequence == 3)
                throw new IOException("sensitive-provider-detail");
        };
        _ = await fixture.DispatchAsync();
        ProjectionDeliveryRetryWorkItem original = fixture.Work.ShouldNotBeNull();
        (await fixture.States.ReadAsync(fixture.Identity, ReplayRecoveryProductionFixture.Route)).State!.LastDeliveredSequence.ShouldBe(0);
        (await fixture.Host.Models.GetAsync<string>(ReplayRecoveryProductionFixture.StoreName, "detail")).Value.ShouldBe("count:3");
        fixture.Host.Backend.BeforeTrySaveByteState = null!;
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        _ = await fixture.DispatchAsync();
        fixture.Work.ShouldBeNull();
        (await fixture.States.ReadAsync(fixture.Identity, ReplayRecoveryProductionFixture.Route)).State!.LastDeliveredSequence.ShouldBe(3);
        fixture.Host.Preparations.ShouldBe(1);
        cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(0);
        fixture.Http.Calls.ShouldAllBe(call => call.DispatchId == original.DispatchId);
    }

    [Theory]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(771)]
    [InlineData(10_000)]
    public async Task Coordinator_FullPrefixBoundariesPrepareOnceAndPersistMatchingDetailIndexAndCheckpoint(int head)
    {
        using var fixture = new ReplayRecoveryCoordinatorFixture(head);
        _ = await fixture.ActivateFixtureWriterAsync();
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        _ = await fixture.DispatchAsync();
        fixture.Work.ShouldBeNull();
        fixture.Host.Preparations.ShouldBe(1);
        fixture.Http.Calls.Select(static call => call.Action).ShouldBe(["stage", "commit", "verify", "reclaim-recovery"]);
        (await fixture.Host.Models.GetAsync<string>(ReplayRecoveryProductionFixture.StoreName, "detail")).Value.ShouldBe("count:" + head);
        (await fixture.Host.Models.GetAsync<string>(ReplayRecoveryProductionFixture.StoreName, "index")).Value.ShouldBe("index:" + head);
        (await fixture.States.ReadAsync(fixture.Identity, ReplayRecoveryProductionFixture.Route)).State!.LastDeliveredSequence.ShouldBe(head);
        cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(1);
        cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(2);
    }

    [Fact]
    public void Binding_IsOpaqueSeparateFromHistoricalCatalogAndOmittedFromOldWork()
    {
        using var fixture = new ReplayRecoveryProductionFixture();
        fixture.Metadata.CatalogFingerprint.ShouldBe(fixture.HistoricalMetadata.CatalogFingerprint);
        fixture.Metadata.Domains.Single().ReplayRecoveryBindings!.Values.Single().IsSupported.ShouldBeTrue();
        JsonSerializer.Serialize(fixture.Metadata, JsonSerializerOptions.Web).ShouldNotContain(ReplayRecoveryProductionFixture.StoreName);
        JsonSerializer.Serialize(fixture.HistoricalMetadata, JsonSerializerOptions.Web).ShouldNotContain("replayRecoveryBindings");
        var work = new ProjectionDeliveryRetryWorkItem("work", "tenant-a", "counter", "counter-1", "sample", "v1", 3,
            fixture.Request.DispatchId, [ReplayRecoveryProductionFixture.Route], [], fixture.Request.DispatchId,
            fixture.Request.CatalogFingerprint, 0, DateTimeOffset.UnixEpoch, null);
        JsonSerializer.Serialize(work, JsonSerializerOptions.Web).ShouldNotContain("replayRecoveryBindings");
        using ServiceProvider changed = fixture.Services("different-existing-models");
        AdminOperationalIndexMetadata.Response metadata = DomainProjectionReplayRecovery.BindMetadata(changed, fixture.HistoricalMetadata);
        metadata.CatalogFingerprint.ShouldBe(fixture.Metadata.CatalogFingerprint);
        metadata.Domains.Single().ReplayRecoveryBindings!.Values.Single().BindingFingerprint.ShouldNotBe(
            fixture.Metadata.Domains.Single().ReplayRecoveryBindings!.Values.Single().BindingFingerprint);
    }

    [Fact]
    public void Binding_TwoDomainHostAdvertisesOnlyTheRequestedDomainWithoutRejectingItsSiblingRegistration()
    {
        using var fixture = new ReplayRecoveryProductionFixture();
        IAsyncDomainProjectionRebuildHandler sibling = Substitute.For<IAsyncDomainProjectionRebuildHandler>();
        sibling.Domain.Returns("inventory");
        sibling.ProjectionType.Returns("inventory-detail");
        sibling.OrdinaryDeliveryEnabled.Returns(true);
        sibling.RebuildSemantics.Returns(DomainProjectionRebuildSemantics.FullReplay);
        var services = new ServiceCollection();
        services.AddSingleton(new DomainProjectionReplayRecoveryRegistration("counter", ReplayRecoveryProductionFixture.Route,
            ReplayRecoveryProductionFixture.StoreName));
        services.AddSingleton(new DomainProjectionReplayRecoveryRegistration("inventory", "inventory-detail", "other-existing-models"));
        using ServiceProvider provider = services.BuildServiceProvider();
        foreach (string[] domains in new[] { new[] { "counter" }, new[] { "inventory" }, new[] { "counter", "inventory" } })
        {
            AdminOperationalIndexMetadata.Response historical = AdminOperationalIndexMetadata.Create(new Hexalith.EventStore.Client.Discovery.DiscoveryResult([], []),
                domains, [], [fixture.Handler, sibling], "sample", "v1", new ProjectionDispatchOptions(), domains);
            AdminOperationalIndexMetadata.Response bound = DomainProjectionReplayRecovery.BindMetadata(provider, historical, [fixture.Handler, sibling]);
            bound.CatalogFingerprint.ShouldBe(historical.CatalogFingerprint);
            bound.Domains.Select(static item => item.Domain).Order().ShouldBe(domains.Order());
            bound.Domains.ShouldAllBe(static domain => domain.ReplayRecoveryBindings!.Count == 1);
            bound.Domains.SelectMany(static domain => domain.ReplayRecoveryBindings!.Values).ShouldAllBe(static binding => binding.IsSupported);
        }
    }

    [Fact]
    public async Task FreshScopes_ReuseOriginalConditionalManifestAfterEveryLostActionResponse()
    {
        using var fixture = new ReplayRecoveryProductionFixture();
        await fixture.Models.SaveAsync(ReplayRecoveryProductionFixture.StoreName, "detail", "before");
        await fixture.Models.SaveAsync(ReplayRecoveryProductionFixture.StoreName, "index", "before-index");
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        foreach (DomainProjectionRebuildBatchAction action in new[] { DomainProjectionRebuildBatchAction.Stage,
            DomainProjectionRebuildBatchAction.Commit, DomainProjectionRebuildBatchAction.Verify })
        {
            (await fixture.ExecuteAsync(action)).Outcomes.ShouldAllBe(static outcome => outcome.Status == ProjectionDispatchStatus.Completed);
            byte[] original = fixture.Backend.ByteStoreValue(DomainProjectionReplayRecovery.Key(fixture.Request));
            int mutations = fixture.Backend.ExecuteStateTransactionCallCount;
            // Ignore the first response and retry on a new host scope against the original persisted values and ETags.
            (await fixture.ExecuteAsync(action)).Outcomes.ShouldAllBe(static outcome => outcome.Status == ProjectionDispatchStatus.Completed);
            fixture.Backend.ByteStoreValue(DomainProjectionReplayRecovery.Key(fixture.Request)).ShouldBe(original);
            fixture.Backend.ExecuteStateTransactionCallCount.ShouldBe(mutations);
        }
        fixture.Preparations.ShouldBe(1);
        cost[ProjectionDeliveryCost.HandlerAttempts].ShouldBe(1);
        cost[ProjectionDeliveryCost.ModelMutations].ShouldBe(2);
        (await fixture.Models.GetAsync<string>(ReplayRecoveryProductionFixture.StoreName, "detail")).Value.ShouldBe("count:3");
        (await fixture.Models.GetAsync<string>(ReplayRecoveryProductionFixture.StoreName, "index")).Value.ShouldBe("index:3");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("different-existing-models")]
    public async Task MissingOrChangedBinding_RejectsBeforeStoreOrPreparation(string? binding)
    {
        using var fixture = new ReplayRecoveryProductionFixture();
        (await fixture.ExecuteAsync(DomainProjectionRebuildBatchAction.Stage)).Outcomes[0].Status.ShouldBe(ProjectionDispatchStatus.Completed);
        byte[] original = fixture.Backend.ByteStoreValue(DomainProjectionReplayRecovery.Key(fixture.Request));
        int calls = fixture.Backend.TrySaveByteOperations.Count;
        ProjectionDispatchValidationException failure = await Should.ThrowAsync<ProjectionDispatchValidationException>(() =>
            fixture.ExecuteAsync(DomainProjectionRebuildBatchAction.Stage, binding));
        failure.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.UnsupportedCapability);
        fixture.Preparations.ShouldBe(1);
        fixture.Backend.TrySaveByteOperations.Count.ShouldBe(calls);
        fixture.Backend.ByteStoreValue(DomainProjectionReplayRecovery.Key(fixture.Request)).ShouldBe(original);
    }

    [Theory]
    [InlineData("{sensitive-payload")]
    [InlineData("{\"counter-detail\":{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":{\"f\":{\"g\":{\"h\":{}}}}}}}}}")]
    [InlineData("[]")]
    [InlineData("{\"counter-detail\":\"bad\",\"counter-detail\":\"bad\"}")]
    public async Task MalformedOrConflictingHeader_ReturnsBoundedValidationWithoutEffects(string header)
    {
        using var fixture = new ReplayRecoveryProductionFixture();
        ProjectionDispatchValidationException failure = await Should.ThrowAsync<ProjectionDispatchValidationException>(() =>
            fixture.ExecuteAsync(DomainProjectionRebuildBatchAction.Stage, header: header));
        failure.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.UnsupportedCapability);
        failure.Message.ShouldNotContain("sensitive-payload");
        fixture.Preparations.ShouldBe(0);
        fixture.Backend.TrySaveByteOperations.ShouldBeEmpty();
        fixture.Backend.ExecuteStateTransactionCallCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CorruptOriginalManifestOrProviderState_FailsClosedWithoutRepreparation(bool invalidRecord)
    {
        using var fixture = new ReplayRecoveryProductionFixture();
        _ = await fixture.ExecuteAsync(DomainProjectionRebuildBatchAction.Stage);
        string key = DomainProjectionReplayRecovery.Key(fixture.Request);
        DomainProjectionReplayRecoveryManifest original = (await fixture.Models.GetAsync<DomainProjectionReplayRecoveryManifest>(
            ReplayRecoveryProductionFixture.StoreName, key)).Value!;
        if (invalidRecord) fixture.Backend.SeedByteStore(key, "{sensitive-provider-detail"u8.ToArray());
        else await fixture.Models.SaveAsync(ReplayRecoveryProductionFixture.StoreName, key, original with
        { Manifest = "{sensitive-manifest"u8.ToArray(), Fingerprint = ReadModelBatchFingerprint.ComputeFromManifest("{sensitive-manifest"u8) });
        byte[] corrupt = fixture.Backend.ByteStoreValue(key);
        int mutations = fixture.Backend.ExecuteStateTransactionCallCount;
        if (invalidRecord)
            (await fixture.ExecuteAsync(DomainProjectionRebuildBatchAction.Commit)).Outcomes[0].ReasonCode.ShouldBe(
                ProjectionDispatchReasonCodes.DeliveryReconciliationRequired);
        else (await Should.ThrowAsync<ProjectionDispatchValidationException>(() => fixture.ExecuteAsync(DomainProjectionRebuildBatchAction.Commit)))
            .ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryReconciliationRequired);
        fixture.Backend.ByteStoreValue(key).ShouldBe(corrupt);
        fixture.Backend.ExecuteStateTransactionCallCount.ShouldBe(mutations);
        fixture.Preparations.ShouldBe(1);
    }

    [Fact]
    public async Task AbsentAndRepeatedCasRefusal_PreparesOnceAndReadsWinnerOncePerActivation()
    {
        using var fixture = new ReplayRecoveryProductionFixture();
        IReadModelStore refusing = Substitute.For<IReadModelStore>();
        refusing.GetAsync<DomainProjectionReplayRecoveryManifest>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ReadModelEntry<DomainProjectionReplayRecoveryManifest>(null, null));
        refusing.TrySaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DomainProjectionReplayRecoveryManifest>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        using ServiceProvider provider = fixture.Services(recoveryStore: refusing);
        for (int activation = 1; activation <= 9; activation++)
        {
            ProjectionDispatchValidationException failure = await Should.ThrowAsync<ProjectionDispatchValidationException>(() =>
                DomainProjectionReplayRecovery.ExecuteAsync(provider, fixture.Request, new ProjectionDispatchOptions(),
                    new() { AppId = "sample", ServiceVersion = "v1" }, DomainProjectionRebuildBatchAction.Stage,
                    fixture.Header, CancellationToken.None));
            failure.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryReconciliationRequired);
            fixture.Preparations.ShouldBe(activation);
            _ = refusing.Received(2 * activation).GetAsync<DomainProjectionReplayRecoveryManifest>(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
            _ = refusing.Received(activation).TrySaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DomainProjectionReplayRecoveryManifest>(),
                string.Empty, Arg.Any<CancellationToken>());
            fixture.Backend.ExecuteStateTransactionCallCount.ShouldBe(0);
        }
    }

    [Fact]
    public async Task TerminalCleanup_RetainsSmallTombstoneAndCannotStartAnotherPreparation()
    {
        using var fixture = new ReplayRecoveryProductionFixture();
        _ = await fixture.ExecuteAsync(DomainProjectionRebuildBatchAction.Stage);
        ProjectionDispatchRequest completed = fixture.Request with { Request = fixture.Request.Request with { Events = [] } };
        (await fixture.ExecuteAsync(DomainProjectionRebuildBatchAction.Reclaim, request: completed)).Outcomes[0].Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        _ = await fixture.ExecuteAsync(DomainProjectionRebuildBatchAction.Commit);
        (await fixture.ExecuteAsync(DomainProjectionRebuildBatchAction.Reclaim, request: completed)).Outcomes[0].Status.ShouldBe(ProjectionDispatchStatus.Completed);
        string key = DomainProjectionReplayRecovery.Key(fixture.Request);
        byte[] tombstone = fixture.Backend.ByteStoreValue(key);
        tombstone.Length.ShouldBeLessThan(16_384);
        (await fixture.Models.GetAsync<DomainProjectionReplayRecoveryManifest>(ReplayRecoveryProductionFixture.StoreName, key)).Value!.Manifest.ShouldBeEmpty();
        _ = await Should.ThrowAsync<ProjectionDispatchValidationException>(() => fixture.ExecuteAsync(DomainProjectionRebuildBatchAction.Stage));
        (await fixture.ExecuteAsync(DomainProjectionRebuildBatchAction.Reclaim, request: completed)).Outcomes[0].Status.ShouldBe(ProjectionDispatchStatus.Completed);
        fixture.Backend.ByteStoreValue(key).ShouldBe(tombstone);
        fixture.Preparations.ShouldBe(1);
    }

    [Fact]
    public async Task CallerCancellation_PropagatesBeforePreparationOrStoreAccess()
    {
        using var fixture = new ReplayRecoveryProductionFixture();
        using var token = new CancellationTokenSource();
        token.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => fixture.ExecuteAsync(DomainProjectionRebuildBatchAction.Stage, token: token.Token));
        fixture.Preparations.ShouldBe(0);
        fixture.Backend.TrySaveByteOperations.ShouldBeEmpty();
    }
}
