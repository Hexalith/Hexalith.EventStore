using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

public sealed partial class ProjectionExpiryAndScratchRecoveryLiveTests
{
    [Fact]
    public async Task CompetingRetainedNamedOwnerRetriesOverRedisThenAbortsAndAllowsOrdinaryGrowthAndFreshNamedRebuild()
    {
        string tenant = "review5-competing-" + Guid.NewGuid().ToString("N");
        using DaprClient client = Client();
        var store = new DaprReadModelStore(client);
        var handler = new LivePartitionedScratchHandler();
        var scope = handler.CreateScope(tenant);
        var identity = NamedIdentity(tenant, "competing-retained-owners");
        string scratch = DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(DomainSharedProjectionRebuildSessionKey.Compute(identity), 0, 0);
        int conflicts = 0;
        var originals = new List<(ReadModelBatchScope Scope, byte[] Envelope)>();
        var interleaved = new ReviewFiveLiveBatchStore(client, store, async (key, bytes, token) =>
        {
            if (key == scratch && ReadModelBatchEnvelope.IsEnvelope(bytes.Span))
            {
                ReadModelBatchEnvelope original = ReadModelBatchEnvelope.FromBytes(bytes)!;
                ReadModelBatchMarker marker = (await store.GetAsync<ReadModelBatchMarker>(scope.StoreName,
                    ReadModelBatchKeys.MarkerKey(original.ScopeHash), token)).Value!;
                originals.Add((new(scope.StoreName, tenant, scope.Domain, scope.Family, scope.Family, marker.BatchId), bytes.ToArray()));
            }
            if (key != scope.StateKey || !ReadModelBatchEnvelope.IsEnvelope(bytes.Span) || conflicts >= 2) return;
            int competitor = conflicts++;
            (await new SharedProjectionEpochCoordinator(store, store).TryWriteScratchAsync(scope, identity.OperationId,
                tenant + ":competing-private:" + competitor, Value(2), "competing-batch-" + competitor,
                ReadModelBatchConcurrency.CreateOnly, token)).ShouldBeTrue();
        });
        var coordinator = new SharedProjectionEpochCoordinator(store, interleaved);
        using var originalProvider = NamedProvider(store, interleaved, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        var first = NamedEvent(1);
        var second = NamedEvent(2) with { GlobalPosition = 2, MessageId = "owner-b" };
        (await NamedBegin(originalProvider, identity, [("owner-a", first), ("owner-b", second)])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        DomainSharedProjectionRebuildResponse accepted = await NamedAccumulate(originalProvider, identity, "owner-a", first);
        for (int retry = 0; accepted.Status is not (ProjectionDispatchStatus.Completed or ProjectionDispatchStatus.AlreadyCompleted) && retry < 5; retry++)
            accepted = await NamedAccumulate(originalProvider, identity, "owner-a", first);
        accepted.Status.ShouldBe(ProjectionDispatchStatus.Completed); conflicts.ShouldBe(2);
        (await NamedDispatch(originalProvider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Accumulate,
            identity, AggregateOrdinal: 1, AggregateId: "owner-b", Events: [second]))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var abort = new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Abort, identity);
        (await NamedDispatch(originalProvider, abort)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        byte[] opened = (byte[])(await _redis.GetDatabase().HashGetAsync(Key(scope.StateKey), "data"))!;
        using DaprClient replacement = Client();
        store = new DaprReadModelStore(replacement); coordinator = new SharedProjectionEpochCoordinator(store, store);
        using var resumed = NamedProvider(store, store, coordinator, handler);
        var raw = new DaprReadModelBatchStateAccessor(replacement, scope.StoreName);
        foreach (var original in originals.Take(2))
        {
            (await raw.TryWriteAsync(scratch, original.Envelope, string.Empty, default)).ShouldBeTrue();
            (await NamedDispatch(resumed, abort)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
            (await _redis.GetDatabase().KeyExistsAsync(Key(scratch))).ShouldBeFalse();
            ((byte[])(await _redis.GetDatabase().HashGetAsync(Key(scope.StateKey), "data"))!).ShouldBe(opened);
        }
        var growth = NamedEvent(9) with { GlobalPosition = 3, MessageId = "growth-after-competing-owners" };
        var delivered = await DomainProjectionDispatcher.DispatchAsync(resumed,
            new ProjectionDispatchRequest(new ProjectionRequest(tenant, "widget", "owner-a", [growth]), [handler.ProjectionType], growth.MessageId, identity.CatalogFingerprint),
            new ProjectionDispatchOptions(), new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" }, default);
        delivered.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var fresh = NamedIdentity(tenant, "fresh-after-competing-owners");
        (await NamedBegin(resumed, fresh, [("owner-a", growth)])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var current = await NamedAccumulate(resumed, fresh, "owner-a", growth);
        (await NamedFinish(resumed, fresh, current)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await coordinator.ReadAsync<JsonObject>(scope, "row:owner-a")).Value!["count"]!.GetValue<int>().ShouldBe(9);
        TestContext.Current.TestOutputHelper!.WriteLine("review5-competing-owners;conflicts=2;retained-attempts-distinct=true;late-original-attempts-reconciled-by-named-Abort=true;physical-private-absent=true;exact-Open-preserved=true;ordinary-growth=true;fresh-named-rebuild=true");
    }

    [Fact]
    public async Task TerminalAcceptedStreamedTransportAndHashPageLateInstallersArePhysicallyRemovedByReplacementNamedAbort()
    {
        string tenant = "review5-streamed-" + Guid.NewGuid().ToString("N");
        using DaprClient client = Client();
        var store = new DaprReadModelStore(client);
        var handler = new LiveStreamedPartitionedScratchHandler();
        var scope = handler.CreateScope(tenant);
        var identity = NamedIdentity(tenant, "late-streamed-owner");
        string session = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        string fragment = session + ":transport:0:0:fragment:0", part = session + ":transport:0:0";
        string hashPage = DomainSharedProjectionRebuildDispatcher.PartitionHashPageKey(session, 0, 0);
        var captured = new Dictionary<string, byte[]>();
        var capturing = new ReviewFiveLiveBatchStore(client, store, (key, bytes, _) =>
        {
            if ((key == fragment || key == part || key == hashPage) && ReadModelBatchEnvelope.IsEnvelope(bytes.Span)) captured.TryAdd(key, bytes.ToArray());
            return Task.CompletedTask;
        });
        var coordinator = new SharedProjectionEpochCoordinator(store, capturing);
        using var provider = NamedProvider(store, capturing, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        var source = NamedEvent(1) with { Payload = RandomNumberGenerator.GetBytes(1_048_576) };
        (await NamedBegin(provider, identity, [("owner-a", source)])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await NamedDispatch(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulatePart,
            identity, AggregateOrdinal: 0, AggregateId: "owner-a", Events: [source], PartOrdinal: 0))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await NamedDispatch(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulateComplete,
            identity, AggregateOrdinal: 0, AggregateId: "owner-a", PartCount: 1,
            ExpectedHistoryFingerprint: DomainSharedProjectionRebuildFingerprint.ComputeHistory("owner-a", false, [source])))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        captured.Count.ShouldBe(3);
        var abort = new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Abort, identity);
        (await NamedDispatch(provider, abort)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var terminal = (await store.GetAsync<DomainSharedProjectionRebuildSessionState>(scope.StoreName, session)).Value!;
        terminal.ScratchCleanedOwnerCount.ShouldBe(1); terminal.ScratchIndexesCleaned.ShouldBeTrue();
        var retired = (await store.GetAsync<DomainSharedProjectionScratchRetirement>(scope.StoreName,
            DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(session, 0) + ":retired")).Value!;
        retired.TransportPartCount.ShouldBe(1); retired.ChunkCount.ShouldBe(5);
        byte[] open = (byte[])(await _redis.GetDatabase().HashGetAsync(Key(scope.StateKey), "data"))!;
        var raw = new DaprReadModelBatchStateAccessor(client, scope.StoreName);
        foreach (var row in captured) (await raw.TryWriteAsync(row.Key, row.Value, string.Empty, default)).ShouldBeTrue();
        using DaprClient replacement = Client();
        store = new DaprReadModelStore(replacement); coordinator = new SharedProjectionEpochCoordinator(store, store);
        using var resumed = NamedProvider(store, store, coordinator, handler);
        (await NamedDispatch(resumed, abort)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        foreach (string key in captured.Keys) (await _redis.GetDatabase().KeyExistsAsync(Key(key))).ShouldBeFalse();
        ((byte[])(await _redis.GetDatabase().HashGetAsync(Key(scope.StateKey), "data"))!).ShouldBe(open);
        (await NamedDispatch(resumed, abort)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        var growth = NamedEvent(9) with { GlobalPosition = 2, MessageId = "after-late-transport" };
        var delivered = await DomainProjectionDispatcher.DispatchAsync(resumed,
            new ProjectionDispatchRequest(new ProjectionRequest(tenant, "widget", "owner-a", [growth]), [handler.ProjectionType], growth.MessageId, identity.CatalogFingerprint),
            new ProjectionDispatchOptions(), new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" }, default);
        delivered.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var fresh = NamedIdentity(tenant, "fresh-after-streamed-retirement");
        (await NamedBegin(resumed, fresh, [("owner-a", growth)])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var current = await NamedAccumulate(resumed, fresh, "owner-a", growth);
        (await NamedFinish(resumed, fresh, current)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        TestContext.Current.TestOutputHelper!.WriteLine("review5-terminal-streamed;accepted-owner=true;terminal-flags=true;transport-parts=1;chunks=5;original-fragment-part-hash-page-physically-absent=true;exact-Open-preserved=true;ordinary-growth=true;fresh-named-rebuild=true;no-test-authored-recovery-or-private-delete=true");
    }

    [Fact]
    public async Task MaximumEscapedLegacyScopeRecoversOverRedisAndRetainsCanonicalBoundedFence()
    {
        using DaprClient client = Client();
        var store = new DaprReadModelStore(client);
        string escaped = "x" + new string('\u0001', 511);
        // Component names are configured provider resources. All five identity components and the
        // original logical key use the full lawful escaping bound under the actual statestore.
        var scope = new SharedProjectionScope("statestore", escaped, escaped, escaped, ["writer"]);
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        _ = await coordinator.RegisterWriterAsync(scope, "writer");
        _ = await coordinator.BeginAsync(scope, "operation", "inventory", new Dictionary<string, long>());
        var state = await store.GetAsync<SharedProjectionEpochState>(scope.StoreName, scope.StateKey);
        var batch = new ReadModelBatch(new(scope.StoreName, escaped, escaped, escaped, escaped, escaped),
            [ReadModelBatchOperation.Write(escaped, Value(1), ReadModelBatchConcurrency.CreateOnly),
                ReadModelBatchOperation.Write(scope.StateKey, state.Value!, ReadModelBatchConcurrency.Match(state.ETag!))]);
        var raw = new DaprReadModelBatchStateAccessor(client, scope.StoreName);
        await Should.ThrowAsync<InvalidOperationException>(() => new ReadModelBatchProtocol(raw, new(), NullLogger.Instance)
            .ExecuteAsync(batch, new RetainedBatchLiveFaultInjector(ReadModelBatchPhase.BeforeCommit, -1), default));
        (await store.RecoverPendingAsync(batch.Scope, escaped)).IsResolved.ShouldBeTrue();
        using DaprClient replacement = Client();
        (await new DaprReadModelStore(replacement).RecoverPendingAsync(batch.Scope, batch.Scope.ComputeMarkerKey())).IsResolved.ShouldBeTrue();
        RawStateEntry fence = await raw.ReadAsync(ReadModelBatchTerminalFence.Key(batch.Scope.ComputeScopeHash(), ReadModelBatchFingerprint.Compute(batch)), default);
        fence.Value.Length.ShouldBeGreaterThan(40_000); fence.Value.Length.ShouldBeLessThanOrEqualTo(ReadModelBatchTerminalFence.MaxLegacyEncodedBytes);
        (await _redis.GetDatabase().KeyExistsAsync(Key(escaped))).ShouldBeFalse();
        TestContext.Current.TestOutputHelper!.WriteLine($"review5-legacy-escape;physical-fence-bytes={fence.Value.Length};bound={ReadModelBatchTerminalFence.MaxLegacyEncodedBytes};replacement-repeated-recovery=true;private-physically-absent=true");
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingHistoricalOrCorruptRetiredOwnerInventoryRefusesNamedSuccessOverRedis(bool corrupt)
    {
        string tenant = "review5-missing-inventory-" + Guid.NewGuid().ToString("N");
        using DaprClient client = Client();
        var store = new DaprReadModelStore(client);
        var handler = new LivePartitionedScratchHandler();
        var scope = handler.CreateScope(tenant);
        var identity = NamedIdentity(tenant, "unavailable-original-inventory");
        string session = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        string scratch = DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(session, 0, 0);
        var capturing = new CapturingScratchLiveBatchStore(client, store, scratch);
        var coordinator = new SharedProjectionEpochCoordinator(store, capturing);
        using var provider = NamedProvider(store, capturing, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        var source = NamedEvent(1);
        (await NamedBegin(provider, identity, [("owner-a", source)])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        (await NamedAccumulate(provider, identity, "owner-a", source)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var abort = new DomainSharedProjectionRebuildRequest(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Abort, identity);
        (await NamedDispatch(provider, abort)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var raw = new DaprReadModelBatchStateAccessor(client, scope.StoreName);
        string retiredKey = DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(session, 0) + ":retired";
        RawStateEntry inventory = await raw.ReadAsync(retiredKey, default);
        if (corrupt)
        {
            var invalid = JsonSerializer.Deserialize<DomainSharedProjectionScratchRetirement>(inventory.Value.Span, JsonSerializerOptions.Web)! with { ChunkCount = -1 };
            (await raw.TryWriteAsync(retiredKey, ReadModelBatchCanonicalJson.Serialize(invalid), inventory.ETag, default)).ShouldBeTrue();
        }
        else (await raw.TryDeleteAsync(retiredKey, inventory.ETag, default)).ShouldBeTrue();
        (await raw.TryWriteAsync(scratch, capturing.OriginalEnvelope, string.Empty, default)).ShouldBeTrue();
        byte[] open = (byte[])(await _redis.GetDatabase().HashGetAsync(Key(scope.StateKey), "data"))!;
        byte[] terminal = (byte[])(await _redis.GetDatabase().HashGetAsync(Key(session), "data"))!;
        using DaprClient replacement = Client();
        store = new DaprReadModelStore(replacement);
        using var resumed = NamedProvider(store, store, new SharedProjectionEpochCoordinator(store, store), handler);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            (await NamedDispatch(resumed, abort)).Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
            ((byte[])(await _redis.GetDatabase().HashGetAsync(Key(scratch), "data"))!).ShouldBe(capturing.OriginalEnvelope.ToArray());
            ((byte[])(await _redis.GetDatabase().HashGetAsync(Key(scope.StateKey), "data"))!).ShouldBe(open);
            ((byte[])(await _redis.GetDatabase().HashGetAsync(Key(session), "data"))!).ShouldBe(terminal);
        }
        RawStateEntry invalidInventory = await raw.ReadAsync(retiredKey, default);
        (await raw.TryWriteAsync(retiredKey, inventory.Value, invalidInventory.ETag, default)).ShouldBeTrue();
        (await NamedDispatch(resumed, abort)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await _redis.GetDatabase().KeyExistsAsync(Key(scratch))).ShouldBeFalse();
        ((byte[])(await _redis.GetDatabase().HashGetAsync(Key(scope.StateKey), "data"))!).ShouldBe(open);
        TestContext.Current.TestOutputHelper!.WriteLine($"review5-inventory;corrupt={corrupt};repeated-refusal=true;late-private-unchanged=true;exact-Open-and-session-preserved=true;original-inventory-restored=true;subsequent-production-Abort-physically-cleaned=true");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MaximumEscapedTerminalAnchorRecoversCompletedAndInterruptedReceiptOverRedis(bool interrupted)
    {
        using DaprClient client = Client();
        var store = new DaprReadModelStore(client);
        string escaped = new('<', ReadModelBatchScope.MaxComponentByteLength);
        var scope = new ReadModelBatchScope("statestore", escaped, escaped, escaped, escaped, escaped);
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(5);
        var batch = new ReadModelBatch(scope,
            [ReadModelBatchOperation.WriteExpiring(escaped, Value(1), ReadModelBatchConcurrency.CreateOnly, deadline)])
            { RetainManifestForRecovery = true };
        var raw = new DaprReadModelBatchStateAccessor(client, scope.StoreName);
        var protocol = new ReadModelBatchProtocol(raw, new(), NullLogger.Instance);
        if (interrupted)
            await Should.ThrowAsync<InvalidOperationException>(() => protocol.ExecuteAsync(batch,
                new RetainedBatchLiveFaultInjector(ReadModelBatchPhase.RecoveryReceiptWritten, 0), default));
        else (await protocol.ExecuteAsync(batch, null, default)).IsSuccess.ShouldBeTrue();
        RawStateEntry receipt = await raw.ReadAsync(scope.ComputeMarkerKey(), default);
        receipt.Value.Length.ShouldBeGreaterThan(ReadModelBatchMarker.MaxControlBytes);
        receipt.Value.Length.ShouldBeLessThanOrEqualTo(ReadModelBatchMarker.MaxRecoveryEncodedBytes);
        using DaprClient replacement = Client();
        store = new DaprReadModelStore(replacement);
        (await store.RecoverPendingAsync(scope, scope.ComputeMarkerKey())).IsResolved.ShouldBeTrue();
        (await store.RecoverPendingAsync(scope, scope.ComputeMarkerKey())).IsResolved.ShouldBeTrue();
        RawStateEntry model = await raw.ReadAsync(escaped, default);
        ReadModelBatchExpiringValue.Decode(model.Value).Deadline.ShouldBe(deadline);
        TimeSpan ttl = (await _redis.GetDatabase().KeyTimeToLiveAsync(Key(escaped))).ShouldNotBeNull();
        ttl.ShouldBeGreaterThan(TimeSpan.Zero); ttl.ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
        TestContext.Current.TestOutputHelper!.WriteLine($"review5-terminal-anchor;interrupted={interrupted};physical-receipt-bytes={receipt.Value.Length};physical-bound={ReadModelBatchMarker.MaxRecoveryEncodedBytes};unchanged-logical-control-bound={ReadModelBatchMarker.MaxControlBytes};replacement-repeated-recovery=true;original-deadline-and-positive-physical-TTL=true");
    }

}
