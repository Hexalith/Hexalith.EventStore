using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Gateway;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Projections.Checkpointed;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Streams;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections.Checkpointed;

/// <summary>
/// Story 16.1 review-loop fixes: truncated and lagging history stay retryable, a vanished record is never
/// recovered, recovery leases are per invocation, persisted health and schema have no defaults, and the
/// scope-factory history resolves a gateway client per read.
/// </summary>
public sealed class CheckpointedProjectionReviewFixTests {
    private const string Store = CheckpointedProjectionHarness.StoreName;
    private const string Key = CheckpointedProjectionHarness.Key;

    [Fact]
    public async Task TruncatedHistoryPageWithoutNewEventsIsUnavailableAndNeverCorrupts() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);
        harness.Gateway.AppendRange(2, 3);
        harness.Gateway.TruncateWithoutEvents = true;

        CheckpointedProjectionUnavailableException direct = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => new CheckpointedProjectionHistory(harness.Gateway).ReadRangeAsync(
                CheckpointedProjectionHarness.Stream, 2, 3, TestContext.Current.CancellationToken));
        CheckpointedProjectionUnavailableException delivery = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => harness.ApplyAsync(3));

        direct.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
        delivery.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
        harness.Record.ShouldNotBeNull().Health.ShouldBe(CheckpointedProjectionHealth.Gapped);

        harness.Gateway.TruncateWithoutEvents = false;
        (await harness.ApplyAsync(3)).Outcome.ShouldBe(CheckpointedProjectionOutcome.Recovered);
    }

    [Fact]
    public async Task HistoryLaggingBehindTheHorizonIsRetryableUntilItCatchesUp() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);

        // Delivery of 4 arrives before the authoritative read model exposes anything past 2.
        harness.Gateway.Append(2);
        CheckpointedProjectionUnavailableException lagging = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => harness.ApplyAsync(4));

        lagging.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
        CheckpointedProjectionRecord<CheckpointedTestState> gapped = harness.Record.ShouldNotBeNull();
        gapped.Health.ShouldBe(CheckpointedProjectionHealth.Gapped, "lagging history is never classified as missing");
        gapped.CorruptionReason.ShouldBeNull();

        harness.Gateway.AppendRange(3, 4);
        (await harness.ApplyAsync(4)).Outcome.ShouldBe(CheckpointedProjectionOutcome.Recovered);
        harness.Record.ShouldNotBeNull().State.ShouldNotBeNull().Sequences.ShouldBe([1L, 2L, 3L, 4L]);
    }

    [Fact]
    public async Task RecoveryOfAnAbsentRecordIsUnavailable() {
        var harness = new CheckpointedProjectionHarness();

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => harness.Worker("worker-a").RecoverAsync(Store, Key, CheckpointedProjectionHarness.Stream, TestContext.Current.CancellationToken));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.RecordMissing);
    }

    [Fact]
    public async Task RecordThatDisappearsAfterThePendingCommitIsNeverAcknowledgedAsRecovered() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);
        harness.Gateway.AppendRange(2, 3);
        var erasing = new ErasingAfterPendingStore(harness.Store);
        var writer = new CheckpointedProjectionWriter<CheckpointedTestState>(
            erasing,
            harness.Transition,
            new CheckpointedProjectionHistory(harness.Gateway),
            CheckpointedProjectionHarness.Options("replica-a"),
            harness.Clock);

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => writer.ApplyAsync(Store, Key, CheckpointedProjectionHarness.Delivery(3), TestContext.Current.CancellationToken));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.RecordMissing);
        erasing.Erased.ShouldBeTrue();
        harness.Record.ShouldBeNull();
    }

    [Fact]
    public async Task ConcurrentRecoveriesOfOneWorkerNeverSupersedeEachOther() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);
        harness.Gateway.AppendRange(2, 4);
        _ = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(() => harness.ApplyAsync(4, withHistory: false));
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        harness.Gateway.BeforeRead = async (_, cancellationToken) => {
            if (Interlocked.Increment(ref reads) == 1) {
                entered.TrySetResult();
                await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        };
        CheckpointedProjectionRecoveryWorker<CheckpointedTestState> worker = harness.Worker("worker-a");

        Task<CheckpointedProjectionResult<CheckpointedTestState>> first = worker.RecoverAsync(Store, Key, CheckpointedProjectionHarness.Stream, TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        string firstOwner = harness.Record.ShouldNotBeNull().Lease.ShouldNotBeNull().WorkerId;
        CheckpointedProjectionUnavailableException second = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => worker.RecoverAsync(Store, Key, CheckpointedProjectionHarness.Stream, TestContext.Current.CancellationToken));
        release.SetResult();
        CheckpointedProjectionResult<CheckpointedTestState> result = await first;

        second.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.RecoveryLeaseHeld);
        firstOwner.ShouldStartWith("worker-a/");
        firstOwner.ShouldNotBe("worker-a");
        result.Outcome.ShouldBe(CheckpointedProjectionOutcome.Recovered);
        harness.Record.ShouldNotBeNull().LastFencingToken.ShouldBe(1, "the second invocation never superseded the first");
    }

    [Fact]
    public async Task CreatedRecordsPersistExplicitHealthAndSchemaVersion() {
        var harness = new CheckpointedProjectionHarness();

        (await harness.ApplyAsync(1)).Outcome.ShouldBe(CheckpointedProjectionOutcome.Applied);

        JsonObject persisted = JsonNode.Parse(harness.Store.SnapshotRaw(Store, Key).ShouldNotBeNull())!.AsObject();
        persisted["health"]!.GetValue<int>().ShouldBe((int)CheckpointedProjectionHealth.Healthy);
        persisted["schemaVersion"]!.GetValue<int>().ShouldBe(CheckpointedProjectionRecord<CheckpointedTestState>.CurrentSchemaVersion);
        CheckpointedProjectionRecord<CheckpointedTestState> empty = CheckpointedProjectionRecord<CheckpointedTestState>.CreateEmpty();
        empty.IsWellFormed().ShouldBeTrue();
        new CheckpointedProjectionRecord<CheckpointedTestState>().IsWellFormed().ShouldBeFalse("a record without explicit fields is malformed");
    }

    [Theory]
    [InlineData("health")]
    [InlineData("schemaVersion")]
    public async Task StoredRecordMissingHealthOrSchemaVersionIsMalformed(string field) {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(2);
        JsonObject persisted = JsonNode.Parse(harness.Store.SnapshotRaw(Store, Key).ShouldNotBeNull())!.AsObject();
        persisted.Remove(field).ShouldBeTrue();
        harness.Store.SeedRaw(Store, Key, persisted);

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => harness.ApplyAsync(3));
        CheckpointedProjectionUnavailableException read = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => harness.Writer().ReadAsync(Store, Key, TestContext.Current.CancellationToken));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.MalformedRecord);
        read.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.MalformedRecord);
    }

    [Fact]
    public async Task ScopeFactoryHistoryResolvesAGatewayClientPerRead() {
        var harness = new CheckpointedProjectionHarness();
        harness.Gateway.AppendRange(1, 3);
        int created = 0;
        int disposed = 0;
        ServiceCollection services = new();
        services.AddScoped<IEventStoreGatewayClient>(_ => {
            Interlocked.Increment(ref created);
            return new DisposableGateway(harness.Gateway, () => Interlocked.Increment(ref disposed));
        });
        await using ServiceProvider provider = services.BuildServiceProvider();
        var history = new CheckpointedProjectionHistory(provider.GetRequiredService<IServiceScopeFactory>());

        (await history.ReadRangeAsync(CheckpointedProjectionHarness.Stream, 1, 3, TestContext.Current.CancellationToken)).Count.ShouldBe(3);
        (await history.ReadAtAsync(CheckpointedProjectionHarness.Stream, 2, TestContext.Current.CancellationToken)).ShouldNotBeNull().Sequence.ShouldBe(2);

        created.ShouldBe(2);
        disposed.ShouldBe(2, "each read disposes the scope that owned its client");
    }

    [Fact]
    public async Task ScopeFactoryHistoryWithoutAGatewayIsUnavailable() {
        await using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        var history = new CheckpointedProjectionHistory(provider.GetRequiredService<IServiceScopeFactory>());

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => history.ReadRangeAsync(CheckpointedProjectionHarness.Stream, 1, 1, TestContext.Current.CancellationToken));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
    }

    private sealed class ErasingAfterPendingStore(IReadModelStore inner) : IReadModelStore {
        public bool Erased { get; private set; }

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key, CancellationToken cancellationToken = default)
            where TValue : class
            => inner.GetAsync<TValue>(storeName, key, cancellationToken);

        public Task SaveAsync<TValue>(string storeName, string key, TValue value, CancellationToken cancellationToken = default)
            where TValue : class
            => inner.SaveAsync(storeName, key, value, cancellationToken);

        public async Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default)
            where TValue : class {
            bool saved = await inner.TrySaveAsync(storeName, key, value, etag, cancellationToken).ConfigureAwait(false);
            if (saved && value is CheckpointedProjectionRecord<CheckpointedTestState> { Health: CheckpointedProjectionHealth.Gapped } && !Erased) {
                // Another actor removes the record between the pending commit and recovery.
                ReadModelEntry<CheckpointedProjectionRecord<CheckpointedTestState>> current = await inner
                    .GetAsync<CheckpointedProjectionRecord<CheckpointedTestState>>(storeName, key, cancellationToken)
                    .ConfigureAwait(false);
                Erased = await ((IReadModelConditionalEraser)inner).TryEraseAsync(storeName, key, current.ETag!, cancellationToken).ConfigureAwait(false);
            }

            return saved;
        }
    }

    private sealed class DisposableGateway(IEventStoreGatewayClient inner, Action onDispose) : IEventStoreGatewayClient, IDisposable {
        public Task<StreamReadPage> ReadStreamAsync(StreamReadRequest request, CancellationToken cancellationToken = default)
            => inner.ReadStreamAsync(request, cancellationToken);

        public Task<SubmitCommandResponse> SubmitCommandAsync(SubmitCommandRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<EventStoreQueryResult> SubmitQueryAsync(SubmitQueryRequest request, string? ifNoneMatch = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<EventStoreQueryResult<T>> SubmitQueryAsync<T>(SubmitQueryRequest request, string? ifNoneMatch = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void Dispose() => onDispose();
    }
}
