using System.Text;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Projections.Checkpointed;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections.Checkpointed;

/// <summary>
/// Story 16.1 review-loop-2 fixes: the expected sequence merges as evidence while gapped, transitions may keep
/// no materialized state, every recovery failure except lease loss releases the lease, and a missing history
/// payload is unavailable rather than a fingerprint mismatch.
/// </summary>
public sealed class CheckpointedProjectionReviewLoop2Tests {
    private const string Store = CheckpointedProjectionHarness.StoreName;
    private const string Key = CheckpointedProjectionHarness.Key;

    [Fact]
    public async Task ExpectedSequenceArrivingWhileGappedIsMergedAsEvidence() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);
        harness.Gateway.AppendRange(2, 3);
        _ = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(() => harness.ApplyAsync(3, withHistory: false));

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => harness.ApplyAsync(2, withHistory: false));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
        CheckpointedProjectionRecord<CheckpointedTestState> merged = harness.Record.ShouldNotBeNull();
        merged.Health.ShouldBe(CheckpointedProjectionHealth.Gapped, "the expected sequence never bypasses fenced recovery");
        merged.Expected.ShouldBe(2);
        merged.RecoveryThrough.ShouldBe(3);
        merged.LastContiguousSequence.ShouldBe(1);
        merged.Pending.ShouldBe([
            new CheckpointedProjectionPendingEvidence(2, CheckpointedProjectionHarness.Delivery(2).Evidence),
            new CheckpointedProjectionPendingEvidence(3, CheckpointedProjectionHarness.Delivery(3).Evidence),
        ]);
        harness.Transition.Applications.ShouldBe(1);

        (await harness.ApplyAsync(2)).Outcome.ShouldBe(CheckpointedProjectionOutcome.Recovered);
        harness.Record.ShouldNotBeNull().State.ShouldNotBeNull().Sequences.ShouldBe([1L, 2L, 3L]);
    }

    [Fact]
    public async Task TransitionsMayAdvanceTheCheckpointWithoutMaterializingState() {
        var harness = new CheckpointedProjectionHarness();
        var writer = new CheckpointedProjectionWriter<CheckpointedTestState>(harness.Store, new CreateOnlyTransition());

        (await writer.ApplyAsync(Store, Key, CheckpointedProjectionHarness.Delivery(1, "ignored"), TestContext.Current.CancellationToken))
            .Outcome.ShouldBe(CheckpointedProjectionOutcome.Applied);
        CheckpointedProjectionRecord<CheckpointedTestState> empty = harness.Record.ShouldNotBeNull();
        (await writer.ApplyAsync(Store, Key, CheckpointedProjectionHarness.Delivery(2, "create"), TestContext.Current.CancellationToken))
            .Outcome.ShouldBe(CheckpointedProjectionOutcome.Applied);

        empty.Health.ShouldBe(CheckpointedProjectionHealth.Healthy);
        empty.LastContiguousSequence.ShouldBe(1);
        empty.State.ShouldBeNull("the stream had no materialized state yet");
        empty.IsWellFormed().ShouldBeTrue();
        CheckpointedProjectionRecord<CheckpointedTestState> created = harness.Record.ShouldNotBeNull();
        created.LastContiguousSequence.ShouldBe(2);
        created.State.ShouldNotBeNull().Values.ShouldBe(["create"]);
    }

    [Fact]
    public async Task RecoveryReleasesItsLeaseAfterConcurrencyExhaustion() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);
        harness.Gateway.AppendRange(2, 3);
        _ = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(() => harness.ApplyAsync(3, withHistory: false));
        var conflicting = new RefusingHealthyWriteStore(harness.Store);
        var worker = new CheckpointedProjectionRecoveryWorker<CheckpointedTestState>(
            conflicting,
            harness.Transition,
            new CheckpointedProjectionHistory(harness.Gateway),
            CheckpointedProjectionHarness.Options("worker-a"),
            harness.Clock);

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => worker.RecoverAsync(Store, Key, CheckpointedProjectionHarness.Stream, TestContext.Current.CancellationToken));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.ConcurrencyExhausted);
        conflicting.RefusedHealthyWrites.ShouldBe(ReadModelWritePolicy.DefaultMaxAttempts);
        CheckpointedProjectionRecord<CheckpointedTestState> record = harness.Record.ShouldNotBeNull();
        record.Health.ShouldBe(CheckpointedProjectionHealth.Gapped);
        record.Lease.ShouldBeNull("only a lost lease is left in place");
        record.LastFencingToken.ShouldBe(1);
    }

    [Fact]
    public async Task HistoryEventWithoutPayloadIsUnavailableNotCorrupt() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);
        harness.Gateway.AppendWithoutPayload(2);
        harness.Gateway.Append(3);

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => harness.ApplyAsync(3));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
        CheckpointedProjectionRecord<CheckpointedTestState> record = harness.Record.ShouldNotBeNull();
        record.Health.ShouldBe(CheckpointedProjectionHealth.Gapped);
        record.CorruptionReason.ShouldBeNull();
        record.Lease.ShouldBeNull();
    }

    private sealed class CreateOnlyTransition : ICheckpointedProjectionTransition<CheckpointedTestState> {
        public CheckpointedTestState? Apply(CheckpointedTestState? current, CheckpointedProjectionEvent @event) {
            string value = @event.DecodedEvent as string ?? Encoding.UTF8.GetString(@event.Payload ?? []);
            return current is null && value != "create"
                ? null
                : new CheckpointedTestState([.. current?.Sequences ?? [], @event.Sequence], [.. current?.Values ?? [], value]);
        }
    }

    private sealed class RefusingHealthyWriteStore(IReadModelStore inner) : IReadModelStore {
        public int RefusedHealthyWrites { get; private set; }

        public Task<ReadModelEntry<TValue>> GetAsync<TValue>(string storeName, string key, CancellationToken cancellationToken = default)
            where TValue : class
            => inner.GetAsync<TValue>(storeName, key, cancellationToken);

        public Task SaveAsync<TValue>(string storeName, string key, TValue value, CancellationToken cancellationToken = default)
            where TValue : class
            => inner.SaveAsync(storeName, key, value, cancellationToken);

        public Task<bool> TrySaveAsync<TValue>(string storeName, string key, TValue value, string etag, CancellationToken cancellationToken = default)
            where TValue : class {
            if (value is CheckpointedProjectionRecord<CheckpointedTestState> { Health: CheckpointedProjectionHealth.Healthy }) {
                // The final compare-and-swap keeps losing to a writer that never takes the lease.
                RefusedHealthyWrites++;
                return Task.FromResult(false);
            }

            return inner.TrySaveAsync(storeName, key, value, etag, cancellationToken);
        }
    }
}
