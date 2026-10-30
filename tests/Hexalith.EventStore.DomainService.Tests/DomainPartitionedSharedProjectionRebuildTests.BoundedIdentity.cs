using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService.Tests.Fixtures;
using Hexalith.EventStore.Testing.Fakes;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
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
    [InlineData(1, "message", false)]
    [InlineData(1, "message", true)]
    [InlineData(65, "message", false)]
    [InlineData(65, "message", true)]
    [InlineData(129, "message", false)]
    [InlineData(129, "message", true)]
    [InlineData(1, "payload", false)]
    [InlineData(1, "payload", true)]
    [InlineData(65, "payload", false)]
    [InlineData(65, "payload", true)]
    [InlineData(129, "payload", false)]
    [InlineData(129, "payload", true)]
    [InlineData(1, "position", false)]
    [InlineData(1, "position", true)]
    [InlineData(65, "position", false)]
    [InlineData(65, "position", true)]
    [InlineData(129, "position", false)]
    [InlineData(129, "position", true)]
    public async Task DispatcherRejectsChangedRetainedEarlyMiddleAndFinalIdentitiesBeforePromotion(int changed, string mutation, bool bufferedOwner)
    {
        var store = new InMemoryReadModelStore();
        PartitionedHandler handler = bufferedOwner ? new OutputOnlyPartitionedHandler() : new StreamedPartitionedHandler();
        handler.FoldOrdinaryRows = true;
        handler.FailBufferedOwnerFold = !bufferedOwner;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DispatcherRejectsCrossPageMessageAliasAndAbortRetainsDurableRetirementInventory(bool bufferedOwner)
    {
        var store = new InMemoryReadModelStore();
        PartitionedHandler handler = bufferedOwner ? new OutputOnlyPartitionedHandler() : new StreamedPartitionedHandler();
        handler.FoldOrdinaryRows = true;
        handler.FailBufferedOwnerFold = !bufferedOwner;
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using var provider = Provider(store, store, coordinator, handler);
        var scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        var original = Identity("alias-selected");
        await BoundedFinishAsync(provider, original, await BoundedAccumulateAsync(provider, original, BoundedHistory(129)));
        long selected = (await coordinator.GetStatusAsync(scope)).ActiveGeneration;
        string checkpoint = JsonSerializer.Serialize(await coordinator.GetCheckpointAsync(scope, "owner-a"));
        string row = JsonSerializer.Serialize((await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value);
        var identity = Identity("distant-duplicate");
        var events = BoundedHistory(257);
        events[^1] = events[^1] with { MessageId = events[0].MessageId };
        var outcome = await BoundedAccumulateAsync(provider, identity, events, requireSuccess: false);
        outcome.Status.ShouldBeOneOf(ProjectionDispatchStatus.Indeterminate, ProjectionDispatchStatus.Failed);
        (await coordinator.GetStatusAsync(scope)).ActiveGeneration.ShouldBe(selected);
        JsonSerializer.Serialize(await coordinator.GetCheckpointAsync(scope, "owner-a")).ShouldBe(checkpoint);
        JsonSerializer.Serialize((await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value).ShouldBe(row);
        var session = (await store.GetAsync<DomainSharedProjectionRebuildSessionState>(StoreName, DomainSharedProjectionRebuildSessionKey.Compute(identity))).Value!;
        var reference = session.PendingCapturedIdentityReference;
        if (bufferedOwner) reference.ShouldBeNull();
        else reference.ShouldNotBeNull();
        string[] installed = reference is null ? [] : await InstalledIdentityKeysAsync(store, reference);
        (await BoundedRebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        using var replacement = Provider(store, store, new SharedProjectionEpochCoordinator(store, store), handler);
        (await BoundedRebuildAsync(replacement, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort))).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        if (reference is not null)
        {
            installed.ShouldNotBeEmpty();
            foreach (string key in installed) store.SnapshotRaw(StoreName, key).ShouldBeNull();
            (await store.GetAsync<System.Text.Json.Nodes.JsonObject>(StoreName, "shared-identity-v1:" + reference.NamespaceHash)).Value!["retired"]!.GetValue<bool>().ShouldBeTrue();
        }
        (await coordinator.GetStatusAsync(scope)).ActiveGeneration.ShouldBe(selected);
        JsonSerializer.Serialize(await coordinator.GetCheckpointAsync(scope, "owner-a")).ShouldBe(checkpoint);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DispatcherAbortReclaimsAcceptedAndPendingOwnerProof(bool bufferedOwner, bool accepted)
    {
        var store = new InMemoryReadModelStore();
        var fault = new ReceiptFaultStore(store) { FailNextReceipt = !accepted };
        PartitionedHandler handler = bufferedOwner ? new OutputOnlyPartitionedHandler() : new StreamedPartitionedHandler();
        handler.FailBufferedOwnerFold = !bufferedOwner;
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using var provider = Provider(fault, store, coordinator, handler);
        var scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        var identity = Identity("abort-proof-" + bufferedOwner + "-" + accepted);
        var outcome = await BoundedAccumulateAsync(provider, identity, BoundedHistory(129), requireSuccess: accepted);
        if (!accepted) outcome.Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        var session = (await store.GetAsync<DomainSharedProjectionRebuildSessionState>(StoreName, sessionKey)).Value!;
        session.AcceptedAggregateCount.ShouldBe(accepted ? 1 : 0);
        var manifest = (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(sessionKey, 0))).Value.ShouldNotBeNull();
        var reference = manifest.CapturedIdentityReference.ShouldNotBeNull();
        if (accepted) session.PendingCapturedIdentityReference.ShouldBeNull();
        else session.PendingCapturedIdentityReference.ShouldNotBeNull();
        string[] installed = await InstalledIdentityKeysAsync(store, reference);
        installed.ShouldNotBeEmpty();
        for (int retry = 0; retry < 2; retry++)
        {
            (await BoundedRebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort))).Status
                .ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
            foreach (string key in installed) store.SnapshotRaw(StoreName, key).ShouldBeNull();
            (await store.GetAsync<System.Text.Json.Nodes.JsonObject>(StoreName, "shared-identity-v1:" + reference.NamespaceHash)).Value!["retired"]!.GetValue<bool>().ShouldBeTrue();
            (await coordinator.GetStatusAsync(scope)).ActiveGeneration.ShouldBe(0);
            (await coordinator.GetCheckpointAsync(scope, "owner-a")).ShouldBeNull();
        }
    }

    private static async Task<string[]> InstalledIdentityKeysAsync(InMemoryReadModelStore store,
        SharedProjectionCapturedIdentityReference reference)
    {
        string inventory = "shared-identity-v1:" + reference.NamespaceHash;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        long pages = (reference.Count + 63) / 64;
        for (int level = 0; ; level++)
        {
            for (long ordinal = 0; ordinal < pages; ordinal++)
            {
                string slot = inventory + ":inventory:" + level + ":" + ordinal;
                var page = (await store.GetAsync<SharedProjectionCapturedIdentityPage>(StoreName, slot)).Value;
                if (page is null) continue;
                keys.Add(slot);
                keys.Add(inventory + ":node:" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(page))));
                foreach (string message in page.MessageHashes.Where(item => item.Length != 0)) keys.Add(inventory + ":message:" + message);
            }
            if (pages == 1) break;
            pages = (pages + 127) / 128;
        }
        return [.. keys];
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
        (await BoundedRebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulatePart,
            identity, AggregateOrdinal: 0, AggregateId: "owner-a", Events: BoundedHistory(1), PartOrdinal: 39_999))).ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryGap);
        (await Should.ThrowAsync<ProjectionDispatchValidationException>(() => BoundedRebuildAsync(provider,
            new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulatePart,
                identity, AggregateOrdinal: 0, AggregateId: "owner-a", Events: BoundedHistory(1), PartOrdinal: 40_000))))
            .ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.MalformedOutcome);
        (await BoundedRebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulateComplete,
            identity, AggregateOrdinal: 0, AggregateId: "owner-a", PartCount: 40_000, ExpectedHistoryFingerprint: "v2:missing-parts"))).ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryGap);
        (await Should.ThrowAsync<ProjectionDispatchValidationException>(() => BoundedRebuildAsync(provider,
            new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulateComplete,
                identity, AggregateOrdinal: 0, AggregateId: "owner-a", PartCount: 40_001, ExpectedHistoryFingerprint: "v2:missing-parts"))))
            .ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.MalformedOutcome);
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

    /// <summary>Keeps fresh buffered accumulation at its existing two preparation passes through identity reservation.</summary>
    [Fact]
    public async Task CapturedIdentityReservationDoesNotRepeatBufferedOwnerPreparation()
    {
        var store = new InMemoryReadModelStore();
        var handler = new OutputOnlyPartitionedHandler();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        int preparationPasses = 0;
        var upcaster = Substitute.For<IEventPayloadUpcaster>();
        upcaster.EventTypeName.Returns("Old.Contracts.CounterRaised");
        upcaster.FromVersion.Returns(1);
        upcaster.TargetEventTypeName.Returns(typeof(VersionedProjectionDispatchTests.RenamedEvent).FullName!);
        upcaster.Upcast(Arg.Any<JsonObject>()).Returns(call =>
        {
            preparationPasses++;
            return new JsonObject { ["Value"] = call.Arg<JsonObject>()["Amount"]!.GetValue<int>() };
        });
        var evolution = new EventPayloadEvolutionRegistry([typeof(VersionedProjectionDispatchTests.RenamedEvent)], [upcaster]);
        using var provider = Provider(store, store, coordinator, handler, evolution);
        var scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        var identity = Identity("fresh-buffered-reservation");
        var events = BoundedHistory(1);
        events[0] = events[0] with { EventTypeName = "Old.Contracts.CounterRaised", StoredPayloadVersion = 1, Payload = JsonSerializer.SerializeToUtf8Bytes(new { Amount = 7 }) };

        var accepted = await BoundedAccumulateAsync(provider, identity, events);

        // The existing scratch reservation needs one planning pass and one install pass.
        // Reserving bounded identity must not add a third complete preparation.
        preparationPasses.ShouldBe(2);
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        var session = (await store.GetAsync<DomainSharedProjectionRebuildSessionState>(StoreName, sessionKey)).Value!;
        session.AcceptedAggregateCount.ShouldBe(1);
        session.PendingScratchChunkCount.ShouldBe(-1);
        session.PendingCapturedIdentityReference.ShouldBeNull();
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(sessionKey, 0))).Value!.CapturedIdentityReference.ShouldNotBeNull();
        (await BoundedRebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulateComplete,
            identity, AggregateOrdinal: 0, AggregateId: "owner-a", PartCount: 1,
            ExpectedHistoryFingerprint: DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-a", false, events)))).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        preparationPasses.ShouldBe(2);
        await BoundedFinishAsync(provider, identity, accepted);
        (await coordinator.GetCheckpointAsync(scope, "owner-a"))!.OwnerSequenceNumber.ShouldBe(1);
    }

    /// <summary>Accepts an owner ordinal once when another accumulation completes during identity reservation.</summary>
    [Fact]
    public async Task ConcurrentOrdinalCompletionDuringIdentityReservationCannotAppendInventoryTwice()
    {
        var store = new InMemoryReadModelStore();
        var receiptFault = new ReceiptFaultStore(store) { FailNextReceipt = true };
        var handler = new OutputOnlyPartitionedHandler();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using var provider = Provider(receiptFault, store, coordinator, handler);
        var scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        var identity = Identity("concurrent-buffered-identity-reservation");
        var events = BoundedHistory(1);
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        string history = DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-a", false, events);
        string inventory = DomainSharedProjectionRebuildFingerprint.AppendInventory(
            DomainSharedProjectionRebuildFingerprint.EmptyInventory, 0, history);
        // A receipt outage leaves the real immutable paged manifest installed while
        // ordinal zero is still unaccepted. Its CreateOnly write cannot mask the session fence.
        (await BoundedAccumulateAsync(provider, identity, events, requireSuccess: false)).Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        var pending = (await store.GetAsync<DomainSharedProjectionRebuildSessionState>(StoreName, sessionKey)).Value!;
        pending.AcceptedAggregateCount.ShouldBe(0);
        var reference = pending.PendingCapturedIdentityReference.ShouldNotBeNull();
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(sessionKey, 0))).Value!.CapturedIdentityReference.ShouldNotBeNull();
        var request = new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.AccumulateComplete, identity, AggregateOrdinal: 0,
            AggregateId: "owner-a", PartCount: 1, ExpectedHistoryFingerprint: history);
        var observedStore = new DispositionFailingReadModelStore(store);
        DomainSharedProjectionRebuildResponse? concurrentCompletion = null;
        observedStore.BeforeReadAsync = async key =>
        {
            if (key != "shared-identity-v1:" + reference.NamespaceHash) return;
            observedStore.BeforeReadAsync = null;
            concurrentCompletion = await BoundedRebuildAsync(provider, request);
        };
        using var replacement = Provider(observedStore, store, new SharedProjectionEpochCoordinator(observedStore, store), handler);
        var retried = await BoundedRebuildAsync(replacement, request);

        concurrentCompletion.ShouldNotBeNull();
        concurrentCompletion.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var session = (await store.GetAsync<DomainSharedProjectionRebuildSessionState>(StoreName, sessionKey)).Value!;
        session.AcceptedAggregateCount.ShouldBe(1);
        session.InventoryFingerprint.ShouldBe(inventory);
        session.LastAggregateId.ShouldBe("owner-a");
        session.PendingCapturedIdentityReference.ShouldBeNull();
        retried.Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        retried.AcceptedAggregateCount.ShouldBe(1);
        retried.InventoryFingerprint.ShouldBe(inventory);
        await BoundedFinishAsync(provider, identity, retried);
        (await coordinator.GetCheckpointAsync(scope, "owner-a"))!.OwnerSequenceNumber.ShouldBe(1);
    }

    /// <summary>Refuses evolution that changes after sealing and preserves the prior selected owner state.</summary>
    [Fact]
    public async Task ChangedEvolutionBetweenStreamedPassesCannotReplaceSelectedOwner()
    {
        var store = new InMemoryReadModelStore();
        var handler = new StreamedPartitionedHandler { FailBufferedOwnerFold = true };
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        int evolutionCalls = 0;
        int? validationPassCount = null;
        var upcaster = Substitute.For<IEventPayloadUpcaster>();
        upcaster.EventTypeName.Returns("Old.Contracts.CounterRaised");
        upcaster.FromVersion.Returns(1);
        upcaster.TargetEventTypeName.Returns(typeof(VersionedProjectionDispatchTests.RenamedEvent).FullName!);
        upcaster.Upcast(Arg.Any<JsonObject>()).Returns(call => new JsonObject
        {
            ["Value"] = call.Arg<JsonObject>()["Amount"]!.GetValue<int>()
                + (validationPassCount is { } count && evolutionCalls++ >= count ? 1 : 0)
        });
        var evolution = new EventPayloadEvolutionRegistry([typeof(VersionedProjectionDispatchTests.RenamedEvent)], [upcaster]);
        using var provider = Provider(store, store, coordinator, handler, evolution);
        var scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        var events = BoundedHistory(66).Select(item => item with
        {
            EventTypeName = "Old.Contracts.CounterRaised", StoredPayloadVersion = 1,
            Payload = JsonSerializer.SerializeToUtf8Bytes(new { Amount = item.SequenceNumber })
        }).ToArray();
        var first = Identity("stable-streamed-evolution");
        await BoundedFinishAsync(provider, first, await BoundedAccumulateAsync(provider, first, events[..65]));
        long selected = (await coordinator.GetStatusAsync(scope)).ActiveGeneration;
        string checkpoint = JsonSerializer.Serialize(await coordinator.GetCheckpointAsync(scope, "owner-a"));
        string selectedValue = JsonSerializer.Serialize((await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value);
        evolutionCalls = 0;
        validationPassCount = events.Length;
        int foldedBefore = handler.StreamedEventCount;
        var next = Identity("changed-streamed-evolution");

        var rejected = await BoundedAccumulateAsync(provider, next, events, requireSuccess: false);

        rejected.Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        rejected.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.HandlerFailure);
        evolutionCalls.ShouldBe(events.Length * 2);
        handler.StreamedEventCount.ShouldBe(foldedBefore);
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(next);
        var session = (await store.GetAsync<DomainSharedProjectionRebuildSessionState>(StoreName, sessionKey)).Value!;
        session.AcceptedAggregateCount.ShouldBe(0);
        session.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Accumulating);
        var pending = session.PendingCapturedIdentityReference.ShouldNotBeNull();
        var inventory = (await store.GetAsync<JsonObject>(StoreName, "shared-identity-v1:" + pending.NamespaceHash)).Value!;
        var sealedReference = inventory["reference"]!.Deserialize<SharedProjectionCapturedIdentityReference>(JsonSerializerOptions.Web)!;
        sealedReference.RootDigest.ShouldNotBeEmpty();
        await coordinator.CapturedIdentities.ValidateCompleteAsync(scope, sealedReference);
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(sessionKey, 0))).Value.ShouldBeNull();
        (await coordinator.GetStatusAsync(scope)).ActiveGeneration.ShouldBe(selected);
        JsonSerializer.Serialize(await coordinator.GetCheckpointAsync(scope, "owner-a")).ShouldBe(checkpoint);
        JsonSerializer.Serialize((await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value).ShouldBe(selectedValue);
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
