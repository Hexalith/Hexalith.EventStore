using System.Text.Json;
using System.Text.Json.Nodes;
using Dapr.Client;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

public sealed partial class ProjectionExpiryAndScratchRecoveryLiveTests
{
    [Theory]
    [InlineData(ReadModelBatchPhase.MarkerPrepared, -1, false, false)]
    [InlineData(ReadModelBatchPhase.MarkerPrepared, -1, true, true)]
    [InlineData(ReadModelBatchPhase.BeforeInstallOperation, 0, false, true)]
    [InlineData(ReadModelBatchPhase.BeforeInstallOperation, 0, true, false)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 0, false, false)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 0, true, true)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 1, false, true)]
    [InlineData(ReadModelBatchPhase.AfterInstallOperation, 1, true, false)]
    [InlineData(ReadModelBatchPhase.BeforeCommit, -1, false, false)]
    [InlineData(ReadModelBatchPhase.BeforeCommit, -1, true, true)]
    [InlineData(ReadModelBatchPhase.AfterCommit, -1, false, true)]
    [InlineData(ReadModelBatchPhase.AfterCommit, -1, true, false)]
    [InlineData(ReadModelBatchPhase.BeforeCompaction, 0, false, false)]
    [InlineData(ReadModelBatchPhase.BeforeCompaction, 0, true, true)]
    [InlineData(ReadModelBatchPhase.RecoveryManifestReclaimed, 1, false, true)]
    [InlineData(ReadModelBatchPhase.RecoveryManifestReclaimed, 1, true, false)]
    public async Task ProductionNamedDispatcherRetriesOrAbortsInterruptedScratchAndDeliversAfterRecovery(
        ReadModelBatchPhase phase, int ordinal, bool populated, bool retryFirst)
    {
        string tenant = "named-scratch-" + Guid.NewGuid().ToString("N");
        using DaprClient originalClient = Client();
        var originalStore = new DaprReadModelStore(originalClient);
        var handler = new LivePartitionedScratchHandler();
        SharedProjectionScope scope = handler.CreateScope(tenant);
        var coordinator = new SharedProjectionEpochCoordinator(originalStore, originalStore);
        using ServiceProvider originalProvider = NamedProvider(originalStore, originalStore, coordinator, handler);
        _ = await coordinator.RegisterWriterAsync(scope, handler.OrdinaryWriterId);
        ProjectionEventDto firstEvent = NamedEvent(1);
        if (populated)
        {
            DomainSharedProjectionRebuildIdentity old = NamedIdentity(tenant, "old");
            (await NamedBegin(originalProvider, old, [("owner-a", firstEvent)])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            DomainSharedProjectionRebuildResponse accepted = await NamedAccumulate(originalProvider, old, "owner-a", firstEvent);
            (await NamedFinish(originalProvider, old, accepted)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        }
        long selectedGeneration = (await coordinator.GetStatusAsync(scope)).ActiveGeneration;
        DomainSharedProjectionRebuildIdentity identity = NamedIdentity(tenant, "interrupted");
        (await NamedBegin(originalProvider, identity, [("owner-a", firstEvent), ("owner-b", firstEvent)])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        var fault = new ScratchRecoveryLiveBatchStore(originalClient, originalStore, phase, ordinal, "owner-scratch:");
        using ServiceProvider interruptedProvider = NamedProvider(originalStore, fault, new SharedProjectionEpochCoordinator(originalStore, fault), handler);
        (await NamedAccumulate(interruptedProvider, identity, "owner-a", firstEvent)).Status.ShouldBe(ProjectionDispatchStatus.Indeterminate);
        string sessionKey = DomainSharedProjectionRebuildSessionKey.Compute(identity);
        string scratch = DomainSharedProjectionRebuildDispatcher.PartitionChunkKey(sessionKey, 0, 0);
        byte[] raw = (byte[])(await _redis.GetDatabase().HashGetAsync(Key(scratch), "data"))!;
        byte[] fenceRaw = (byte[])(await _redis.GetDatabase().HashGetAsync(Key(scope.StateKey), "data"))!;
        if (phase is ReadModelBatchPhase.MarkerPrepared or ReadModelBatchPhase.BeforeInstallOperation)
        {
            raw.ShouldBeNull(); ReadModelBatchEnvelope.IsEnvelope(fenceRaw).ShouldBeFalse();
            await AssertPreparedOwnerManifestAsync(originalClient, scope, identity);
        }
        else (ReadModelBatchEnvelope.IsEnvelope(raw) || ReadModelBatchEnvelope.IsEnvelope(fenceRaw)).ShouldBeTrue("the interruption must retain actual pending SDK evidence");
        ((await coordinator.ReadAsync<JsonObject>(scope, "row:owner-a")).Value?["count"]?.GetValue<int>()).ShouldBe(populated ? 1 : null);
        (await coordinator.ReadAsync<JsonObject>(scope, "row:owner-b")).Value.ShouldBeNull();

        using DaprClient replacedClient = Client();
        var store = new DaprReadModelStore(replacedClient);
        coordinator = new SharedProjectionEpochCoordinator(store, store);
        using ServiceProvider provider = NamedProvider(store, store, coordinator, handler);
        if (retryFirst)
        {
            (await NamedAccumulate(provider, identity, "owner-a", firstEvent)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
            (await NamedAccumulate(provider, identity, "owner-a", firstEvent)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        }
        DomainSharedProjectionRebuildResponse aborted = await NamedDispatch(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Abort, identity));
        aborted.Status.ShouldBe(ProjectionDispatchStatus.Completed, aborted.ToString());
        aborted.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Aborted);
        SharedProjectionEpochStatus status = await coordinator.GetStatusAsync(scope);
        status.IsBuilding.ShouldBeFalse(); status.IsAborting.ShouldBeFalse(); status.ActiveGeneration.ShouldBe(selectedGeneration);
        ((await coordinator.ReadAsync<JsonObject>(scope, "row:owner-a")).Value?["count"]?.GetValue<int>()).ShouldBe(populated ? 1 : null);
        (await _redis.GetDatabase().KeyExistsAsync(Key(scratch))).ShouldBeFalse();
        (await _redis.GetDatabase().KeyExistsAsync(Key(DomainSharedProjectionRebuildDispatcher.PartitionManifestKey(sessionKey, 0)))).ShouldBeFalse();
        SharedProjectionEpochState persisted = JsonSerializer.Deserialize<SharedProjectionEpochState>((byte[])(await _redis.GetDatabase().HashGetAsync(Key(scope.StateKey), "data"))!, JsonSerializerOptions.Web)!;
        persisted.Phase.ShouldBe(SharedProjectionEpochPhase.Open);
        foreach (string owner in new[] { "owner-a", "owner-b" })
        {
            ProjectionEventDto growth = NamedEvent(9) with { GlobalPosition = 2, SequenceNumber = populated && owner == "owner-a" ? 2 : 1, MessageId = "growth-" + owner };
            ProjectionDispatchResponse delivered = await DomainProjectionDispatcher.DispatchAsync(provider,
                new ProjectionDispatchRequest(new ProjectionRequest(tenant, "widget", owner, [growth]), [handler.ProjectionType], growth.MessageId, identity.CatalogFingerprint),
                new ProjectionDispatchOptions(), new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" }, CancellationToken.None);
            delivered.Outcomes.Single().Status.ShouldBe(ProjectionDispatchStatus.Completed, delivered.ToString());
            (await coordinator.ReadAsync<JsonObject>(scope, "row:" + owner)).Value!["count"]!.GetValue<int>().ShouldBe(9);
        }
        DomainSharedProjectionRebuildIdentity fresh = NamedIdentity(tenant, "fresh");
        ProjectionEventDto captured = NamedEvent(9) with { GlobalPosition = 2, MessageId = "growth-owner-b" };
        (await NamedBegin(provider, fresh, [("owner-b", captured)])).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        DomainSharedProjectionRebuildResponse next = await NamedAccumulate(provider, fresh, "owner-b", captured);
        (await NamedFinish(provider, fresh, next)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        (await coordinator.ReadAsync<JsonObject>(scope, "row:owner-b")).Value!["count"]!.GetValue<int>().ShouldBe(9);
        TestContext.Current.TestOutputHelper!.WriteLine($"phase={phase}; ordinal={ordinal}; populated={populated}; retry={retryFirst}; production-dispatcher=true; private-cleaned=true; epoch=Open; ordinary-owners=2; fresh-rebuild=true; replaced-client-store-coordinator=true");
    }

    internal static ReadModelBatchScope OwnerScratchScope(SharedProjectionScope scope, DomainSharedProjectionRebuildIdentity identity)
        => new(scope.StoreName, scope.TenantId, scope.Domain, scope.Family, scope.Family,
            "owner-scratch:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(identity.OperationId))) + ":0:0");

    internal static async Task AssertPreparedOwnerManifestAsync(DaprClient client, SharedProjectionScope scope,
        DomainSharedProjectionRebuildIdentity identity)
    {
        ReadModelBatchScope batchScope = OwnerScratchScope(scope, identity);
        var raw = new DaprReadModelBatchStateAccessor(client, scope.StoreName);
        RawStateEntry bytes = await raw.ReadAsync(batchScope.ComputeMarkerKey(), CancellationToken.None);
        ReadModelBatchMarker marker = ReadModelBatchMarker.ReadProof(bytes.Value).ShouldNotBeNull();
        marker.Status.ShouldBe(ReadModelBatchMarkerStatus.Prepared); marker.ScopeHash.ShouldBe(batchScope.ComputeScopeHash());
        ReadOnlyMemory<byte> manifest = marker.OriginalManifest;
        if (marker.OriginalManifestKey is { } key) manifest = (await raw.ReadAsync(key, CancellationToken.None)).Value;
        manifest.IsEmpty.ShouldBeFalse();
        ReadModelBatchFingerprint.ComputeFromManifest(manifest.Span).ShouldBe(marker.Fingerprint);
        TestContext.Current.TestOutputHelper!.WriteLine($"prepared-marker={System.Text.Encoding.UTF8.GetString(bytes.Value.Span)}; immutable-manifest={System.Text.Encoding.UTF8.GetString(manifest.Span)}; no-participant-envelope=true");
    }

    internal static ServiceProvider NamedProvider(DaprReadModelStore store, IReadModelBatchStore batches, SharedProjectionEpochCoordinator coordinator, LivePartitionedScratchHandler handler)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IReadModelStore>(store); services.AddSingleton(batches); services.AddSingleton(coordinator);
        services.AddScoped<IAsyncDomainProjectionHandler>(_ => handler);
        return services.BuildServiceProvider();
    }
    internal static DomainSharedProjectionRebuildIdentity NamedIdentity(string tenant, string operation)
        => new(tenant, "widget", "partitioned-widget", operation, ProjectionRouteCatalogFingerprint.Compute("widget-service", "v1", [new ProjectionDispatchRoute("widget", "partitioned-widget")]));
    internal static ProjectionEventDto NamedEvent(byte value)
        => new("widget-changed", [value], "json", 1, DateTimeOffset.UnixEpoch, "correlation", "message-1", "user", 1);
    internal static Task<DomainSharedProjectionRebuildResponse> NamedDispatch(IServiceProvider provider, DomainSharedProjectionRebuildRequest request)
        => DomainSharedProjectionRebuildDispatcher.DispatchAsync(provider, request, new ProjectionDispatchOptions(), new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" }, CancellationToken.None);
    internal static Task<DomainSharedProjectionRebuildResponse> NamedBegin(IServiceProvider provider, DomainSharedProjectionRebuildIdentity identity, (string Owner, ProjectionEventDto Event)[] histories)
    {
        string fingerprint = DomainSharedProjectionRebuildFingerprint.EmptyInventory;
        for (int i = 0; i < histories.Length; i++) fingerprint = DomainSharedProjectionRebuildFingerprint.AppendInventory(fingerprint, i, DomainSharedProjectionRebuildFingerprint.ComputeHistory(histories[i].Owner, false, [histories[i].Event]));
        return NamedDispatch(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin, identity,
            CaptureInventoryFingerprint: fingerprint, SourceHighWatermarks: histories.ToDictionary(item => item.Owner, item => item.Event.GlobalPosition)));
    }
    internal static Task<DomainSharedProjectionRebuildResponse> NamedAccumulate(IServiceProvider provider, DomainSharedProjectionRebuildIdentity identity, string owner, ProjectionEventDto source)
        => NamedDispatch(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Accumulate, identity, AggregateOrdinal: 0, AggregateId: owner, Events: [source]));
    internal static async Task<DomainSharedProjectionRebuildResponse> NamedFinish(IServiceProvider provider, DomainSharedProjectionRebuildIdentity identity, DomainSharedProjectionRebuildResponse current)
    {
        current = await NamedDispatch(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Finalize, identity, ExpectedAggregateCount: current.AcceptedAggregateCount, ExpectedInventoryFingerprint: current.InventoryFingerprint));
        current.Status.ShouldBe(ProjectionDispatchStatus.Completed, current.ToString());
        foreach (DomainSharedProjectionRebuildAction action in new[] { DomainSharedProjectionRebuildAction.Stage, DomainSharedProjectionRebuildAction.Commit, DomainSharedProjectionRebuildAction.Verify })
        {
            current = await NamedDispatch(provider, new(DomainSharedProjectionRebuildProtocol.Version, action, identity));
            current.Status.ShouldBeOneOf(ProjectionDispatchStatus.Completed, ProjectionDispatchStatus.AlreadyCompleted);
        }
        return current;
    }
}
