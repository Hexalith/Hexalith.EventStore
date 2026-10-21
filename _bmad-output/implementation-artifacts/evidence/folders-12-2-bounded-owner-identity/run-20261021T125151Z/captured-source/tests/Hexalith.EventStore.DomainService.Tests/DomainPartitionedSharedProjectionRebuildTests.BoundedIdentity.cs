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
    [InlineData(10_001, false)]
    [InlineData(32_769, false)]
    [InlineData(10_001, true)]
    [InlineData(32_769, true)]
    public async Task ProductionDispatcherRebuildsGrowsAndRebuildsCompleteOriginalOwner(int count, bool bufferedOwner)
    {
        var store = new InMemoryReadModelStore();
        PartitionedHandler handler = bufferedOwner ? new OutputOnlyPartitionedHandler() : new StreamedPartitionedHandler();
        handler.FoldOrdinaryRows = true;
        handler.FailBufferedOwnerFold = !bufferedOwner;
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
    [InlineData(1, "message")]
    [InlineData(65, "message")]
    [InlineData(129, "message")]
    [InlineData(1, "payload")]
    [InlineData(65, "payload")]
    [InlineData(129, "payload")]
    [InlineData(1, "position")]
    [InlineData(65, "position")]
    [InlineData(129, "position")]
    public async Task DispatcherRejectsChangedRetainedEarlyMiddleAndFinalIdentitiesBeforePromotion(int changed, string mutation)
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
        changedEvents[changed - 1] = mutation switch
        {
            "message" => changedEvents[changed - 1] with { MessageId = "changed-original-message" },
            "payload" => changedEvents[changed - 1] with { Payload = [99] },
            "position" => changedEvents[changed - 1] with { GlobalPosition = changedEvents[changed - 1].GlobalPosition + 1 },
            _ => throw new InvalidOperationException("Unknown contradiction fixture.")
        };
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

    [Fact]
    public async Task TransportPartAdmissionFollowsConfiguredEventAdmissionBeyondTenThousandParts()
    {
        var store = new InMemoryReadModelStore();
        var handler = new OutputOnlyPartitionedHandler();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using var provider = Provider(store, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        var identity = Identity("configured-part-admission");
        (await BoundedRebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            identity, CaptureInventoryFingerprint: "capture", SourceHighWatermarks: new Dictionary<string, long> { ["owner-a"] = 3 }))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var part = await BoundedRebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulatePart,
            identity, AggregateOrdinal: 0, AggregateId: "owner-a", Events: BoundedHistory(1), PartOrdinal: 10_001));
        part.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        part.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryGap);
        var completed = await BoundedRebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulateComplete,
            identity, AggregateOrdinal: 0, AggregateId: "owner-a", PartCount: 10_002, ExpectedHistoryFingerprint: "v2:missing-parts"));
        completed.Status.ShouldBe(ProjectionDispatchStatus.Retryable);
        completed.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryGap);
        (await coordinator.GetStatusAsync(handler.CreateScope("tenant-a"))).ActiveGeneration.ShouldBe(0);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task LegacyUnacceptedInlineManifestResumesWithoutRewritingItsEvidence(bool bufferedOwner, bool malformedDigests)
    {
        var store = new InMemoryReadModelStore();
        PartitionedHandler handler = bufferedOwner ? new OutputOnlyPartitionedHandler() : new StreamedPartitionedHandler();
        handler.FoldOrdinaryRows = true;
        handler.FailBufferedOwnerFold = !bufferedOwner;
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using var provider = Provider(store, store, coordinator, handler);
        var scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        var identity = Identity("legacy-unaccepted-" + bufferedOwner + "-" + malformedDigests);
        var events = BoundedHistory(129);
        string history = DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-a", false, events);
        string inventory = DomainSharedProjectionRebuildFingerprint.AppendInventory(DomainSharedProjectionRebuildFingerprint.EmptyInventory, 0, history);
        (await BoundedRebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            identity, CaptureInventoryFingerprint: inventory, SourceHighWatermarks: new Dictionary<string, long> { ["owner-a"] = events[^1].GlobalPosition }))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await BoundedRebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulatePart,
            identity, AggregateOrdinal: 0, AggregateId: "owner-a", Events: events, PartOrdinal: 0))).Status.ShouldBe(ProjectionDispatchStatus.Completed);

        // Reproduce the old durable boundary exactly: chunks and inline manifest are present,
        // but the session CAS accepting the owner has not happened. No paged inventory exists.
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        string manifestKey = DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(sessionKey, 0);
        byte[] chunk = events[^1].Payload;
        string chunkHash = Convert.ToHexString(SHA256.HashData(chunk));
        await store.SaveAsync(StoreName, DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(sessionKey, 0, 0),
            new DomainSharedProjectionStoredPartition(0, 0, "owner-a", history, chunk));
        string[]? hashPageDigests = null;
        if (!bufferedOwner)
        {
            var page = new DomainSharedProjectionChunkHashPage(0, "owner-a", 0, [chunkHash]);
            await store.SaveAsync(StoreName, DomainSharedProjectionRebuildDispatcher.PartitionHashPageKey(sessionKey, 0, 0), page);
            hashPageDigests = [Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(page)))];
        }
        byte[] digests = events.SelectMany(LegacyDigest).ToArray();
        if (malformedDigests) digests = digests[..^32];
        var legacy = new DomainSharedProjectionStoredPartitionManifest(0, "owner-a", history, false,
            bufferedOwner ? [chunkHash] : [], events.Length, digests, 1, bufferedOwner ? 0 : 1, hashPageDigests);
        var legacyJson = JsonSerializer.SerializeToNode(legacy, JsonSerializerOptions.Web)!.AsObject();
        legacyJson.Remove("capturedIdentityReference").ShouldBeTrue();
        await store.SaveAsync(StoreName, manifestKey, legacyJson);
        var pending = (await store.GetAsync<DomainSharedProjectionRebuildSessionState>(StoreName, sessionKey)).Value!;
        var legacySession = JsonSerializer.SerializeToNode(pending with { PendingScratchChunkCount = 1 }, JsonSerializerOptions.Web)!.AsObject();
        legacySession.Remove("pendingCapturedIdentityReference").ShouldBeTrue();
        await store.SaveAsync(StoreName, sessionKey, legacySession);
        byte[] originalManifest = store.SnapshotRaw(StoreName, manifestKey)!;
        var reservation = SharedProjectionCapturedIdentityStore.Describe(scope, identity.OperationId, "owner-a", events.Length,
            (await coordinator.GetStatusAsync(scope)).Epoch);

        using var replacement = Provider(store, store, new SharedProjectionEpochCoordinator(store, store), handler);
        var resumed = await BoundedRebuildAsync(replacement, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulateComplete,
            identity, AggregateOrdinal: 0, AggregateId: "owner-a", PartCount: 1, ExpectedHistoryFingerprint: history));
        store.SnapshotRaw(StoreName, manifestKey).ShouldBe(originalManifest);
        (await store.GetAsync<System.Text.Json.Nodes.JsonObject>(StoreName, "shared-identity-v1:" + reservation.NamespaceHash)).Value.ShouldBeNull();
        var session = (await store.GetAsync<DomainSharedProjectionRebuildSessionState>(StoreName, sessionKey)).Value!;
        session.PendingCapturedIdentityReference.ShouldBeNull();
        if (malformedDigests)
        {
            resumed.Status.ShouldBe(ProjectionDispatchStatus.Failed);
            session.AcceptedAggregateCount.ShouldBe(0);
            (await coordinator.GetStatusAsync(scope)).ActiveGeneration.ShouldBe(0);
            (await coordinator.GetCheckpointAsync(scope, "owner-a")).ShouldBeNull();
            return;
        }
        resumed.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        session.AcceptedAggregateCount.ShouldBe(1);
        await BoundedFinishAsync(replacement, identity, resumed);
        var checkpoint = (await coordinator.GetCheckpointAsync(scope, "owner-a"))!;
        checkpoint.OwnerSequenceNumber.ShouldBe(events.Length);
        checkpoint.CapturedIdentityReference.ShouldBeNull();
        checkpoint.CapturedEventDigests.ShouldBe(digests);
        foreach (int index in new[] { 0, 64, 128 })
            (await BoundedOrdinaryAsync(replacement, handler, identity, events[index])).ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await BoundedOrdinaryAsync(replacement, handler, identity, BoundedHistory(130)[^1])).ShouldBe(ProjectionDispatchStatus.Completed);

        static byte[] LegacyDigest(ProjectionEventDto item)
        {
            byte[] source = System.Text.Encoding.UTF8.GetBytes("owner-a");
            byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(new ProjectionRequest("tenant-a", "widget", "owner-a", [item]), JsonSerializerOptions.Web);
            using var bytes = new MemoryStream();
            using var writer = new BinaryWriter(bytes);
            writer.Write(source.Length);
            writer.Write(item.GlobalPosition);
            writer.Write(item.SequenceNumber);
            writer.Write(canonical.Length);
            writer.Write(source);
            writer.Write(canonical);
            writer.Flush();
            return SHA256.HashData(bytes.ToArray());
        }
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
