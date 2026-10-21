using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed partial class DomainPartitionedSharedProjectionRebuildTests
{
    [Theory]
    [InlineData(10_001)]
    [InlineData(32_769)]
    public async Task ProductionStreamedDispatcherRebuildsGrowsAndRebuildsCompleteOriginalOwner(int count)
    {
        var store = new InMemoryReadModelStore();
        var handler = new StreamedPartitionedHandler { FoldOrdinaryRows = true, FailBufferedOwnerFold = true };
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using var provider = Provider(store, store, coordinator, handler);
        var scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        ProjectionEventDto[] events = BoundedHistory(count);
        var first = Identity("bounded-original-" + count);
        var accepted = await BoundedAccumulateAsync(provider, first, events);
        accepted.AcceptedAggregateCount.ShouldBe(1);
        string manifestKey = DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(DomainSharedProjectionRebuildSessionKey.Compute(first), 0);
        var manifest = (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName, manifestKey)).Value!;
        manifest.CapturedEventDigests.ShouldBeNull();
        manifest.CapturedIdentityReference!.Count.ShouldBe(count);
        JsonSerializer.SerializeToUtf8Bytes(manifest).Length.ShouldBeLessThan(2048);
        await BoundedFinishAsync(provider, first, accepted);
        var checkpoint = (await coordinator.GetCheckpointAsync(scope, "owner-a"))!;
        checkpoint.OwnerSequenceNumber.ShouldBe(count);
        checkpoint.CapturedEventDigests.ShouldBeNull();
        checkpoint.CapturedIdentityReference.ShouldBe(manifest.CapturedIdentityReference);
        JsonSerializer.SerializeToUtf8Bytes(checkpoint).Length.ShouldBeLessThan(2048);
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName, manifestKey)).Value.ShouldBeNull();
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value!.Value.ShouldBe(Convert.ToHexString(SHA256.HashData(events[^1].Payload)));
        foreach (int index in new[] { 0, count / 2, count - 1 })
            (await BoundedOrdinaryAsync(provider, handler, first, events[index])).ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        ProjectionEventDto growth = BoundedHistory(count + 1)[^1];
        (await BoundedOrdinaryAsync(provider, handler, first, growth)).ShouldBe(ProjectionDispatchStatus.Completed);
        (await coordinator.GetCheckpointAsync(scope, "owner-a"))!.CapturedIdentityReference.ShouldBe(checkpoint.CapturedIdentityReference);
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        using var replacement = Provider(store, store, coordinator, handler);
        var second = Identity("bounded-grown-" + count);
        var grown = BoundedHistory(count + 1);
        accepted = await BoundedAccumulateAsync(replacement, second, grown);
        await BoundedFinishAsync(replacement, second, accepted);
        checkpoint = (await coordinator.GetCheckpointAsync(scope, "owner-a"))!;
        checkpoint.OwnerSequenceNumber.ShouldBe(count + 1);
        checkpoint.CapturedIdentityReference!.Count.ShouldBe(count + 1);
        foreach (int index in new[] { 0, count / 2, count })
            (await BoundedOrdinaryAsync(replacement, handler, second, grown[index])).ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await BoundedOrdinaryAsync(replacement, handler, second, grown[0] with { Payload = [99] })).ShouldBe(ProjectionDispatchStatus.Failed);
        (await coordinator.GetStatusAsync(scope)).DeliveryConflicts.ShouldNotBeEmpty();
        checkpoint.OwnerSequenceNumber.ShouldBe(count + 1);
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value!.Value.ShouldBe(Convert.ToHexString(SHA256.HashData(growth.Payload)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(65)]
    [InlineData(129)]
    public async Task DispatcherRejectsChangedRetainedEarlyMiddleAndFinalIdentitiesBeforePromotion(int changed)
    {
        var store = new InMemoryReadModelStore();
        var handler = new StreamedPartitionedHandler { FoldOrdinaryRows = true, FailBufferedOwnerFold = true };
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using var provider = Provider(store, store, coordinator, handler);
        var scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        var first = Identity("retained-first");
        await BoundedFinishAsync(provider, first, await BoundedAccumulateAsync(provider, first, BoundedHistory(129)));
        long selected = (await coordinator.GetStatusAsync(scope)).ActiveGeneration;
        var originalCheckpoint = await coordinator.GetCheckpointAsync(scope, "owner-a");
        var changedEvents = BoundedHistory(130);
        changedEvents[changed - 1] = changedEvents[changed - 1] with { MessageId = "changed-original-message" };
        var next = Identity("retained-changed");
        var accepted = await BoundedAccumulateAsync(provider, next, changedEvents);
        (await BoundedRebuildAsync(provider, Finalize(next, accepted))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await BoundedRebuildAsync(provider, Lifecycle(next, DomainSharedProjectionRebuildAction.Stage))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await BoundedRebuildAsync(provider, Lifecycle(next, DomainSharedProjectionRebuildAction.Commit))).Status.ShouldBeOneOf(ProjectionDispatchStatus.Failed, ProjectionDispatchStatus.Indeterminate);
        (await coordinator.GetStatusAsync(scope)).ActiveGeneration.ShouldBe(selected);
        JsonSerializer.Serialize(await coordinator.GetCheckpointAsync(scope, "owner-a")).ShouldBe(JsonSerializer.Serialize(originalCheckpoint));
    }

    [Fact]
    public async Task DispatcherRejectsCrossPageMessageAliasAndAbortRetainsDurableRetirementInventory()
    {
        var store = new InMemoryReadModelStore();
        var handler = new StreamedPartitionedHandler { FailBufferedOwnerFold = true };
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using var provider = Provider(store, store, coordinator, handler);
        var scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        var identity = Identity("distant-duplicate");
        var events = BoundedHistory(257);
        events[^1] = events[^1] with { MessageId = events[0].MessageId };
        var outcome = await BoundedAccumulateAsync(provider, identity, events, requireSuccess: false);
        outcome.Status.ShouldBeOneOf(ProjectionDispatchStatus.Indeterminate, ProjectionDispatchStatus.Failed);
        var session = (await store.GetAsync<DomainSharedProjectionRebuildSessionState>(StoreName, DomainSharedProjectionRebuildSessionKey.Compute(identity))).Value!;
        var reference = session.PendingCapturedIdentityReference.ShouldNotBeNull();
        (await BoundedRebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        using var replacement = Provider(store, store, new SharedProjectionEpochCoordinator(store, store), handler);
        (await BoundedRebuildAsync(replacement, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort))).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await store.GetAsync<System.Text.Json.Nodes.JsonObject>(StoreName, "shared-identity-v1:" + reference.NamespaceHash)).Value!["retired"]!.GetValue<bool>().ShouldBeTrue();
        (await coordinator.GetStatusAsync(scope)).ActiveGeneration.ShouldBe(0);
        (await coordinator.GetCheckpointAsync(scope, "owner-a")).ShouldBeNull();
    }

    private static ProjectionEventDto[] BoundedHistory(int count)
        => Enumerable.Range(1, count).Select(sequence => Event(BitConverter.GetBytes(sequence)) with
        {
            SequenceNumber = sequence, GlobalPosition = sequence * 3L, MessageId = "original-message-" + sequence,
            Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(sequence)
        }).ToArray();

    private static Task<DomainSharedProjectionRebuildResponse> BoundedRebuildAsync(IServiceProvider provider, DomainSharedProjectionRebuildRequest request)
        => DomainSharedProjectionRebuildDispatcher.DispatchAsync(provider, request,
            new ProjectionDispatchOptions { MaxRebuildEventCount = 40_000 },
            new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" }, TestContext.Current.CancellationToken);

    private static async Task<DomainSharedProjectionRebuildResponse> BoundedAccumulateAsync(IServiceProvider provider,
        DomainSharedProjectionRebuildIdentity identity, ProjectionEventDto[] events, bool requireSuccess = true)
    {
        string history = DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-a", false, events);
        string inventory = DomainSharedProjectionRebuildFingerprint.AppendInventory(DomainSharedProjectionRebuildFingerprint.EmptyInventory, 0, history);
        (await BoundedRebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            identity, CaptureInventoryFingerprint: inventory, SourceHighWatermarks: new Dictionary<string, long> { ["owner-a"] = events[^1].GlobalPosition }))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        int part = 0;
        for (int first = 0; first < events.Length; first += 256)
            (await BoundedRebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulatePart,
                identity, AggregateOrdinal: 0, AggregateId: "owner-a", Events: events.AsSpan(first, Math.Min(256, events.Length - first)).ToArray(), PartOrdinal: part++))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var result = await BoundedRebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulateComplete,
            identity, AggregateOrdinal: 0, AggregateId: "owner-a", PartCount: part, ExpectedHistoryFingerprint: history));
        if (requireSuccess) result.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        return result;
    }

    private static async Task BoundedFinishAsync(IServiceProvider provider, DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildResponse accepted)
    {
        (await BoundedRebuildAsync(provider, Finalize(identity, accepted))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await BoundedRebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Stage))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await BoundedRebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Commit))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await BoundedRebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Verify))).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
    }

    private static async Task<ProjectionDispatchStatus> BoundedOrdinaryAsync(IServiceProvider provider, PartitionedHandler handler,
        DomainSharedProjectionRebuildIdentity identity, ProjectionEventDto item)
    {
        var result = await DomainProjectionDispatcher.DispatchAsync(provider, new ProjectionDispatchRequest(
            new ProjectionRequest(identity.TenantId, identity.Domain, "owner-a", [item]), [handler.ProjectionType], item.MessageId,
            identity.CatalogFingerprint), new ProjectionDispatchOptions { MaxRebuildEventCount = 40_000 },
            new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" }, TestContext.Current.CancellationToken);
        return result.Outcomes.Single().Status;
    }
}
