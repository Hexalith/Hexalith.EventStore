using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed class DomainSharedProjectionRebuildDispatcherTests {
    private const string IndexKey = "widget:shared-index";
    private const string StoreName = "statestore";

    [Fact]
    public async Task Session_MultiAggregateErasedDuplicateAndCommit_ProducesVerifiedAtomicReplacement() {
        var store = new InMemoryReadModelStore();
        await store.SaveAsync(StoreName, IndexKey, new SharedIndex(["stale"]));
        var staging = new RecordingStagingStore(store);
        var handler = new SharedIndexHandler();
        using ServiceProvider provider = BuildProvider(store, staging, handler);
        DomainSharedProjectionRebuildIdentity identity = CreateIdentity("operation-1");

        DomainSharedProjectionRebuildResponse current = await DispatchAsync(provider, Begin(identity));
        current.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Accumulating);
        current = await DispatchAsync(provider, Accumulate(identity, 0, "aggregate-a", false, ProjectionEvent(1, 1)));
        DomainSharedProjectionRebuildResponse duplicate = await DispatchAsync(
            provider,
            Accumulate(identity, 0, "aggregate-a", false, ProjectionEvent(1, 1)));
        duplicate.Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        current = await DispatchAsync(provider, Accumulate(identity, 1, "aggregate-b", true));
        current = await DispatchAsync(provider, Accumulate(identity, 2, "aggregate-c", false, ProjectionEvent(1, 3)));

        current = await DispatchAsync(provider, Finalize(identity, current));
        current.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Finalized);
        current = await DispatchAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Stage));
        current.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Prepared);
        (await store.GetAsync<SharedIndex>(StoreName, IndexKey)).Value!.AggregateIds.ShouldBe(["stale"]);

        current = await DispatchAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Commit));

        current.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Committed);
        (await store.GetAsync<SharedIndex>(StoreName, IndexKey)).Value!.AggregateIds.ShouldBe(["aggregate-a", "aggregate-c"]);
        handler.AccumulateCalls.ShouldBe(2);
        staging.VerifyCalls.ShouldBe(1);
    }

    [Fact]
    public async Task EmptyInventory_FinalizationPrunesStaleSharedView() {
        var store = new InMemoryReadModelStore();
        await store.SaveAsync(StoreName, IndexKey, new SharedIndex(["stale-a", "stale-b"]));
        var handler = new SharedIndexHandler();
        using ServiceProvider provider = BuildProvider(store, store, handler);
        DomainSharedProjectionRebuildIdentity identity = CreateIdentity("operation-empty");

        DomainSharedProjectionRebuildResponse current = await DispatchAsync(provider, Begin(identity));
        current = await DispatchAsync(provider, Finalize(identity, current));
        _ = await DispatchAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Stage));
        current = await DispatchAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Commit));

        current.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Committed);
        (await store.GetAsync<SharedIndex>(StoreName, IndexKey)).Value!.AggregateIds.ShouldBeEmpty();
        handler.AccumulateCalls.ShouldBe(0);
    }

    [Fact]
    public async Task CancellationConflictAndAbort_PreserveLiveViewAndAllowNewOperationRetry() {
        var store = new InMemoryReadModelStore();
        await store.SaveAsync(StoreName, IndexKey, new SharedIndex(["live-before"]));
        var handler = new SharedIndexHandler();
        using ServiceProvider provider = BuildProvider(store, store, handler);
        DomainSharedProjectionRebuildIdentity identity = CreateIdentity("operation-abort");

        DomainSharedProjectionRebuildResponse current = await DispatchAsync(provider, Begin(identity));
        current = await DispatchAsync(provider, Accumulate(identity, 0, "aggregate-a", false, ProjectionEvent(1, 1)));
        DomainSharedProjectionRebuildResponse conflict = await DispatchAsync(
            provider,
            Accumulate(identity, 0, "aggregate-a", false, ProjectionEvent(1, 99)));
        conflict.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        conflict.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        using (var cancellation = new CancellationTokenSource()) {
            await cancellation.CancelAsync();
            _ = await Should.ThrowAsync<OperationCanceledException>(() => DispatchAsync(
                provider,
                Accumulate(identity, 1, "aggregate-b", false, ProjectionEvent(1, 2)),
                cancellation.Token));
        }

        current = await DispatchAsync(provider, Finalize(identity, current));
        _ = await DispatchAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Stage));
        current = await DispatchAsync(provider, Lifecycle(identity, DomainSharedProjectionRebuildAction.Abort));
        current.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Aborted);
        (await store.GetAsync<SharedIndex>(StoreName, IndexKey)).Value!.AggregateIds.ShouldBe(["live-before"]);

        DomainSharedProjectionRebuildIdentity retryIdentity = CreateIdentity("operation-retry");
        current = await DispatchAsync(provider, Begin(retryIdentity));
        current = await DispatchAsync(provider, Accumulate(retryIdentity, 0, "aggregate-z", false, ProjectionEvent(1, 7)));
        current = await DispatchAsync(provider, Finalize(retryIdentity, current));
        _ = await DispatchAsync(provider, Lifecycle(retryIdentity, DomainSharedProjectionRebuildAction.Stage));
        current = await DispatchAsync(provider, Lifecycle(retryIdentity, DomainSharedProjectionRebuildAction.Commit));

        current.Phase.ShouldBe(DomainSharedProjectionRebuildPhase.Committed);
        (await store.GetAsync<SharedIndex>(StoreName, IndexKey)).Value!.AggregateIds.ShouldBe(["aggregate-z"]);
    }

    private static DomainSharedProjectionRebuildRequest Accumulate(
        DomainSharedProjectionRebuildIdentity identity,
        long ordinal,
        string aggregateId,
        bool erased,
        params ProjectionEventDto[] events)
        => new(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Accumulate,
            identity,
            ordinal,
            aggregateId,
            erased,
            events);

    private static DomainSharedProjectionRebuildRequest Begin(DomainSharedProjectionRebuildIdentity identity)
        => Lifecycle(identity, DomainSharedProjectionRebuildAction.Begin);

    private static ServiceProvider BuildProvider(
        IReadModelStore sessionStore,
        IReadModelBatchStagingStore stagingStore,
        SharedIndexHandler handler) {
        var services = new ServiceCollection();
        _ = services.AddSingleton(sessionStore);
        _ = services.AddSingleton(stagingStore);
        _ = services.AddScoped<IAsyncDomainProjectionHandler>(_ => handler);
        return services.BuildServiceProvider();
    }

    private static DomainSharedProjectionRebuildIdentity CreateIdentity(string operationId) {
        string fingerprint = ProjectionRouteCatalogFingerprint.Compute(
            "widget-service",
            "v1",
            [new ProjectionDispatchRoute("widget", "widget-index")]);
        return new DomainSharedProjectionRebuildIdentity(
            "tenant-a",
            "widget",
            "widget-index",
            operationId,
            fingerprint);
    }

    private static Task<DomainSharedProjectionRebuildResponse> DispatchAsync(
        IServiceProvider provider,
        DomainSharedProjectionRebuildRequest request,
        CancellationToken cancellationToken = default)
        => DomainSharedProjectionRebuildDispatcher.DispatchAsync(
            provider,
            request,
            new ProjectionDispatchOptions(),
            new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
            cancellationToken);

    private static DomainSharedProjectionRebuildRequest Finalize(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildResponse response)
        => new(
            DomainSharedProjectionRebuildProtocol.Version,
            DomainSharedProjectionRebuildAction.Finalize,
            identity,
            ExpectedAggregateCount: response.AcceptedAggregateCount,
            ExpectedInventoryFingerprint: response.InventoryFingerprint);

    private static DomainSharedProjectionRebuildRequest Lifecycle(
        DomainSharedProjectionRebuildIdentity identity,
        DomainSharedProjectionRebuildAction action)
        => new(DomainSharedProjectionRebuildProtocol.Version, action, identity);

    private static ProjectionEventDto ProjectionEvent(long sequence, int value)
        => new(
            "widget-updated",
            JsonSerializer.SerializeToUtf8Bytes(new { value }),
            "json",
            sequence,
            DateTimeOffset.UnixEpoch,
            "correlation-1",
            $"message-{value}",
            "user-1");

    private sealed record CandidateState(IReadOnlyList<string> AggregateIds);

    private sealed class RecordingStagingStore(IReadModelBatchStagingStore inner) : IReadModelBatchStagingStore {
        public int VerifyCalls { get; private set; }

        public Task<ReadModelBatchStagingResult> AbortAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
            => inner.AbortAsync(batch, cancellationToken);

        public Task<ReadModelBatchStagingResult> CommitAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
            => inner.CommitAsync(batch, cancellationToken);

        public Task<ReadModelBatchStagingResult> StageAsync(ReadModelBatch batch, CancellationToken cancellationToken = default)
            => inner.StageAsync(batch, cancellationToken);

        public Task<ReadModelBatchStagingResult> VerifyAsync(ReadModelBatch batch, CancellationToken cancellationToken = default) {
            VerifyCalls++;
            return inner.VerifyAsync(batch, cancellationToken);
        }
    }

    private sealed record SharedIndex(IReadOnlyList<string> AggregateIds);

    private sealed class SharedIndexHandler : IAsyncDomainSharedProjectionRebuildHandler {
        public int AccumulateCalls { get; private set; }

        public string Domain => "widget";

        public string ProjectionType => "widget-index";

        public string RebuildStoreName => StoreName;

        public Task<DomainSharedProjectionRebuildCandidate> AccumulateAsync(
            DomainSharedProjectionRebuildIdentity identity,
            DomainSharedProjectionRebuildCandidate candidate,
            ProjectionRequest aggregateHistory,
            CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            CandidateState state = JsonSerializer.Deserialize<CandidateState>(candidate.State.Span)!;
            AccumulateCalls++;
            return Task.FromResult(new DomainSharedProjectionRebuildCandidate(
                JsonSerializer.SerializeToUtf8Bytes(new CandidateState([.. state.AggregateIds, aggregateHistory.AggregateId]))));
        }

        public Task<DomainSharedProjectionRebuildCandidate> CreateEmptyCandidateAsync(
            DomainSharedProjectionRebuildIdentity identity,
            CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new DomainSharedProjectionRebuildCandidate(
                JsonSerializer.SerializeToUtf8Bytes(new CandidateState([]))));
        }

        public Task<DomainProjectionRebuildPlan> FinalizeAsync(
            DomainSharedProjectionRebuildIdentity identity,
            DomainSharedProjectionRebuildCandidate candidate,
            CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            CandidateState state = JsonSerializer.Deserialize<CandidateState>(candidate.State.Span)!;
            return Task.FromResult(new DomainProjectionRebuildPlan(
                StoreName,
                [ReadModelBatchOperation.Write(
                    IndexKey,
                    new SharedIndex(state.AggregateIds),
                    ReadModelBatchConcurrency.LastWrite)]));
        }

        public Task<DomainProjectionHandlerResult> ProjectAsync(
            ProjectionRequest request,
            string dispatchId,
            CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
