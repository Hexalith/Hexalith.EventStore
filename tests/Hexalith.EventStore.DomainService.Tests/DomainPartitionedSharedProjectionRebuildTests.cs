using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed class DomainPartitionedSharedProjectionRebuildTests
{
    private const string StoreName = "statestore";

    [Fact]
    public void OwnerFingerprintBindsOptionalProvenanceAndRetainsExistingV1Receipts()
    {
        var source = new ProjectionEventDto("Example.Event", [1, 2], "json", 1,
            new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero), "correlation", "message", "user", 7);
        string current = DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-a", false, [source]);
        current.ShouldStartWith("v2:");

        ProjectionEventDto[] changed =
        [
            source with { MetadataVersion = 2 },
            source with { StoredEventContractType = "contract" },
            source with { StoredPayloadVersion = 2 },
            source with { StoredSerializationFormat = "json-v2" },
            source with { EffectiveEventContractType = "effective" },
            source with { EffectivePayloadVersion = 3 },
            source with { EffectiveSerializationFormat = "json-v3" },
            source with { EffectivePayload = [3, 4] },
            source with { StoredEventTypeName = "stored" },
            source with { StoredDigest = [5, 6] },
            source with { RegistryFingerprint = "registry" },
            source with { IsAdapted = false }
        ];
        foreach (ProjectionEventDto item in changed)
            DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-a", false, [item])
                .ShouldNotBe(current);

        string legacy = DomainSharedProjectionRebuildFingerprint.ComputeHistory(
            "owner-a", false, [source], "v1:existing-receipt");
        legacy.ShouldStartWith("v1:");
        foreach (ProjectionEventDto item in changed)
            DomainSharedProjectionRebuildFingerprint.ComputeHistory(
                "owner-a", false, [item], legacy).ShouldBe(legacy);
    }

    [Fact]
    public async Task AbandonedPreSessionBeginPagesAreReclaimedAcrossOperationsAndRestart()
    {
        var store = new InMemoryReadModelStore();
        var handler = new PartitionedHandler();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity first = Identity("abandoned-begin-first");
        DomainSharedProjectionRebuildIdentity second = Identity("abandoned-begin-second");
        DomainSharedProjectionRebuildIdentity third = Identity("abandoned-begin-third");
        static DomainSharedProjectionRebuildRequest Part(DomainSharedProjectionRebuildIdentity identity) => new(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.BeginPart,
            identity, SourceHighWatermarks: new Dictionary<string, long> { ["owner-a"] = 0 }, PartOrdinal: 0);
        static string PageKey(DomainSharedProjectionRebuildIdentity identity)
            => DomainSharedProjectionRebuildSessionKey.Compute(identity) + ":begin:0";

        (await RebuildAsync(provider, Part(first))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await store.GetAsync<DomainSharedProjectionBeginInventoryPage>(StoreName, PageKey(first)))
            .Value.ShouldNotBeNull();
        using ServiceProvider restarted = Provider(store, store,
            new SharedProjectionEpochCoordinator(store, store), handler);
        (await RebuildAsync(restarted, Part(first))).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await RebuildAsync(restarted, Part(second))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await store.GetAsync<DomainSharedProjectionBeginInventoryPage>(StoreName, PageKey(first)))
            .Value.ShouldNotBeNull();
        await DomainSharedProjectionBeginCleanup.RunPassAsync(restarted, CancellationToken.None);
        (await store.GetAsync<DomainSharedProjectionBeginInventoryPage>(StoreName, PageKey(first)))
            .Value.ShouldBeNull();
        (await store.GetAsync<DomainSharedProjectionBeginInventoryPage>(StoreName, PageKey(second)))
            .Value.ShouldNotBeNull();
        DomainSharedProjectionRebuildResponse abortedSecond = await RebuildAsync(restarted, new(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Abort, second));
        abortedSecond.Status.ShouldBe(ProjectionDispatchStatus.Completed, abortedSecond.ToString());
        abortedSecond.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Aborted);
        (await store.GetAsync<DomainSharedProjectionBeginInventoryPage>(StoreName, PageKey(second)))
            .Value.ShouldBeNull();
        (await RebuildAsync(restarted, Part(third))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await store.GetAsync<DomainSharedProjectionBeginInventoryPage>(StoreName, PageKey(third)))
            .Value.ShouldNotBeNull();
        (await RebuildAsync(restarted, new(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Abort, third))).Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Aborted);
        (await store.GetAsync<DomainSharedProjectionBeginInventoryPage>(StoreName, PageKey(third)))
            .Value.ShouldBeNull();

        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        _ = await coordinator.BeginAsync(scope, "another-active-cut", "other-fingerprint",
            new Dictionary<string, long> { ["owner-a"] = 0 });
        DomainSharedProjectionRebuildIdentity fourth = Identity("rejected-against-other-cut");
        (await RebuildAsync(restarted, Part(fourth))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(restarted, new(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Abort, fourth))).Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Aborted);
        (await store.GetAsync<DomainSharedProjectionBeginInventoryPage>(StoreName, PageKey(fourth)))
            .Value.ShouldBeNull();
        (await coordinator.GetStatusAsync(scope)).OperationId.ShouldBe("another-active-cut");
    }

    [Fact]
    public async Task FailedBeginPageCleanupDoesNotDelaySealedReceiptOrNextOperationAndRecoversAfterRestart()
    {
        var store = new InMemoryReadModelStore();
        var faults = new ScratchFaultBatchStore(store);
        var handler = new PartitionedHandler();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, faults, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity first = Identity("cleanup-independent-first");
        DomainSharedProjectionRebuildIdentity second = Identity("cleanup-independent-second");
        static DomainSharedProjectionRebuildRequest Part(DomainSharedProjectionRebuildIdentity identity) => new(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.BeginPart,
            identity, SourceHighWatermarks: new Dictionary<string, long> { ["owner-a"] = 0 }, PartOrdinal: 0);
        static DomainSharedProjectionRebuildRequest Seal(DomainSharedProjectionRebuildIdentity identity) => new(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            identity, ExpectedAggregateCount: 1, CaptureInventoryFingerprint: new string('F', 64), PartCount: 1);
        static string PageKey(DomainSharedProjectionRebuildIdentity identity)
            => DomainSharedProjectionRebuildSessionKey.Compute(identity) + ":begin:0";

        (await RebuildAsync(provider, Part(first))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        faults.FailNextScratch = true;
        (await RebuildAsync(provider, Seal(first))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await store.GetAsync<DomainSharedProjectionBeginInventoryPage>(StoreName, PageKey(first)))
            .Value.ShouldNotBeNull();
        var failures = new List<Exception>();
        await DomainSharedProjectionBeginCleanup.RunPassAsync(provider, CancellationToken.None, failures.Add);
        failures.ShouldHaveSingleItem();
        (await store.GetAsync<DomainSharedProjectionBeginInventoryPage>(StoreName, PageKey(first)))
            .Value.ShouldNotBeNull();

        // The first cut still holds the ordinary append fence until the producer consumes its receipt.
        // Begin-page cleanup does not participate in that receipt or the next operation's lease transition.
        (await RebuildAsync(provider, Part(second))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        using ServiceProvider restarted = Provider(store, store,
            new SharedProjectionEpochCoordinator(store, store), handler);
        await DomainSharedProjectionBeginCleanup.RunPassAsync(restarted, CancellationToken.None);
        (await store.GetAsync<DomainSharedProjectionBeginInventoryPage>(StoreName, PageKey(first)))
            .Value.ShouldBeNull();
        (await store.GetAsync<DomainSharedProjectionBeginInventoryPage>(StoreName, PageKey(second)))
            .Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task PagedBeginRequiresEveryMaximumLengthOwnerAndErasureMarkerBeforeSealing()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new PartitionedHandler();
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("paged-begin-max-identifiers");
        string[] owners = [.. Enumerable.Range(0, 10_000).Select(index =>
            "o" + index.ToString("D5", System.Globalization.CultureInfo.InvariantCulture) + "-"
            + Convert.ToHexString(RandomNumberGenerator.GetBytes(124)) + "X")];
        owners.All(owner => owner.Length == 256).ShouldBeTrue();
        var parts = new List<DomainSharedProjectionRebuildRequest>();
        for (int ordinal = 0; ordinal < 50; ordinal++)
        {
            string[] pageOwners = [.. owners.Skip(ordinal * 200).Take(200)];
            var request = new DomainSharedProjectionRebuildRequest(
                DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.BeginPart,
                identity, SourceHighWatermarks: pageOwners.ToDictionary(owner => owner, _ => 0L,
                    StringComparer.Ordinal), ErasedOwnerIds: pageOwners, PartOrdinal: ordinal);
            JsonSerializer.SerializeToUtf8Bytes(request).Length.ShouldBeLessThanOrEqualTo(524_288);
            parts.Add(request);
        }

        foreach (DomainSharedProjectionRebuildRequest part in parts.Take(49))
            (await RebuildAsync(provider, part)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, parts[0])).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        var complete = new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            identity, ExpectedAggregateCount: 10_000,
            CaptureInventoryFingerprint: new string('F', 64), PartCount: 50);
        DomainSharedProjectionRebuildResponse omitted = await RebuildAsync(provider, complete);
        omitted.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        (await RebuildAsync(provider, parts[49])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, complete)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, complete)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await RebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Inspect, identity,
            CaptureInventoryFingerprint: new string('F', 64)))).Status
            .ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Inspect, identity,
            CaptureInventoryFingerprint: new string('A', 64)))).Status
            .ShouldBe(ProjectionDispatchStatus.Failed);
        (await coordinator.GetStatusAsync(scope)).OperationId.ShouldBe(identity.OperationId);
        await DomainSharedProjectionBeginCleanup.RunPassAsync(provider, TestContext.Current.CancellationToken);
        for (int ordinal = 0; ordinal < 50; ordinal++)
        {
            string key = DomainSharedProjectionRebuildSessionKey.Compute(identity) + ":begin:" + ordinal;
            (await store.GetAsync<DomainSharedProjectionBeginInventoryPage>(StoreName, key))
                .Value.ShouldBeNull();
        }
        DomainSharedProjectionRebuildIdentity next = Identity("paged-begin-next");
        (await RebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.BeginPart, next,
            SourceHighWatermarks: new Dictionary<string, long> { ["owner-next"] = 0 },
            PartOrdinal: 0))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, parts[0])).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await RebuildAsync(provider, complete)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await store.GetAsync<DomainSharedProjectionBeginInventoryPage>(StoreName,
            DomainSharedProjectionRebuildSessionKey.Compute(next) + ":begin:0"))
            .Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task MaximumStreamedOwnerHashPageManifestFitsTheSdkBatchGate()
    {
        const int maximumChunks = 30_008;
        int pageCount = (maximumChunks + DomainSharedProjectionRebuildDispatcher.ChunkHashesPerPage - 1)
            / DomainSharedProjectionRebuildDispatcher.ChunkHashesPerPage;
        var manifest = new DomainSharedProjectionStoredPartitionManifest(0, new string('a', 256),
            new string('B', 64), false, [], 10_000, RandomNumberGenerator.GetBytes(10_000 * 32),
            10_000, maximumChunks, [.. Enumerable.Repeat(new string('C', 64), pageCount)]);
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var scope = new SharedProjectionScope(StoreName, "tenant-a", "widget",
            "partitioned-widget", ["ordinary-dispatch"]);
        _ = await coordinator.RegisterWriterAsync(scope, "ordinary-dispatch");
        _ = await coordinator.BeginAsync(scope, "maximum-hash-page-manifest", new string('D', 64),
            new Dictionary<string, long> { ["owner-a"] = 10_000 });
        (await coordinator.TryWriteScratchAsync(scope, "maximum-hash-page-manifest", "owner-manifest",
            manifest, "maximum-hash-page-batch", ReadModelBatchConcurrency.CreateOnly)).ShouldBeTrue();
    }

    [Fact]
    public async Task StreamedOwnerCrossesHashPageBoundaryAndReclaimsBothPages()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new StreamedPartitionedHandler { EmitOneChunkPerEvent = true };
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("hash-page-boundary");
        ProjectionEventDto[] events = [.. Enumerable.Range(1, 129).Select(sequence =>
            Event(BitConverter.GetBytes(sequence)) with
            { SequenceNumber = sequence, GlobalPosition = sequence, MessageId = "message-" + sequence })];
        string fingerprint = DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-a", false, events);
        string inventory = DomainSharedProjectionRebuildFingerprint.AppendInventory(
            DomainSharedProjectionRebuildFingerprint.EmptyInventory, 0, fingerprint);
        (await RebuildAsync(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            identity, CaptureInventoryFingerprint: inventory,
            SourceHighWatermarks: new Dictionary<string, long> { ["owner-a"] = 129 })))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulatePart,
            identity, AggregateOrdinal: 0, AggregateId: "owner-a", Events: events, PartOrdinal: 0)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        DomainSharedProjectionRebuildResponse accepted = await RebuildAsync(provider,
            new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.AccumulateComplete, identity, AggregateOrdinal: 0,
                AggregateId: "owner-a", PartCount: 1, ExpectedHistoryFingerprint: fingerprint));
        accepted.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        for (int page = 0; page < 2; page++)
            (await store.GetAsync<DomainSharedProjectionChunkHashPage>(StoreName,
                DomainSharedProjectionRebuildDispatcher.PartitionHashPageKey(sessionKey, 0, page)))
                .Value.ShouldNotBeNull().Hashes.Length.ShouldBe(page == 0 ? 128 : 1);
        string secondPageKey = DomainSharedProjectionRebuildDispatcher.PartitionHashPageKey(sessionKey, 0, 1);
        DomainSharedProjectionChunkHashPage secondPage = (await store
            .GetAsync<DomainSharedProjectionChunkHashPage>(StoreName, secondPageKey)).Value.ShouldNotBeNull();
        await store.SaveAsync(StoreName, secondPageKey, secondPage with { Hashes = [new string('0', 64)] });
        DomainSharedProjectionRebuildSessionState state = (await store
            .GetAsync<DomainSharedProjectionRebuildSessionState>(StoreName, sessionKey)).Value.ShouldNotBeNull();
        var cursor = new DomainSharedProjectionPartitionCursor(store, StoreName, state);
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            _ = await cursor.ReadRangeAsync(0, 128, 1, CancellationToken.None);
        });
        await store.SaveAsync(StoreName, secondPageKey, secondPage);
        (await FinishAsync(provider, identity, accepted)).Status.ShouldBeOneOf(
            ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        for (int page = 0; page < 2; page++)
            (await store.GetAsync<DomainSharedProjectionChunkHashPage>(StoreName,
                DomainSharedProjectionRebuildDispatcher.PartitionHashPageKey(sessionKey, 0, page)))
                .Value.ShouldBeNull();
    }

    [Fact]
    public async Task AbortedNearLimitStreamedOwnerReclaimsItsHashPage()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new StreamedPartitionedHandler();
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("near-limit-hash-page-abort");
        ProjectionEventDto item = Event(RandomNumberGenerator.GetBytes(1_048_576));
        (await BeginAsync(provider, identity, [("owner-a", item)])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulatePart,
            identity, AggregateOrdinal: 0, AggregateId: "owner-a", Events: [item], PartOrdinal: 0)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulateComplete,
            identity, AggregateOrdinal: 0, AggregateId: "owner-a", PartCount: 1,
            ExpectedHistoryFingerprint: DomainSharedProjectionRebuildFingerprint.ComputeHistory(
                "owner-a", false, [item])))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        string key = DomainSharedProjectionRebuildDispatcher.PartitionHashPageKey(
            DomainSharedProjectionRebuildSessionKey.Compute(identity), 0, 0);
        (await store.GetAsync<DomainSharedProjectionChunkHashPage>(StoreName, key)).Value.ShouldNotBeNull();
        (await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort)))
            .Status.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        (await store.GetAsync<DomainSharedProjectionChunkHashPage>(StoreName, key)).Value.ShouldBeNull();
    }

    [Fact]
    public async Task PausedStreamedNearLimitOwnerChunkCannotLandAfterAbort()
    {
        var store = new InMemoryReadModelStore();
        var paused = new PausingOwnerScratchBatchStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, paused);
        var handler = new StreamedPartitionedHandler { FailBufferedOwnerFold = true };
        using ServiceProvider provider = Provider(store, paused, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("paused-streamed-near-limit-chunk");
        ProjectionEventDto item = Event(RandomNumberGenerator.GetBytes(1_048_576));
        string fingerprint = DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-large", false, [item]);
        string inventory = DomainSharedProjectionRebuildFingerprint.AppendInventory(
            DomainSharedProjectionRebuildFingerprint.EmptyInventory, 0, fingerprint);
        (await RebuildAsync(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            identity, CaptureInventoryFingerprint: inventory,
            SourceHighWatermarks: new Dictionary<string, long> { ["owner-large"] = 1 })))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.AccumulatePart, identity,
            AggregateOrdinal: 0, AggregateId: "owner-large", Events: [item], PartOrdinal: 0)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        Task<DomainSharedProjectionRebuildResponse> completing = RebuildAsync(provider,
            new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.AccumulateComplete, identity,
                AggregateOrdinal: 0, AggregateId: "owner-large", PartCount: 1,
                ExpectedHistoryFingerprint: fingerprint));
        await paused.WritePaused.Task.WaitAsync(TimeSpan.FromSeconds(10));
        (await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort)))
            .Status.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        paused.ReleaseWrite.TrySetResult();
        (await completing).Status.ShouldNotBe(ProjectionDispatchStatus.Completed);
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        (await store.GetAsync<DomainSharedProjectionStoredPartition>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(sessionKey, 0, 0))).Value.ShouldBeNull();
        (await store.GetAsync<DomainSharedProjectionTransportPart>(StoreName,
            sessionKey + ":transport:0:0")).Value.ShouldBeNull();
        (await store.GetAsync<DomainSharedProjectionTransportFragment>(StoreName,
            sessionKey + ":transport:0:0:fragment:10")).Value.ShouldBeNull();
    }

    [Fact]
    public async Task StreamedNearLimitOwnerRetriesFirstChunkWriteFailureWithoutOrphans()
    {
        var store = new InMemoryReadModelStore();
        var fault = new FailOnceOwnerScratchBatchStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, fault);
        var handler = new StreamedPartitionedHandler { FailBufferedOwnerFold = true };
        using ServiceProvider provider = Provider(store, fault, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("streamed-near-limit-retry");
        ProjectionEventDto item = Event(RandomNumberGenerator.GetBytes(1_048_576));
        string fingerprint = DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-large", false, [item]);
        string inventory = DomainSharedProjectionRebuildFingerprint.AppendInventory(
            DomainSharedProjectionRebuildFingerprint.EmptyInventory, 0, fingerprint);
        (await RebuildAsync(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            identity, CaptureInventoryFingerprint: inventory,
            SourceHighWatermarks: new Dictionary<string, long> { ["owner-large"] = 1 })))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.AccumulatePart, identity,
            AggregateOrdinal: 0, AggregateId: "owner-large", Events: [item], PartOrdinal: 0)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var completion = new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.AccumulateComplete, identity,
            AggregateOrdinal: 0, AggregateId: "owner-large", PartCount: 1,
            ExpectedHistoryFingerprint: fingerprint);
        fault.FailNextOwnerScratch = true;
        DomainSharedProjectionRebuildResponse first = await RebuildAsync(provider, completion);
        first.Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        fault.FailureCount.ShouldBe(1);
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        DomainSharedProjectionRebuildSessionState pending = (await store
            .GetAsync<DomainSharedProjectionRebuildSessionState>(StoreName, sessionKey)).Value.ShouldNotBeNull();
        pending.PendingScratchChunkCount.ShouldBe(1);
        DomainSharedProjectionRebuildResponse accepted = await RebuildAsync(provider, completion);
        accepted.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, completion)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await FinishAsync(provider, identity, accepted)).Status.ShouldBeOneOf(
            ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        (await store.GetAsync<DomainSharedProjectionStoredPartition>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(sessionKey, 0, 0))).Value.ShouldBeNull();
        (await store.GetAsync<DomainSharedProjectionTransportFragment>(StoreName,
            sessionKey + ":transport:0:0:fragment:10")).Value.ShouldBeNull();
    }

    [Fact]
    public async Task SixtyFourNearLimitEventsCompleteThroughStreamedFoldAndRejectWrongFingerprint()
    {
        const int eventCount = 64;
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new StreamedPartitionedHandler { FailBufferedOwnerFold = true };
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("sixty-four-near-limit-streamed");
        ProjectionEventDto[] events = [.. Enumerable.Range(1, eventCount).Select(sequence =>
            Event(RandomNumberGenerator.GetBytes(1_048_576)) with
            {
                SequenceNumber = sequence, GlobalPosition = sequence, MessageId = "message-" + sequence
            })];
        string fingerprint = DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-large", false, events);
        string inventory = DomainSharedProjectionRebuildFingerprint.AppendInventory(
            DomainSharedProjectionRebuildFingerprint.EmptyInventory, 0, fingerprint);
        (await RebuildAsync(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            identity, CaptureInventoryFingerprint: inventory,
            SourceHighWatermarks: new Dictionary<string, long> { ["owner-large"] = eventCount })))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        for (int ordinal = 0; ordinal < eventCount; ordinal++)
        {
            var part = new DomainSharedProjectionRebuildRequest(
                DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.AccumulatePart, identity,
                AggregateOrdinal: 0, AggregateId: "owner-large", Events: [events[ordinal]],
                PartOrdinal: ordinal);
            JsonSerializer.SerializeToUtf8Bytes(part).Length.ShouldBeLessThanOrEqualTo(2_097_152);
            (await RebuildAsync(provider, part)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        }

        var completion = new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.AccumulateComplete, identity,
            AggregateOrdinal: 0, AggregateId: "owner-large", PartCount: eventCount,
            ExpectedHistoryFingerprint: fingerprint);
        (await RebuildAsync(provider, completion with { ExpectedHistoryFingerprint = "wrong" }))
            .Status.ShouldBe(ProjectionDispatchStatus.Failed);
        handler.StreamedEventCount.ShouldBe(0);
        DomainSharedProjectionRebuildResponse accepted = await RebuildAsync(provider, completion);
        accepted.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        handler.StreamedEventCount.ShouldBe(eventCount);
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        (await store.GetAsync<DomainSharedProjectionChunkHashPage>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionHashPageKey(sessionKey, 0, 0)))
            .Value.ShouldNotBeNull().Hashes.Length.ShouldBe(5);
        var acceptedPart = new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.AccumulatePart, identity,
            AggregateOrdinal: 0, AggregateId: "owner-large", Events: [events[0]], PartOrdinal: 0);
        (await RebuildAsync(provider, acceptedPart)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await RebuildAsync(provider, acceptedPart with
        {
            Events = [events[0] with { Payload = RandomNumberGenerator.GetBytes(1_048_576) }]
        })).Status.ShouldBe(ProjectionDispatchStatus.Failed);
        (await RebuildAsync(provider, completion)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await FinishAsync(provider, identity, accepted)).Status.ShouldBeOneOf(
            ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        (await coordinator.ReadAsync<PayloadRow>(handler.CreateScope("tenant-a"), "row:owner-large"))
            .Value.ShouldNotBeNull().Value.ShouldBe(Convert.ToHexString(SHA256.HashData(events[^1].Payload)));
        (await store.GetAsync<DomainSharedProjectionChunkHashPage>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionHashPageKey(sessionKey, 0, 0)))
            .Value.ShouldBeNull();
        (await store.GetAsync<DomainSharedProjectionTransportFragment>(StoreName,
            sessionKey + ":transport:0:63:fragment:10")).Value.ShouldBeNull();
    }

    [Fact]
    public async Task PausedNearLimitTransportFragmentCannotLandAfterAbort()
    {
        var store = new InMemoryReadModelStore();
        var paused = new PausingTransportScratchBatchStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, paused);
        var handler = new StreamedPartitionedHandler();
        using ServiceProvider provider = Provider(store, paused, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("paused-near-limit-transport");
        ProjectionEventDto item = Event(RandomNumberGenerator.GetBytes(1_048_576));
        _ = await BeginAsync(provider, identity, [("owner-large", item)]);
        var part = new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.AccumulatePart, identity,
            AggregateOrdinal: 0, AggregateId: "owner-large", Events: [item], PartOrdinal: 0);
        Task<DomainSharedProjectionRebuildResponse> write = RebuildAsync(provider, part);
        await paused.WritePaused.Task.WaitAsync(TimeSpan.FromSeconds(10));
        (await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort)))
            .Status.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        paused.ReleaseWrite.TrySetResult();
        (await write).Status.ShouldNotBe(ProjectionDispatchStatus.Completed);
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        (await store.GetAsync<DomainSharedProjectionTransportPart>(StoreName,
            sessionKey + ":transport:0:0")).Value.ShouldBeNull();
        for (int fragment = 0; fragment < 11; fragment++)
        {
            (await store.GetAsync<DomainSharedProjectionTransportFragment>(StoreName,
                sessionKey + ":transport:0:0:fragment:" + fragment)).Value.ShouldBeNull();
        }
    }

    [Fact]
    public async Task NearLimitHighEntropyPartsReplayAndAbortReclaimPrivateTransport()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new StreamedPartitionedHandler();
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("near-limit-transport-abort");
        ProjectionEventDto[] events =
        [
            Event(RandomNumberGenerator.GetBytes(1_048_576)) with
            { SequenceNumber = 1, GlobalPosition = 1, MessageId = "message-1" },
            Event(RandomNumberGenerator.GetBytes(1_048_576)) with
            { SequenceNumber = 2, GlobalPosition = 2, MessageId = "message-2" }
        ];
        string historyFingerprint = DomainSharedProjectionRebuildFingerprint.ComputeHistory(
            "owner-large", false, events);
        string inventory = DomainSharedProjectionRebuildFingerprint.AppendInventory(
            DomainSharedProjectionRebuildFingerprint.EmptyInventory, 0, historyFingerprint);
        (await RebuildAsync(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            identity, CaptureInventoryFingerprint: inventory,
            SourceHighWatermarks: new Dictionary<string, long> { ["owner-large"] = 2 })))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);

        var first = new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.AccumulatePart, identity,
            AggregateOrdinal: 0, AggregateId: "owner-large", Events: [events[0]], PartOrdinal: 0);
        var second = first with { Events = [events[1]], PartOrdinal = 1 };
        JsonSerializer.SerializeToUtf8Bytes(first).Length.ShouldBeLessThanOrEqualTo(2_097_152);
        JsonSerializer.SerializeToUtf8Bytes(second).Length.ShouldBeLessThanOrEqualTo(2_097_152);
        (await RebuildAsync(provider, first)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, first)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await RebuildAsync(provider, second)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort)))
            .Status.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        (await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort)))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        for (int ordinal = 0; ordinal < 2; ordinal++)
        {
            (await store.GetAsync<DomainSharedProjectionTransportPart>(StoreName,
                sessionKey + ":transport:0:" + ordinal)).Value.ShouldBeNull();
            for (int fragment = 0; fragment < 11; fragment++)
            {
                (await store.GetAsync<DomainSharedProjectionTransportFragment>(StoreName,
                    sessionKey + ":transport:0:" + ordinal + ":fragment:" + fragment))
                    .Value.ShouldBeNull();
            }
        }
        (await coordinator.GetStatusAsync(handler.CreateScope("tenant-a"))).IsBuilding.ShouldBeFalse();

        DomainSharedProjectionRebuildIdentity committed = Identity("near-limit-transport-commit");
        (await RebuildAsync(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            committed, CaptureInventoryFingerprint: inventory,
            SourceHighWatermarks: new Dictionary<string, long> { ["owner-large"] = 2 })))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, first with { Identity = committed }))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, second with { Identity = committed }))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        DomainSharedProjectionRebuildResponse accepted = await RebuildAsync(provider,
            new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.AccumulateComplete, committed,
                AggregateOrdinal: 0, AggregateId: "owner-large", PartCount: 2,
                ExpectedHistoryFingerprint: historyFingerprint));
        accepted.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await FinishAsync(provider, committed, accepted)).Status.ShouldBeOneOf(
            ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        (await coordinator.ReadAsync<PayloadRow>(handler.CreateScope("tenant-a"), "row:owner-large"))
            .Value.ShouldNotBeNull().Value.ShouldBe(
                Convert.ToHexString(SHA256.HashData(events[1].Payload)));
        (await RebuildAsync(provider, Lifecycle(committed, DomainSharedProjectionRebuildAction.Commit)))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        string committedKey = DomainSharedProjectionRebuildSessionKey.Compute(committed);
        (await store.GetAsync<DomainSharedProjectionTransportFragment>(StoreName,
            committedKey + ":transport:0:1:fragment:10")).Value.ShouldBeNull();
    }

    [Fact]
    public async Task TenThousandEventOwnerAccumulatesThroughBoundedRetrySafeParts()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new StreamedPartitionedHandler();
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("partitioned-transport-10000");
        ProjectionEventDto[] events = Enumerable.Range(1, 10_000)
            .Select(sequence => Event(RandomNumberGenerator.GetBytes(768)) with
            {
                SequenceNumber = sequence,
                GlobalPosition = sequence,
                MessageId = "message-" + sequence
            }).ToArray();
        string historyFingerprint = DomainSharedProjectionRebuildFingerprint.ComputeHistory(
            "owner-large", false, events);
        string inventory = DomainSharedProjectionRebuildFingerprint.AppendInventory(
            DomainSharedProjectionRebuildFingerprint.EmptyInventory, 0, historyFingerprint);
        (await RebuildAsync(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            identity, CaptureInventoryFingerprint: inventory,
            SourceHighWatermarks: new Dictionary<string, long> { ["owner-large"] = 10_000 })))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);

        const int eventsPerPart = 128;
        int partCount = (events.Length + eventsPerPart - 1) / eventsPerPart;
        for (int part = 0; part < partCount; part++)
        {
            ProjectionEventDto[] page = [.. events.Skip(part * eventsPerPart).Take(eventsPerPart)];
            var request = new DomainSharedProjectionRebuildRequest(
                DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.AccumulatePart, identity,
                AggregateOrdinal: 0, AggregateId: "owner-large", Events: page, PartOrdinal: part);
            (await RebuildAsync(provider, request)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            if (part == 0)
            {
                (await RebuildAsync(provider, request)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
                (await RebuildAsync(provider, request with
                {
                    Events = [page[0] with { Payload = [0] }, .. page.Skip(1)]
                })).Status.ShouldBe(ProjectionDispatchStatus.Failed);
            }
        }

        var complete = new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.AccumulateComplete, identity,
            AggregateOrdinal: 0, AggregateId: "owner-large", PartCount: partCount,
            ExpectedHistoryFingerprint: historyFingerprint);
        DomainSharedProjectionRebuildResponse accepted = await RebuildAsync(provider, complete);
        accepted.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, complete)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await FinishAsync(provider, identity, accepted)).Status.ShouldBeOneOf(
            ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        PayloadRow selected = (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-large"))
            .Value.ShouldNotBeNull();
        selected.Value.ShouldBe(Convert.ToHexString(SHA256.HashData(events[^1].Payload)));

        (await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Commit)))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        string partKey = DomainSharedProjectionRebuildSessionKey.Compute(identity) + ":transport:0:0";
        (await store.GetAsync<DomainSharedProjectionTransportPart>(StoreName, partKey)).Value.ShouldBeNull();
    }

    [Fact]
    public async Task ActivePositiveHeadCannotBeAccumulatedAsErased()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new PartitionedHandler();
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("active-owner-erasure-rejected");
        string fingerprint = DomainSharedProjectionRebuildFingerprint.AppendInventory(
            DomainSharedProjectionRebuildFingerprint.EmptyInventory, 0,
            DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-a", true, []));
        (await RebuildAsync(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            identity, CaptureInventoryFingerprint: fingerprint,
            SourceHighWatermarks: new Dictionary<string, long> { ["owner-a"] = 1 },
            ErasedOwnerIds: []))).Status.ShouldBe(ProjectionDispatchStatus.Completed);

        DomainSharedProjectionRebuildResponse rejected = await RebuildAsync(provider,
            new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.Accumulate, identity,
                AggregateOrdinal: 0, AggregateId: "owner-a", IsErased: true, Events: []));

        rejected.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        rejected.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        (await coordinator.GetStatusAsync(handler.CreateScope("tenant-a"))).ActiveGeneration.ShouldBe(0);
    }

    [Fact]
    public async Task SealedPositiveHeadTombstoneMayBeOmittedWhileOtherOwnerPromotes()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new PartitionedHandler();
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("sealed-erased-tombstone");
        ProjectionEventDto active = Event([2]) with { GlobalPosition = 2 };
        string fingerprint = DomainSharedProjectionRebuildFingerprint.AppendInventory(
            DomainSharedProjectionRebuildFingerprint.AppendInventory(
                DomainSharedProjectionRebuildFingerprint.EmptyInventory, 0,
                DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-a", true, [])),
            1, DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-b", false, [active]));
        (await RebuildAsync(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            identity, CaptureInventoryFingerprint: fingerprint,
            SourceHighWatermarks: new Dictionary<string, long> { ["owner-a"] = 1, ["owner-b"] = 2 },
            ErasedOwnerIds: ["owner-a"]))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        DomainSharedProjectionRebuildResponse falseActive = await RebuildAsync(provider,
            new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.Accumulate, identity,
                AggregateOrdinal: 0, AggregateId: "owner-a", Events: [Event([1])]));
        falseActive.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        falseActive.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        DomainSharedProjectionRebuildResponse current = await RebuildAsync(provider,
            new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.Accumulate, identity,
                AggregateOrdinal: 0, AggregateId: "owner-a", IsErased: true, Events: []));
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        current = await AccumulateAsync(provider, identity, 1, ("owner-b", active));

        (await FinishAsync(provider, identity, current)).Status.ShouldBeOneOf(
            ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value.ShouldBeNull();
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-b")).Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task CapturedReplayRejectsChangedEarlierEnvelopeWhenFinalPositionMatches()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new PartitionedHandler();
        var evolution = new EventPayloadEvolutionRegistry(
            [typeof(VersionedProjectionDispatchTests.RenamedEvent)],
            [new VersionedProjectionDispatchTests.RenameStep()]);
        using ServiceProvider provider = Provider(store, store, coordinator, handler, evolution);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("captured-earlier-envelope");
        ProjectionEventDto[] events =
        [
            Event(JsonSerializer.SerializeToUtf8Bytes(new { Amount = 5 })) with
            {
                EventTypeName = "Old.Contracts.CounterRaised", StoredPayloadVersion = 1,
                SequenceNumber = 1, GlobalPosition = 1, MessageId = "message-1"
            },
            Event([2]) with { SequenceNumber = 2, GlobalPosition = 3, MessageId = "message-2" }
        ];
        string capture = DomainSharedProjectionRebuildFingerprint.AppendInventory(
            DomainSharedProjectionRebuildFingerprint.EmptyInventory, 0,
            DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-a", false, events));
        DomainSharedProjectionRebuildResponse begun = await RebuildAsync(provider,
            new DomainSharedProjectionRebuildRequest(
                DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
                identity, CaptureInventoryFingerprint: capture,
                SourceHighWatermarks: new Dictionary<string, long> { ["owner-a"] = 3 }));
        begun.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        DomainSharedProjectionRebuildResponse accumulated = await RebuildAsync(provider,
            new DomainSharedProjectionRebuildRequest(
                DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Accumulate,
                identity, AggregateOrdinal: 0, AggregateId: "owner-a", Events: events));
        accumulated.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await FinishAsync(provider, identity, accumulated)).Status.ShouldBeOneOf(
            ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);

        async Task<ProjectionDispatchResponse> DeliverAsync(ProjectionEventDto item, string dispatchId)
            => await DomainProjectionDispatcher.DispatchAsync(provider,
                new ProjectionDispatchRequest(
                    new ProjectionRequest("tenant-a", "widget", "owner-a", [item]),
                    [handler.ProjectionType], dispatchId, identity.CatalogFingerprint),
                new ProjectionDispatchOptions(),
                new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
                CancellationToken.None);

        ProjectionDispatchResponse changed = await DeliverAsync(events[0] with { GlobalPosition = 2 },
            "changed-earlier-position");
        changed.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Failed);
        changed.Outcomes.Single().ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        ProjectionDispatchResponse exact = await DeliverAsync(events[0], "exact-earlier-position");
        exact.Outcomes.Single().Status.ShouldBeOneOf(
            ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        ProjectionDispatchResponse changedPayload = await DeliverAsync(events[0] with
        {
            Payload = JsonSerializer.SerializeToUtf8Bytes(new { Amount = 6 })
        }, "changed-evolved-payload");
        changedPayload.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Failed);
    }

    [Fact]
    public async Task CommitRejectsACapturedOwnerMissingFromAcceptedManifests()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new PartitionedHandler();
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("omitted-captured-owner");
        (string Owner, ProjectionEventDto Event)[] captured =
        [
            ("owner-a", Event([1]) with { GlobalPosition = 1 }),
            ("owner-b", Event([2]) with { GlobalPosition = 2 })
        ];
        string incompleteFingerprint = DomainSharedProjectionRebuildFingerprint.AppendInventory(
            DomainSharedProjectionRebuildFingerprint.EmptyInventory, 0,
            DomainSharedProjectionRebuildFingerprint.ComputeHistory(captured[0].Owner, false,
                [captured[0].Event]));
        (await RebuildAsync(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
            identity, CaptureInventoryFingerprint: incompleteFingerprint,
            SourceHighWatermarks: new Dictionary<string, long>
            {
                ["owner-a"] = 1,
                ["owner-b"] = 2
            }))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        DomainSharedProjectionRebuildResponse current = await AccumulateAsync(provider, identity, 0, captured[0]);
        (await RebuildAsync(provider, Finalize(identity, current))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Stage)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);

        DomainSharedProjectionRebuildResponse rejected = await RebuildAsync(provider,
            Lifecycle(identity, DomainSharedProjectionRebuildAction.Commit));

        rejected.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        rejected.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        (await coordinator.GetStatusAsync(scope)).ActiveGeneration.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task PartitionOwnerHistoryMustEndAtSealedCapturePosition(long finalPosition)
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new PartitionedHandler();
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("partitioned-capture-mismatch");
        (string Owner, ProjectionEventDto Event)[] captured = [("owner-a", Event([1, 2, 3]))];
        _ = await BeginAsync(provider, identity, captured);

        DomainSharedProjectionRebuildResponse result = await AccumulateAsync(provider, identity, 0,
            ("owner-a", captured[0].Event with { GlobalPosition = finalPosition }));

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(sessionKey, 0))).Value.ShouldBeNull();
    }

    [Fact]
    public async Task PartitionOwnerHistoryRejectsBackwardPositionsEvenWhenFinalMatchesCapture()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new PartitionedHandler();
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("partitioned-backward-capture");
        (string Owner, ProjectionEventDto Event)[] captured = [("owner-a", Event([1, 2, 3]) with { GlobalPosition = 3 })];
        _ = await BeginAsync(provider, identity, captured);
        ProjectionEventDto[] events =
        [
            Event([1]) with { SequenceNumber = 1, GlobalPosition = 2, MessageId = "message-1" },
            Event([2]) with { SequenceNumber = 2, GlobalPosition = 1, MessageId = "message-2" },
            Event([3]) with { SequenceNumber = 3, GlobalPosition = 3, MessageId = "message-3" }
        ];

        DomainSharedProjectionRebuildResponse result = await RebuildAsync(provider,
            new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.Accumulate, identity, AggregateOrdinal: 0,
                AggregateId: "owner-a", Events: events));

        result.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        result.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
    }

    [Fact]
    public async Task DispatcherHandoffRejectsCheckpointBehindSealedCapture()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new PartitionedHandler();
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("partitioned-behind-capture");
        (string Owner, ProjectionEventDto Event)[] history = [("owner-a", Event([1, 2, 3]))];
        DomainSharedProjectionRebuildResponse current = await BeginAsync(provider, identity, history);
        current = await AccumulateAsync(provider, identity, 0, history[0]);
        (await RebuildAsync(provider, Finalize(identity, current))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Stage)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        string checkpointKey = "shared-checkpoint:" + scope.ComputeHash() + ":"
            + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("owner-a")));
        (await store.TrySaveAsync(scope.StoreName, checkpointKey,
            new SharedProjectionCheckpoint(0, [0], 1), string.Empty)).ShouldBeTrue();

        DomainSharedProjectionRebuildResponse rejected = await RebuildAsync(provider,
            Lifecycle(identity, DomainSharedProjectionRebuildAction.Commit));
        rejected.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        rejected.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        (await coordinator.GetStatusAsync(scope)).ActiveGeneration.ShouldBe(0);
        (await coordinator.GetCheckpointAsync(scope, "owner-a"))!.Position.ShouldBe(0);
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.BeginAsync(
            scope, "next-epoch", "next-inventory", new Dictionary<string, long>()));
    }

    [Fact]
    public async Task CommitRejectsCheckpointSequenceAheadOfCapturedHistoryBeforePromotion()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new PartitionedHandler();
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("partitioned-excess-sequence");
        (string Owner, ProjectionEventDto Event)[] history = [("owner-a", Event([1, 2, 3]))];
        DomainSharedProjectionRebuildResponse current = await BeginAsync(provider, identity, history);
        current = await AccumulateAsync(provider, identity, 0, history[0]);
        (await RebuildAsync(provider, Finalize(identity, current))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Stage)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await store.TrySaveAsync(scope.StoreName, CheckpointKey(scope, "owner-a"),
            new SharedProjectionCheckpoint(1, [1], 2), string.Empty)).ShouldBeTrue();

        DomainSharedProjectionRebuildResponse rejected = await RebuildAsync(provider,
            Lifecycle(identity, DomainSharedProjectionRebuildAction.Commit));
        rejected.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        rejected.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        (await coordinator.GetStatusAsync(scope)).ActiveGeneration.ShouldBe(0);
    }

    [Fact]
    public async Task StaleCaptureIsRejectedBeforePromotionAndFreshRetryCanCommit()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new PartitionedHandler();
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        (await store.TrySaveAsync(scope.StoreName, CheckpointKey(scope, "owner-a"),
            new SharedProjectionCheckpoint(1, [1], 1), string.Empty)).ShouldBeTrue();
        DomainSharedProjectionRebuildIdentity stale = Identity("partitioned-stale-capture");
        DomainSharedProjectionRebuildResponse rejected = await RebuildAsync(provider,
            new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version,
                DomainSharedProjectionRebuildAction.Begin, stale,
                CaptureInventoryFingerprint: "stale-inventory",
                SourceHighWatermarks: new Dictionary<string, long> { ["owner-a"] = 0 }));
        rejected.Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        (await coordinator.GetStatusAsync(scope)).ActiveGeneration.ShouldBe(0);

        DomainSharedProjectionRebuildIdentity fresh = Identity("partitioned-fresh-capture");
        (string Owner, ProjectionEventDto Event)[] history = [("owner-a", Event([1, 2, 3]))];
        DomainSharedProjectionRebuildResponse current = await BeginAsync(provider, fresh, history);
        current = await AccumulateAsync(provider, fresh, 0, history[0]);
        (await FinishAsync(provider, fresh, current)).Status.ShouldBeOneOf(
            ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        (await coordinator.GetStatusAsync(scope)).ActiveGeneration.ShouldBe(1);
    }

    [Fact]
    public async Task RebuiltOwnerWithoutOrdinaryCheckpointAcceptsNextSequenceAndCleansScratch()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new PartitionedHandler();
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("partitioned-sequence-handoff");
        (string Owner, ProjectionEventDto Event)[] history = [("owner-a", Event([1, 2, 3]))];
        DomainSharedProjectionRebuildResponse current = await BeginAsync(provider, identity, history);
        current = await AccumulateAsync(provider, identity, 0, history[0]);
        current = await FinishAsync(provider, identity, current);
        current.Status.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);

        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(sessionKey, 0))).Value.ShouldBeNull();
        (await store.GetAsync<DomainSharedProjectionStoredPartition>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(sessionKey, 0, 0))).Value.ShouldBeNull();
        string operationHash = Convert.ToHexString(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(identity.OperationId)));
        foreach (string batchId in new[]
        {
            $"owner-scratch:{operationHash}:0:0",
            $"owner-scratch:{operationHash}:0:-1",
            $"scratch-cleanup:{operationHash}:chunks:0:0",
            $"scratch-cleanup:{operationHash}:manifest:0"
        })
        {
            string aggregateId = batchId.StartsWith("scratch-cleanup:", StringComparison.Ordinal)
                ? "shared-" + handler.ProjectionType
                : scope.Family;
            string receiptKey = new ReadModelBatchScope(StoreName, scope.TenantId, scope.Domain,
                aggregateId, scope.Family, batchId).ComputeMarkerKey();
            (await store.GetAsync<JsonObject>(StoreName, receiptKey)).Value.ShouldBeNull();
        }
        DomainSharedProjectionRebuildSessionState session = (await store.GetAsync<DomainSharedProjectionRebuildSessionState>(
            StoreName, sessionKey)).Value.ShouldNotBeNull();
        session.ScratchCleanedOwnerCount.ShouldBe(1);

        SharedProjectionCheckpoint checkpoint = (await coordinator.GetCheckpointAsync(scope, "owner-a"))!;
        checkpoint.OwnerSequenceNumber.ShouldBe(1);
        async Task<ProjectionDispatchResponse> DeliverCapturedAsync(ProjectionEventDto source, string dispatchId)
            => await DomainProjectionDispatcher.DispatchAsync(provider,
                new ProjectionDispatchRequest(
                    new ProjectionRequest("tenant-a", "widget", "owner-a", [source]),
                    [handler.ProjectionType], dispatchId, identity.CatalogFingerprint),
                new ProjectionDispatchOptions(),
                new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
                CancellationToken.None);

        ProjectionDispatchResponse replay = await DeliverCapturedAsync(history[0].Event, "captured-replay");
        replay.Outcomes.Single().Status.ShouldBeOneOf(
            ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        ProjectionDispatchResponse altered = await DeliverCapturedAsync(
            history[0].Event with { Payload = [1, 2, 4] }, "captured-altered");
        altered.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Failed);
        altered.Outcomes.Single().ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);

        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(scope, handler.OrdinaryWriterId);
        (await coordinator.JournalAsync(scope, lease,
            new SharedProjectionDelivery("owner-a", 2, [4], [2], OwnerSequenceNumber: 2)))
            .ShouldBe(SharedProjectionJournalResult.Journaled);
        (await coordinator.JournalAsync(scope, lease,
            new SharedProjectionDelivery("owner-a", 4, [6], [4], OwnerSequenceNumber: 4)))
            .ShouldBe(SharedProjectionJournalResult.DeliveryGap);
    }

    [Fact]
    public async Task PromotedGenerationKeepsFenceUntilOwnerSequenceHandoffCompletes()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new PartitionedHandler();
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity first = Identity("partitioned-handoff-fence");
        (string Owner, ProjectionEventDto Event)[] history = [("owner-a", Event([1, 2, 3]))];
        DomainSharedProjectionRebuildResponse current = await BeginAsync(provider, first, history);
        current = await AccumulateAsync(provider, first, 0, history[0]);
        current = await RebuildAsync(provider, Finalize(first, current));
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, Lifecycle(first, DomainSharedProjectionRebuildAction.Stage)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);

        // Stop precisely after SDK promotion, before the dispatcher seeds owner checkpoints.
        await coordinator.CommitAsync(scope, first.OperationId, requireOwnerSequenceHandoff: true);
        SharedProjectionLease lease = await coordinator.RefreshLeaseAsync(scope, handler.OrdinaryWriterId);
        (await coordinator.JournalAsync(scope, lease,
            new SharedProjectionDelivery("owner-a", 2, [4], [2], OwnerSequenceNumber: 2)))
            .ShouldBe(SharedProjectionJournalResult.Backpressure);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.BeginAsync(
            scope, "partitioned-next", "next-inventory", new Dictionary<string, long> { ["owner-a"] = 1 }));
        (await coordinator.CatchUpAsync(scope,
            static (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]))).ShouldBe(0);
        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.BeginAsync(
            scope, "partitioned-next", "next-inventory", new Dictionary<string, long> { ["owner-a"] = 1 }));

        (await RebuildAsync(provider, Lifecycle(first, DomainSharedProjectionRebuildAction.Commit)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await coordinator.GetCheckpointAsync(scope, "owner-a"))!.OwnerSequenceNumber.ShouldBe(1);
        (await coordinator.JournalAsync(scope, lease,
            new SharedProjectionDelivery("owner-a", 2, [4], [2], OwnerSequenceNumber: 2)))
            .ShouldBe(SharedProjectionJournalResult.Journaled);
        _ = await coordinator.CatchUpAsync(scope,
            static (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));
        (await coordinator.BeginAsync(scope, "partitioned-next", "next-inventory",
            new Dictionary<string, long> { ["owner-a"] = 2 })).ShouldBe(2);
    }

    [Fact]
    public async Task DispatcherCommitKeepsDeliveryAndNextRebuildFencedUntilSeedingCompletes()
    {
        var store = new InMemoryReadModelStore();
        var paused = new PausingCheckpointStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(paused, store);
        var handler = new PartitionedHandler();
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("dispatcher-seeding-race");
        (string Owner, ProjectionEventDto Event)[] history = [("owner-a", Event([1, 2, 3]))];
        DomainSharedProjectionRebuildResponse current = await BeginAsync(provider, identity, history);
        current = await AccumulateAsync(provider, identity, 0, history[0]);
        (await RebuildAsync(provider, Finalize(identity, current))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Stage)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);

        paused.PauseNextCheckpointRead = true;
        Task<DomainSharedProjectionRebuildResponse> commit = RebuildAsync(provider,
            Lifecycle(identity, DomainSharedProjectionRebuildAction.Commit));
        await paused.ReadPaused.Task.WaitAsync(TimeSpan.FromSeconds(10));
        ProjectionEventDto next = Event([4]) with { SequenceNumber = 2, GlobalPosition = 2, MessageId = "message-2" };
        ProjectionDispatchResponse delivery = await DomainProjectionDispatcher.DispatchAsync(provider,
            new ProjectionDispatchRequest(
                new ProjectionRequest("tenant-a", "widget", "owner-a", [next]),
                [handler.ProjectionType], "delivery-after-promotion", identity.CatalogFingerprint),
            new ProjectionDispatchOptions(),
            new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
            CancellationToken.None);
        delivery.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Retryable);

        DomainSharedProjectionRebuildResponse nextBegin = await BeginAsync(provider,
            Identity("dispatcher-next-rebuild"), history);
        nextBegin.Status.ShouldNotBe(ProjectionDispatchStatus.Completed);
        paused.ReleaseRead.TrySetResult();
        (await commit).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        delivery = await DomainProjectionDispatcher.DispatchAsync(provider,
            new ProjectionDispatchRequest(
                new ProjectionRequest("tenant-a", "widget", "owner-a", [next]),
                [handler.ProjectionType], "delivery-after-seeding", identity.CatalogFingerprint),
            new ProjectionDispatchOptions(),
            new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
            CancellationToken.None);
        delivery.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
    }

    [Fact]
    public async Task SelectedReadAndLiveDeliveryProceedWhenCommittedScratchCleanupFails()
    {
        var store = new InMemoryReadModelStore();
        var fault = new ScratchFaultBatchStore(store);
        var handler = new PartitionedHandler();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, fault, coordinator, handler);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("cleanup-after-handoff");
        (string Owner, ProjectionEventDto Event)[] history = [("owner-a", Event([1, 2, 3]))];
        DomainSharedProjectionRebuildResponse current = await BeginAsync(provider, identity, history);
        current = await AccumulateAsync(provider, identity, 0, history[0]);
        current = await RebuildAsync(provider, Finalize(identity, current));
        current = await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Stage));
        fault.FailNextScratch = true;
        (await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Commit)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);

        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value.ShouldNotBeNull();
        ProjectionEventDto next = Event([4]) with
        {
            SequenceNumber = 2, GlobalPosition = 2, MessageId = "message-2"
        };
        ProjectionDispatchResponse live = await DomainProjectionDispatcher.DispatchAsync(provider,
            new ProjectionDispatchRequest(new ProjectionRequest("tenant-a", "widget", "owner-a", [next]),
                [handler.ProjectionType], "live-during-private-cleanup", identity.CatalogFingerprint),
            new ProjectionDispatchOptions(),
            new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
            CancellationToken.None);
        live.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(sessionKey, 0))).Value.ShouldNotBeNull();

        // The hosted pass consumes the durable work item after the response. A failed
        // deletion leaves the item intact for a later pass on this or another replica.
        await DomainSharedProjectionScratchCleanup.RunPassAsync(provider, new ProjectionDispatchOptions(),
            CancellationToken.None);
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(sessionKey, 0))).Value.ShouldNotBeNull();
        await DomainSharedProjectionScratchCleanup.RunPassAsync(provider, new ProjectionDispatchOptions(),
            CancellationToken.None);
        (await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Verify)))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(sessionKey, 0))).Value.ShouldBeNull();
    }

    [Fact]
    public async Task CommittedAndAbortedScratchCleanupResumesAfterStoreFailure()
    {
        var store = new InMemoryReadModelStore();
        var fault = new ScratchFaultBatchStore(store);
        var handler = new PartitionedHandler();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, fault, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity committed = Identity("partitioned-cleanup-retry");
        (string Owner, ProjectionEventDto Event)[] history = [("owner-a", Event([1, 2, 3]))];
        DomainSharedProjectionRebuildResponse current = await BeginAsync(provider, committed, history);
        current = await AccumulateAsync(provider, committed, 0, history[0]);
        current = await RebuildAsync(provider, Finalize(committed, current));
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        current = await RebuildAsync(provider, Lifecycle(committed, DomainSharedProjectionRebuildAction.Stage));
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        fault.FailNextScratch = true;
        (await RebuildAsync(provider, Lifecycle(committed, DomainSharedProjectionRebuildAction.Commit)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);

        string committedKey = DomainSharedProjectionRebuildSessionKey.Compute(committed);
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(committedKey, 0))).Value.ShouldNotBeNull();
        var restarted = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider restartProvider = Provider(store, fault, restarted, handler);
        DomainSharedProjectionRebuildIdentity later = Identity("later-epoch");
        (await BeginAsync(restartProvider, later, history)).Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(committedKey, 0))).Value.ShouldNotBeNull();
        await DomainSharedProjectionScratchCleanup.RunPassAsync(restartProvider, new ProjectionDispatchOptions(),
            CancellationToken.None);
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(committedKey, 0))).Value.ShouldBeNull();
        (await BeginAsync(restartProvider, later, history)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(committedKey, 0))).Value.ShouldBeNull();
        (await store.GetAsync<DomainSharedProjectionStoredPartition>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(committedKey, 0, 0))).Value.ShouldBeNull();
        (await RebuildAsync(restartProvider, Lifecycle(later, DomainSharedProjectionRebuildAction.Abort)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);

        DomainSharedProjectionRebuildIdentity aborted = Identity("partitioned-cleanup-abort");
        current = await BeginAsync(restartProvider, aborted, history);
        current = await AccumulateAsync(restartProvider, aborted, 0, history[0]);
        (await RebuildAsync(restartProvider, Lifecycle(aborted, DomainSharedProjectionRebuildAction.Abort)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        string abortedKey = DomainSharedProjectionRebuildSessionKey.Compute(aborted);
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(abortedKey, 0))).Value.ShouldBeNull();
        (await store.GetAsync<DomainSharedProjectionStoredPartition>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(abortedKey, 0, 0))).Value.ShouldBeNull();
        (await RebuildAsync(restartProvider, Lifecycle(aborted, DomainSharedProjectionRebuildAction.Abort)))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
    }

    [Fact]
    public async Task GlobalEpochReclaimsPriorTenantScratchAfterCrossTenantRestart()
    {
        var store = new InMemoryReadModelStore();
        var fault = new ScratchFaultBatchStore(store);
        var handler = new GlobalPartitionedHandler();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = Provider(store, fault, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity first = Identity("global-first");
        (string Owner, ProjectionEventDto Event)[] history = [("owner-a", Event([1, 2, 3]))];
        DomainSharedProjectionRebuildResponse current = await BeginAsync(provider, first, history);
        current = await AccumulateAsync(provider, first, 0, history[0]);
        current = await RebuildAsync(provider, Finalize(first, current));
        current = await RebuildAsync(provider, Lifecycle(first, DomainSharedProjectionRebuildAction.Stage));
        fault.FailNextScratch = true;
        (await RebuildAsync(provider, Lifecycle(first, DomainSharedProjectionRebuildAction.Commit)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        string firstKey = DomainSharedProjectionRebuildSessionKey.Compute(first);
        string manifest = DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(firstKey, 0);
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName, manifest))
            .Value.ShouldNotBeNull();

        var restarted = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider nextProvider = Provider(store, store, restarted, handler);
        DomainSharedProjectionRebuildIdentity second = first with { TenantId = "tenant-b" };
        (await restarted.GetStatusAsync(handler.CreateScope("tenant-b"))).OperationTenantId.ShouldBe("tenant-a");
        (await BeginAsync(nextProvider, second, [("owner-b", Event([4, 5, 6]))]))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName, manifest))
            .Value.ShouldBeNull();
        (await restarted.GetStatusAsync(handler.CreateScope("tenant-b"))).OperationTenantId.ShouldBe("tenant-b");
        (await RebuildAsync(nextProvider, Lifecycle(second, DomainSharedProjectionRebuildAction.Abort)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
    }

    [Fact]
    public async Task GlobalEpochDoesNotReuseAnEmptyCaptureFromAnotherTenant()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new GlobalPartitionedHandler();
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity first = Identity("global-empty-reused");
        (await BeginAsync(provider, first, [])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await RebuildAsync(provider, Lifecycle(first, DomainSharedProjectionRebuildAction.Abort)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        SharedProjectionEpochStatus firstStatus = await coordinator.GetStatusAsync(scope);
        firstStatus.OperationId.ShouldBe(first.OperationId);
        long firstEpoch = firstStatus.Epoch;

        DomainSharedProjectionRebuildIdentity second = first with { TenantId = "tenant-b" };
        (await BeginAsync(provider, second, [])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(scope);
        status.Epoch.ShouldBe(firstEpoch + 1);
        status.OperationTenantId.ShouldBe("tenant-b");
        (await BeginAsync(provider, second, [])).Status.ShouldBeOneOf(
            ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        (await RebuildAsync(provider, Lifecycle(second, DomainSharedProjectionRebuildAction.Abort)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
    }

    [Fact]
    public async Task SessionlessGlobalAbortRequiresOperationTenantAsWellAsId()
    {
        var store = new InMemoryReadModelStore();
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new GlobalPartitionedHandler();
        using ServiceProvider provider = Provider(store, store, coordinator, handler);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity owner = Identity("shared-sessionless-abort");
        _ = await coordinator.BeginAsync(scope, owner.OperationId, "sealed-inventory",
            new Dictionary<string, long>(StringComparer.Ordinal),
            operationTenantId: owner.TenantId);

        DomainSharedProjectionRebuildIdentity otherTenant = owner with { TenantId = "tenant-b" };
        (await RebuildAsync(provider, Lifecycle(otherTenant, DomainSharedProjectionRebuildAction.Abort)))
            .Status.ShouldBe(ProjectionDispatchStatus.Failed);
        SharedProjectionEpochStatus stillOwned = await coordinator.GetStatusAsync(scope);
        stillOwned.OperationTenantId.ShouldBe(owner.TenantId);
        stillOwned.IsBuilding.ShouldBeTrue();

        (await RebuildAsync(provider, Lifecycle(owner, DomainSharedProjectionRebuildAction.Abort)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await coordinator.GetStatusAsync(scope)).IsBuilding.ShouldBeFalse();
    }

    [Fact]
    public async Task AbortedSessionCloseoutKeepsEpochFencedUntilRetryCleansScratch()
    {
        var store = new InMemoryReadModelStore();
        var fault = new ReceiptFaultStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new PartitionedHandler();
        using ServiceProvider provider = Provider(fault, store, coordinator, handler);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("partitioned-abort-closeout");
        (string Owner, ProjectionEventDto Event)[] history = [("owner-a", Event([1, 2, 3]))];
        _ = await BeginAsync(provider, identity, history);
        (await AccumulateAsync(provider, identity, 0, history[0])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        fault.FailNextAbortCloseout = true;

        (await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort)))
            .Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        (await coordinator.GetStatusAsync(scope)).IsAborting.ShouldBeTrue();
        await Should.ThrowAsync<InvalidOperationException>(() => coordinator.BeginAsync(scope,
            "new-before-closeout", "inventory", new Dictionary<string, long> { ["owner-a"] = 1 }));

        (await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await coordinator.GetStatusAsync(scope)).IsAborting.ShouldBeFalse();
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(sessionKey, 0))).Value.ShouldBeNull();
        (await store.GetAsync<DomainSharedProjectionStoredPartition>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(sessionKey, 0, 0))).Value.ShouldBeNull();
        (await coordinator.BeginAsync(scope, "new-after-closeout", "inventory",
            new Dictionary<string, long> { ["owner-a"] = 1 })).ShouldBe(2);
    }

    [Fact]
    public async Task HighEntropyPartitionsPromoteAndPruneWithoutTenantCandidateGrowth()
    {
        var store = new InMemoryReadModelStore();
        var fault = new ScratchFaultBatchStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new PartitionedHandler();
        using ServiceProvider provider = Provider(store, fault, coordinator, handler);
        const int ownerCount = 10_000;
        (string Owner, ProjectionEventDto Event)[] histories = Enumerable.Range(0, ownerCount)
            .Select(index => ($"owner-{index:D4}", Event(RandomNumberGenerator.GetBytes(256))))
            .ToArray();

        DomainSharedProjectionRebuildIdentity first = Identity("partitioned-full");
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildResponse current = await BeginAsync(provider, first, histories);
        for (int index = 0; index < histories.Length; index++)
        {
            current = await AccumulateAsync(provider, first, index, histories[index]);
            current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        }

        DomainSharedProjectionRebuildSessionState session = (await store.GetAsync<DomainSharedProjectionRebuildSessionState>(
            StoreName, DomainSharedProjectionRebuildSessionKey.Compute(first))).Value.ShouldNotBeNull();
        session.CandidateState.Length.ShouldBe(0);
        session.Receipts.Length.ShouldBe(0);
        session.Partitioned.ShouldBeTrue();
        current = await RebuildAsync(provider, Finalize(first, current));
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        current = await RebuildAsync(provider, Lifecycle(first, DomainSharedProjectionRebuildAction.Stage));
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        fault.FailNextScratch = true;
        (await RebuildAsync(provider, Lifecycle(first, DomainSharedProjectionRebuildAction.Commit)))
            .Status.ShouldBe(ProjectionDispatchStatus.Completed);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-0000")).Value.ShouldNotBeNull();
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-9999")).Value.ShouldNotBeNull();
        ProjectionEventDto next = Event([4]) with
        {
            SequenceNumber = 2, GlobalPosition = 2, MessageId = "capacity-live-after-handoff"
        };
        ProjectionDispatchResponse live = await DomainProjectionDispatcher.DispatchAsync(provider,
            new ProjectionDispatchRequest(new ProjectionRequest("tenant-a", "widget", "owner-0000", [next]),
                [handler.ProjectionType], "capacity-live-during-cleanup", first.CatalogFingerprint),
            new ProjectionDispatchOptions(),
            new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
            CancellationToken.None);
        live.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        string firstSessionKey = DomainSharedProjectionRebuildSessionKey.Compute(first);
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(firstSessionKey, 0))).Value.ShouldNotBeNull();
        (await RebuildAsync(provider, Lifecycle(first, DomainSharedProjectionRebuildAction.Verify)))
            .Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        await coordinator.BeginAsync(scope, "capacity-next-epoch", "inventory",
            new Dictionary<string, long>());
        (await RebuildAsync(provider, Lifecycle(first, DomainSharedProjectionRebuildAction.Commit)))
            .Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(firstSessionKey, 0))).Value.ShouldBeNull();
        DomainSharedProjectionRebuildSessionState cleaned = (await store.GetAsync<DomainSharedProjectionRebuildSessionState>(
            StoreName, firstSessionKey)).Value.ShouldNotBeNull();
        cleaned.ScratchIndexesCleaned.ShouldBeTrue();
        cleaned.ScratchCleanedOwnerCount.ShouldBe(ownerCount);
        await coordinator.AbortAsync(scope, "capacity-next-epoch",
            static (_, _, _) => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]));

        // A new selected generation contains only its rebuilt inventory; old physical rows remain
        // available for recovery but cannot be reached through the selected-generation read seam.
        DomainSharedProjectionRebuildIdentity second = Identity("partitioned-prune");
        (string Owner, ProjectionEventDto Event)[] replacement = [histories[9999]];
        current = await BeginAsync(provider, second, replacement);
        current = await AccumulateAsync(provider, second, 0, replacement[0]);
        current = await FinishAsync(provider, second, current);
        current.Status.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-0000")).Value.ShouldBeNull();
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-9999")).Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task PartitionSavedBeforeReceiptSurvivesCrashAndFailedFinalizeLeavesActiveGeneration()
    {
        var store = new InMemoryReadModelStore();
        var fault = new ReceiptFaultStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new PartitionedHandler();
        using ServiceProvider provider = Provider(fault, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        (string Owner, ProjectionEventDto Event)[] firstHistory = [("owner-a", Event([1, 2, 3]))];
        DomainSharedProjectionRebuildIdentity first = Identity("partitioned-retry");
        _ = await BeginAsync(provider, first, firstHistory);
        fault.FailNextReceipt = true;

        DomainSharedProjectionRebuildResponse uncertain = await AccumulateAsync(provider, first, 0, firstHistory[0]);
        uncertain.Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        DomainSharedProjectionRebuildSessionState incomplete = (await store.GetAsync<DomainSharedProjectionRebuildSessionState>(
            StoreName, DomainSharedProjectionRebuildSessionKey.Compute(first))).Value.ShouldNotBeNull();
        incomplete.AcceptedAggregateCount.ShouldBe(0);

        DomainSharedProjectionRebuildResponse accepted = await AccumulateAsync(provider, first, 0, firstHistory[0]);
        accepted.Status.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        accepted = await FinishAsync(provider, first, accepted);
        accepted.Status.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        SharedProjectionScope scope = handler.CreateScope("tenant-a");
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value.ShouldNotBeNull();

        DomainSharedProjectionRebuildIdentity failed = Identity("partitioned-failed");
        (string Owner, ProjectionEventDto Event)[] secondHistory = [("owner-b", Event([4, 5, 6]))];
        _ = await BeginAsync(provider, failed, secondHistory);
        DomainSharedProjectionRebuildResponse current = await AccumulateAsync(provider, failed, 0, secondHistory[0]);
        handler.FailFinalize = true;
        DomainSharedProjectionRebuildResponse result = await RebuildAsync(provider, Finalize(failed, current));
        result.Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value.ShouldNotBeNull();
        (await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-b")).Value.ShouldBeNull();
    }

    [Fact]
    public async Task AbortedUnacceptedOwnerReclaimsReservedScratchAfterReceiptFailure()
    {
        var store = new InMemoryReadModelStore();
        var fault = new ReceiptFaultStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new PartitionedHandler();
        using ServiceProvider provider = Provider(fault, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("partitioned-pending-abort");
        (string Owner, ProjectionEventDto Event)[] history = [("owner-a", Event(RandomNumberGenerator.GetBytes(600_000)))];
        _ = await BeginAsync(provider, identity, history);
        fault.FailNextReceipt = true;
        (await AccumulateAsync(provider, identity, 0, history[0])).Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);

        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        DomainSharedProjectionRebuildSessionState pending = (await store.GetAsync<DomainSharedProjectionRebuildSessionState>(
            StoreName, sessionKey)).Value.ShouldNotBeNull();
        pending.PendingScratchChunkCount.ShouldBeGreaterThan(0);
        (await store.GetAsync<DomainSharedProjectionStoredPartition>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(sessionKey, 0, 0))).Value.ShouldNotBeNull();

        (await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort)))
            .Status.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        for (int ordinal = 0; ordinal < pending.PendingScratchChunkCount; ordinal++)
        {
            (await store.GetAsync<DomainSharedProjectionStoredPartition>(StoreName,
                DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(sessionKey, 0, ordinal))).Value.ShouldBeNull();
        }

        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(sessionKey, 0))).Value.ShouldBeNull();
    }

    [Fact]
    public async Task PausedOwnerScratchWriteCannotLandAfterAbortCleanup()
    {
        var store = new InMemoryReadModelStore();
        var pausingBatches = new PausingOwnerScratchBatchStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, pausingBatches);
        var handler = new PartitionedHandler();
        using ServiceProvider provider = Provider(store, pausingBatches, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        DomainSharedProjectionRebuildIdentity identity = Identity("partitioned-paused-scratch");
        (string Owner, ProjectionEventDto Event)[] history = [("owner-a", Event([1, 2, 3]))];
        _ = await BeginAsync(provider, identity, history);
        Task<DomainSharedProjectionRebuildResponse> accumulate = AccumulateAsync(provider, identity, 0, history[0]);
        await pausingBatches.WritePaused.Task.WaitAsync(TimeSpan.FromSeconds(10));
        (await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort)))
            .Status.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        pausingBatches.ReleaseWrite.TrySetResult();
        (await accumulate).Status.ShouldNotBe(ProjectionDispatchStatus.Completed);

        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        (await store.GetAsync<DomainSharedProjectionStoredPartition>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(sessionKey, 0, 0))).Value.ShouldBeNull();
        (await store.GetAsync<DomainSharedProjectionStoredPartitionManifest>(StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(sessionKey, 0))).Value.ShouldBeNull();
    }

    [Fact]
    public async Task OneOwnerLargerThanOneMiBStagesDurableChunksAndPromotes()
    {
        var store = new InMemoryReadModelStore();
        var fault = new ReceiptFaultStore(store);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        var handler = new PartitionedHandler();
        using ServiceProvider provider = Provider(fault, store, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(handler.CreateScope("tenant-a"), handler.OrdinaryWriterId);
        byte[] payload = RandomNumberGenerator.GetBytes(1_200_000);
        (string Owner, ProjectionEventDto Event)[] history = [("owner-large", Event(payload))];
        DomainSharedProjectionRebuildIdentity identity = Identity("partitioned-large-owner");
        _ = await BeginAsync(provider, identity, history);
        fault.FailNextReceipt = true;
        (await AccumulateAsync(provider, identity, 0, history[0])).Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        DomainSharedProjectionRebuildResponse accepted = await AccumulateAsync(provider, identity, 0, history[0]);
        accepted.Status.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        (await FinishAsync(provider, identity, accepted)).Status
            .ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        PayloadRow row = (await coordinator.ReadAsync<PayloadRow>(handler.CreateScope("tenant-a"), "row:owner-large"))
            .Value.ShouldNotBeNull();
        row.Value.ShouldBe(Convert.ToHexString(SHA256.HashData(payload)));
    }

    private static async Task<DomainSharedProjectionRebuildResponse> BeginAsync(
        IServiceProvider provider,
        DomainSharedProjectionRebuildIdentity identity,
        IReadOnlyList<(string Owner, ProjectionEventDto Event)> histories)
    {
        string fingerprint = DomainSharedProjectionRebuildFingerprint.EmptyInventory;
        for (int index = 0; index < histories.Count; index++)
        {
            fingerprint = DomainSharedProjectionRebuildFingerprint.AppendInventory(
                fingerprint, index,
                DomainSharedProjectionRebuildFingerprint.ComputeHistory(histories[index].Owner, false, [histories[index].Event]));
        }

        var highWatermarks = histories.ToDictionary(static item => item.Owner, static _ => 1L, StringComparer.Ordinal);
        return await RebuildAsync(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Begin,
            identity,
            CaptureInventoryFingerprint: fingerprint,
            SourceHighWatermarks: highWatermarks));
    }

    private static Task<DomainSharedProjectionRebuildResponse> AccumulateAsync(
        IServiceProvider provider,
        DomainSharedProjectionRebuildIdentity identity,
        long ordinal,
        (string Owner, ProjectionEventDto Event) history)
        => RebuildAsync(provider, new DomainSharedProjectionRebuildRequest(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Accumulate,
            identity,
            AggregateOrdinal: ordinal,
            AggregateId: history.Owner,
            Events: [history.Event]));

    private static async Task<DomainSharedProjectionRebuildResponse> FinishAsync(
        IServiceProvider provider,
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildResponse current)
    {
        current = await RebuildAsync(provider, Finalize(identity, current));
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        current = await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Stage));
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        current = await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Commit));
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed);
        return await RebuildAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Verify));
    }

    private static DomainSharedProjectionRebuildRequest Finalize(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildResponse current)
        => new(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Finalize,
            identity,
            ExpectedAggregateCount: current.AcceptedAggregateCount,
            ExpectedInventoryFingerprint: current.InventoryFingerprint);

    private static DomainSharedProjectionRebuildRequest Lifecycle(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildAction action)
        => new(DomainSharedProjectionRebuildProtocol.Version, action, identity);

    private static Task<DomainSharedProjectionRebuildResponse> RebuildAsync(
        IServiceProvider provider,
        DomainSharedProjectionRebuildRequest request)
        => DomainSharedProjectionRebuildDispatcher.DispatchAsync(
            provider, request, new ProjectionDispatchOptions(),
            new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
            CancellationToken.None);

    private static DomainSharedProjectionRebuildIdentity Identity(string operation)
        => new(
            "tenant-a", "widget", "partitioned-widget", operation,
            ProjectionRouteCatalogFingerprint.Compute("widget-service", "v1",
                [new ProjectionDispatchRoute("widget", "partitioned-widget")]));

    private static string CheckpointKey(SharedProjectionScope scope, string owner)
        => "shared-checkpoint:" + scope.ComputeHash() + ":"
            + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(owner)));

    private static ProjectionEventDto Event(byte[] payload)
        => new("widget-changed", payload, "json", 1, DateTimeOffset.UnixEpoch, "correlation-1", "message-1", "user-1", 1);

    private static ServiceProvider Provider(
        IReadModelStore sessionStore,
        IReadModelBatchStore batchStore,
        SharedProjectionEpochCoordinator coordinator,
        PartitionedHandler handler,
        EventPayloadEvolutionRegistry? evolution = null)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(sessionStore);
        _ = services.AddSingleton(batchStore);
        _ = services.AddSingleton(coordinator);
        _ = services.AddScoped<IAsyncDomainProjectionHandler>(_ => handler);
        if (evolution is not null) _ = services.AddSingleton(evolution);
        return services.BuildServiceProvider();
    }

    private sealed record PayloadRow(string Value);

    private sealed class StreamedPartitionedHandler : PartitionedHandler,
        IAsyncDomainSharedProjectionStreamedOwnerHandler
    {
    }

    private sealed class GlobalPartitionedHandler : PartitionedHandler
    {
        public override bool GlobalEpochScope => true;

        public override SharedProjectionScope CreateScope(string tenantId)
            => new(StoreName, "global-index", Domain, ProjectionType, [OrdinaryWriterId]);
    }

    private class PartitionedHandler : IAsyncDomainSharedProjectionPartitionedRebuildHandler
    {
        public bool EpochEnabled => true;
        public virtual bool GlobalEpochScope => false;
        public string OrdinaryWriterId => "ordinary-dispatch";
        public string Domain => "widget";
        public string ProjectionType => "partitioned-widget";
        public string RebuildStoreName => StoreName;
        public bool FailFinalize { get; set; }
        public bool FailBufferedOwnerFold { get; set; }
        public bool EmitOneChunkPerEvent { get; set; }
        public int StreamedEventCount { get; private set; }

        public virtual SharedProjectionScope CreateScope(string tenantId)
            => new(StoreName, tenantId, Domain, ProjectionType, [OrdinaryWriterId]);

        public Task<ReadOnlyMemory<byte>> PreparePartitionAsync(
            DomainSharedProjectionRebuildIdentity identity,
            ProjectionRequest aggregateHistory,
            CancellationToken cancellationToken)
            => Task.FromResult((ReadOnlyMemory<byte>)aggregateHistory.Events.Last().Payload.ToArray());

        public Task<IReadOnlyList<ReadOnlyMemory<byte>>> PreparePartitionChunksAsync(
            DomainSharedProjectionRebuildIdentity identity,
            ProjectionRequest aggregateHistory,
            CancellationToken cancellationToken)
        {
            if (FailBufferedOwnerFold) throw new InvalidOperationException("Buffered owner fold must not run.");
            byte[] payload = aggregateHistory.Events.Last().Payload;
            return Task.FromResult<IReadOnlyList<ReadOnlyMemory<byte>>>(Enumerable.Range(0,
                (payload.Length + 255_999) / 256_000)
                .Select(index => (ReadOnlyMemory<byte>)payload.AsMemory(index * 256_000,
                    Math.Min(256_000, payload.Length - index * 256_000)))
                .ToArray());
        }

        public async IAsyncEnumerable<ReadOnlyMemory<byte>> StreamPartitionChunksAsync(
            DomainSharedProjectionRebuildIdentity identity,
            string aggregateId,
            IAsyncEnumerable<ProjectionEventDto> events,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            byte[]? lastPayload = null;
            await foreach (ProjectionEventDto item in events.WithCancellation(cancellationToken))
            {
                lastPayload = item.Payload;
                StreamedEventCount++;
                if (EmitOneChunkPerEvent) yield return item.Payload;
            }
            if (EmitOneChunkPerEvent) yield break;
            if (lastPayload is null) throw new InvalidOperationException("The owner history is empty.");
            for (int offset = 0; offset < lastPayload.Length; offset += 256_000)
                yield return lastPayload.AsMemory(offset, Math.Min(256_000, lastPayload.Length - offset));
        }

        public async IAsyncEnumerable<ReadModelBatchOperation> StreamFinalizedOperationsAsync(
            DomainSharedProjectionRebuildIdentity identity,
            IDomainSharedProjectionPartitionCursor partitions,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (FailFinalize) throw new InvalidOperationException("Injected fold failure.");
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            string? owner = null;
            await foreach (DomainSharedProjectionRebuildPartition chunk in partitions.ReadAllAsync(cancellationToken))
            {
                if (owner is not null && owner != chunk.AggregateId)
                {
                    yield return ReadModelBatchOperation.Write("row:" + owner,
                        new PayloadRow(Convert.ToHexString(hash.GetHashAndReset())),
                        ReadModelBatchConcurrency.LastWrite);
                }
                owner = chunk.AggregateId;
                hash.AppendData(chunk.State.Span);
            }
            if (owner is not null)
                yield return ReadModelBatchOperation.Write("row:" + owner,
                    new PayloadRow(Convert.ToHexString(hash.GetHashAndReset())),
                    ReadModelBatchConcurrency.LastWrite);
        }

        public Task<DomainProjectionRebuildPlan> FinalizePartitionsAsync(
            DomainSharedProjectionRebuildIdentity identity,
            IReadOnlyList<DomainSharedProjectionRebuildPartition> partitions,
            CancellationToken cancellationToken)
        {
            if (FailFinalize) throw new InvalidOperationException("Injected fold failure.");
            ReadModelBatchOperation[] operations = partitions
                .GroupBy(static partition => partition.AggregateId, StringComparer.Ordinal)
                .Select(group => ReadModelBatchOperation.Write(
                    "row:" + group.Key,
                    new PayloadRow(Convert.ToHexString(SHA256.HashData(
                        group.OrderBy(static partition => partition.ChunkOrdinal)
                            .SelectMany(static partition => partition.State.ToArray()).ToArray()))),
                    ReadModelBatchConcurrency.LastWrite))
                .ToArray();
            return Task.FromResult(new DomainProjectionRebuildPlan(StoreName, operations));
        }

        public Task<IReadOnlyList<ReadModelBatchOperation>> FoldCatchUpAsync(
            SharedProjectionDelivery delivery,
            long generation,
            SharedProjectionEpochCoordinator coordinator,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ReadModelBatchOperation>>([]);

        public Task<DomainProjectionHandlerResult> ProjectAsync(
            ProjectionRequest request,
            string dispatchId,
            CancellationToken cancellationToken)
            => Task.FromResult(DomainProjectionHandlerResult.AlreadyCompleted());

        public Task<DomainSharedProjectionRebuildCandidate> CreateEmptyCandidateAsync(
            DomainSharedProjectionRebuildIdentity identity,
            CancellationToken cancellationToken)
            => Task.FromResult(new DomainSharedProjectionRebuildCandidate([]));

        public Task<DomainSharedProjectionRebuildCandidate> AccumulateAsync(
            DomainSharedProjectionRebuildIdentity identity,
            DomainSharedProjectionRebuildCandidate candidate,
            ProjectionRequest aggregateHistory,
            CancellationToken cancellationToken)
            => throw new NotSupportedException("Partitioned accumulation must be dispatched by the SDK.");

        public Task<DomainProjectionRebuildPlan> FinalizeAsync(
            DomainSharedProjectionRebuildIdentity identity,
            DomainSharedProjectionRebuildCandidate candidate,
            CancellationToken cancellationToken)
            => throw new NotSupportedException("Partitioned finalization must be dispatched by the SDK.");
    }

    private sealed class ReceiptFaultStore(InMemoryReadModelStore inner) : IReadModelStore
    {
        public bool FailNextReceipt { get; set; }

        public bool FailNextAbortCloseout { get; set; }

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key, CancellationToken cancellationToken = default)
            where TValue : class
            => inner.GetAsync<TValue>(storeName, key, cancellationToken);

        public Task SaveAsync<TValue>(string storeName, string key, TValue value, CancellationToken cancellationToken = default)
            where TValue : class
            => inner.SaveAsync(storeName, key, value, cancellationToken);

        public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default)
            where TValue : class
        {
            if (FailNextAbortCloseout && value is DomainSharedProjectionRebuildSessionState
                { Phase: DomainSharedProjectionRebuildPhase.Aborted })
            {
                FailNextAbortCloseout = false;
                throw new IOException("Injected abort closeout outage.");
            }

            if (FailNextReceipt && value is DomainSharedProjectionRebuildSessionState { AcceptedAggregateCount: 1 })
            {
                FailNextReceipt = false;
                throw new HttpRequestException("Injected receipt outage.");
            }

            return inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
        }
    }

    private sealed class PausingCheckpointStore(InMemoryReadModelStore inner) : IReadModelStore
    {
        public bool PauseNextCheckpointRead { get; set; }

        public TaskCompletionSource ReadPaused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key,
            CancellationToken cancellationToken = default) where TValue : class
        {
            if (PauseNextCheckpointRead && key.StartsWith("shared-checkpoint:", StringComparison.Ordinal))
            {
                PauseNextCheckpointRead = false;
                ReadPaused.TrySetResult();
                await ReleaseRead.Task.WaitAsync(cancellationToken);
            }

            return await inner.GetAsync<TValue>(storeName, key, cancellationToken);
        }

        public Task SaveAsync<TValue>(string storeName, string key, TValue value,
            CancellationToken cancellationToken = default) where TValue : class
            => inner.SaveAsync(storeName, key, value, cancellationToken);

        public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag,
            CancellationToken cancellationToken = default) where TValue : class
            => inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
    }

    private sealed class ScratchFaultBatchStore(InMemoryReadModelStore inner) : IReadModelBatchStore
    {
        public bool FailNextScratch { get; set; }

        public Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
        {
            if (FailNextScratch && batch.Scope.BatchId.StartsWith("scratch-cleanup:", StringComparison.Ordinal))
            {
                FailNextScratch = false;
                throw new IOException("Injected scratch cleanup outage.");
            }

            return inner.ExecuteAsync(batch, cancellationToken);
        }
    }

    private sealed class FailOnceOwnerScratchBatchStore(InMemoryReadModelStore inner) : IReadModelBatchStore
    {
        public bool FailNextOwnerScratch { get; set; }
        public int FailureCount { get; private set; }

        public Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch,
            CancellationToken cancellationToken = default)
        {
            if (FailNextOwnerScratch && batch.Scope.BatchId.StartsWith("owner-scratch:",
                StringComparison.Ordinal))
            {
                FailNextOwnerScratch = false;
                FailureCount++;
                throw new IOException("Injected owner chunk write failure.");
            }

            return inner.ExecuteAsync(batch, cancellationToken);
        }
    }

    private sealed class PausingOwnerScratchBatchStore(InMemoryReadModelStore inner) : IReadModelBatchStore
    {
        public TaskCompletionSource WritePaused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch,
            CancellationToken cancellationToken = default)
        {
            if (batch.Scope.BatchId.StartsWith("owner-scratch:", StringComparison.Ordinal))
            {
                WritePaused.TrySetResult();
                await ReleaseWrite.Task.WaitAsync(cancellationToken);
            }

            return await inner.ExecuteAsync(batch, cancellationToken);
        }
    }

    private sealed class PausingTransportScratchBatchStore(InMemoryReadModelStore inner) : IReadModelBatchStore
    {
        public TaskCompletionSource WritePaused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ReadModelBatchResult> ExecuteAsync(ReadModelBatch batch,
            CancellationToken cancellationToken = default)
        {
            if (batch.Scope.BatchId.StartsWith("transport-scratch:", StringComparison.Ordinal)
                && batch.Scope.BatchId.Contains(":fragment:", StringComparison.Ordinal))
            {
                WritePaused.TrySetResult();
                await ReleaseWrite.Task.WaitAsync(cancellationToken);
            }

            return await inner.ExecuteAsync(batch, cancellationToken);
        }
    }
}
