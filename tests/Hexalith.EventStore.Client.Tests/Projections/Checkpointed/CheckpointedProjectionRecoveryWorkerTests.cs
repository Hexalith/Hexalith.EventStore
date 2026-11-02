using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Projections.Checkpointed;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections.Checkpointed;

/// <summary>
/// AD-6 fenced recovery: lease expiry and restart, superseded workers, horizon extension, and failed and
/// successful recovery.
/// </summary>
public sealed class CheckpointedProjectionRecoveryWorkerTests {
    private const string Store = CheckpointedProjectionHarness.StoreName;
    private const string Key = CheckpointedProjectionHarness.Key;

    [Fact]
    public async Task SuccessfulRecoveryValidatesEveryPendingEvidenceAndClearsTheLease() {
        CheckpointedProjectionHarness harness = await GappedAsync(through: 5, pending: [3, 5]);

        CheckpointedProjectionResult<CheckpointedTestState> result = await RecoverAsync(harness, "worker-a");

        result.Outcome.ShouldBe(CheckpointedProjectionOutcome.Recovered);
        CheckpointedProjectionRecord<CheckpointedTestState> record = harness.Record.ShouldNotBeNull();
        record.Health.ShouldBe(CheckpointedProjectionHealth.Healthy);
        record.LastContiguousSequence.ShouldBe(5);
        record.LastAccepted.ShouldBe(CheckpointedProjectionHarness.Delivery(5).Evidence);
        record.State.ShouldNotBeNull().Sequences.ShouldBe([1L, 2L, 3L, 4L, 5L]);
        record.Pending.ShouldBeEmpty();
        record.Lease.ShouldBeNull();
        record.Expected.ShouldBeNull();
        record.RecoveryThrough.ShouldBeNull();
        record.LastFencingToken.ShouldBe(1);
        record.IsWellFormed().ShouldBeTrue();
    }

    [Fact]
    public async Task MissingAuthoritativeHistoryPersistsCorrupt() {
        CheckpointedProjectionHarness harness = await GappedAsync(through: 4, pending: [4]);
        harness.Gateway.Remove(3);

        CheckpointedProjectionResult<CheckpointedTestState> result = await RecoverAsync(harness, "worker-a");

        result.Outcome.ShouldBe(CheckpointedProjectionOutcome.Corrupt);
        CheckpointedProjectionRecord<CheckpointedTestState> record = harness.Record.ShouldNotBeNull();
        record.CorruptionReason.ShouldBe(CheckpointedProjectionCorruptionReason.HistoryMissing);
        record.CorruptSequence.ShouldBe(3);
        record.Lease.ShouldBeNull();
    }

    [Fact]
    public async Task PendingEvidenceContradictingHistoryPersistsCorrupt() {
        CheckpointedProjectionHarness harness = await GappedAsync(through: 4, pending: [4]);

        // Authoritative history at 4 differs from what was delivered and recorded as evidence.
        harness.Gateway.Append(4, "rewritten");

        CheckpointedProjectionResult<CheckpointedTestState> result = await RecoverAsync(harness, "worker-a");

        result.Outcome.ShouldBe(CheckpointedProjectionOutcome.Corrupt);
        harness.Record.ShouldNotBeNull().CorruptionReason.ShouldBe(CheckpointedProjectionCorruptionReason.RecoveryEvidenceMismatch);
    }

    [Fact]
    public async Task FailedRecoveryReleasesItsLeaseAndStaysGapped() {
        CheckpointedProjectionHarness harness = await GappedAsync(through: 4, pending: [4]);
        harness.Gateway.Unavailable = true;

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => RecoverAsync(harness, "worker-a"));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
        CheckpointedProjectionRecord<CheckpointedTestState> record = harness.Record.ShouldNotBeNull();
        record.Health.ShouldBe(CheckpointedProjectionHealth.Gapped);
        record.Lease.ShouldBeNull();
        record.LastFencingToken.ShouldBe(1);

        harness.Gateway.Unavailable = false;
        (await RecoverAsync(harness, "worker-b")).Outcome.ShouldBe(CheckpointedProjectionOutcome.Recovered);
        harness.Record.ShouldNotBeNull().LastFencingToken.ShouldBe(2);
    }

    [Fact]
    public async Task LeaseExpiryAllowsARestartedWorkerToRecoverWithANewFencingToken() {
        CheckpointedProjectionHarness harness = await GappedAsync(through: 4, pending: [4]);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Gateway.BeforeRead = async (_, cancellationToken) => {
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        };
        using var crash = new CancellationTokenSource();

        // Worker A takes the lease and its process dies mid-replay.
        Task<CheckpointedProjectionResult<CheckpointedTestState>> crashed = harness.Worker("worker-a")
            .RecoverAsync(Store, Key, CheckpointedProjectionHarness.Stream, crash.Token);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        await crash.CancelAsync();
        _ = await Should.ThrowAsync<TaskCanceledException>(() => crashed);
        harness.Gateway.BeforeRead = null;
        harness.Record.ShouldNotBeNull().Lease.ShouldNotBeNull().WorkerId.ShouldStartWith("worker-a/");

        CheckpointedProjectionUnavailableException held = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => RecoverAsync(harness, "worker-b"));
        held.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.RecoveryLeaseHeld);

        harness.Clock.UtcNow += TimeSpan.FromSeconds(31);
        CheckpointedProjectionResult<CheckpointedTestState> restarted = await RecoverAsync(harness, "worker-b");

        restarted.Outcome.ShouldBe(CheckpointedProjectionOutcome.Recovered);
        harness.Record.ShouldNotBeNull().LastFencingToken.ShouldBe(2);
        harness.Record!.LastContiguousSequence.ShouldBe(4);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiredUnsupersededLeaseCannotCommitHealthyOrCorrupt(bool corrupt) {
        CheckpointedProjectionHarness harness = await GappedAsync(through: 4, pending: [4]);
        if (corrupt) {
            harness.Gateway.Remove(3);
        }

        (TaskCompletionSource entered, TaskCompletionSource release) = BlockFirstRead(harness);
        Task<CheckpointedProjectionResult<CheckpointedTestState>> stale = RecoverAsync(harness, "worker-a");
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        CheckpointedProjectionRecord<CheckpointedTestState> leased = harness.Record.ShouldNotBeNull();
        leased.Lease.ShouldNotBeNull();
        harness.Clock.UtcNow += TimeSpan.FromSeconds(31);
        release.SetResult();

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(() => stale);

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.RecoveryLeaseLost);
        CheckpointedProjectionRecord<CheckpointedTestState> after = harness.Record.ShouldNotBeNull();
        after.Health.ShouldBe(CheckpointedProjectionHealth.Gapped);
        after.Version.ShouldBe(leased.Version);
        after.Lease.ShouldBe(leased.Lease);
    }

    [Fact]
    public async Task RecordRemovedAfterLeaseAcquisitionReportsRecordMissing() {
        CheckpointedProjectionHarness harness = await GappedAsync(through: 4, pending: [4]);
        (TaskCompletionSource entered, TaskCompletionSource release) = BlockFirstRead(harness);
        Task<CheckpointedProjectionResult<CheckpointedTestState>> recovering = RecoverAsync(harness, "worker-a");
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        ReadModelEntry<CheckpointedProjectionRecord<CheckpointedTestState>> leased = await harness.Store
            .GetAsync<CheckpointedProjectionRecord<CheckpointedTestState>>(Store, Key, TestContext.Current.CancellationToken);
        (await ((IReadModelConditionalEraser)harness.Store).TryEraseAsync(Store, Key, leased.ETag!, TestContext.Current.CancellationToken))
            .ShouldBeTrue();
        release.SetResult();

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(() => recovering);

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.RecordMissing);
        harness.Record.ShouldBeNull();
    }

    [Fact]
    public async Task SupersededWorkerCannotClearANewerLease() {
        CheckpointedProjectionHarness harness = await GappedAsync(through: 4, pending: [4]);
        (TaskCompletionSource entered, TaskCompletionSource release) = BlockFirstRead(harness);
        Task<CheckpointedProjectionResult<CheckpointedTestState>> stale = RecoverAsync(harness, "worker-a");
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Worker B supersedes the expired lease while A is still replaying.
        harness.Clock.UtcNow += TimeSpan.FromSeconds(31);
        CheckpointedProjectionRecord<CheckpointedTestState> current = harness.Record.ShouldNotBeNull();
        CheckpointedProjectionRecord<CheckpointedTestState> superseded = current with {
            Lease = new CheckpointedProjectionLease("worker-b", current.LastFencingToken + 1, current.Version + 1, harness.Clock.UtcNow.AddSeconds(30)),
            LastFencingToken = current.LastFencingToken + 1,
            Version = current.Version + 1,
        };
        harness.Store.SeedRaw(Store, Key, superseded);
        release.SetResult();

        CheckpointedProjectionUnavailableException lost = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(() => stale);

        lost.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.RecoveryLeaseLost);
        CheckpointedProjectionRecord<CheckpointedTestState> after = harness.Record.ShouldNotBeNull();
        after.Health.ShouldBe(CheckpointedProjectionHealth.Gapped);
        after.Lease.ShouldBe(superseded.Lease);
        after.Version.ShouldBe(superseded.Version);
    }

    [Fact]
    public async Task ExpiredWorkerCannotOverwriteNewerHealthyState() {
        CheckpointedProjectionHarness harness = await GappedAsync(through: 4, pending: [4]);
        (TaskCompletionSource entered, TaskCompletionSource release) = BlockFirstRead(harness);
        Task<CheckpointedProjectionResult<CheckpointedTestState>> stale = RecoverAsync(harness, "worker-a");
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        harness.Clock.UtcNow += TimeSpan.FromSeconds(31);
        (await RecoverAsync(harness, "worker-b")).Outcome.ShouldBe(CheckpointedProjectionOutcome.Recovered);
        harness.Gateway.Append(5);
        (await harness.ApplyAsync(5)).Outcome.ShouldBe(CheckpointedProjectionOutcome.Applied);
        CheckpointedProjectionRecord<CheckpointedTestState> newer = harness.Record.ShouldNotBeNull();
        release.SetResult();

        CheckpointedProjectionResult<CheckpointedTestState> result = await stale;

        result.Outcome.ShouldBe(CheckpointedProjectionOutcome.Recovered);
        CheckpointedProjectionRecord<CheckpointedTestState> after = harness.Record.ShouldNotBeNull();
        after.Version.ShouldBe(newer.Version, "the expired worker never wrote");
        after.LastContiguousSequence.ShouldBe(5);
        after.State.ShouldNotBeNull().Sequences.ShouldBe([1L, 2L, 3L, 4L, 5L]);
    }

    [Fact]
    public async Task ConcurrentHorizonExtensionIsReplayedBeforeHealthy() {
        CheckpointedProjectionHarness harness = await GappedAsync(through: 4, pending: [4]);
        harness.Gateway.Append(5);
        harness.Gateway.Append(6);
        CheckpointedProjectionUnavailableException? concurrent = null;
        int reads = 0;
        harness.Gateway.BeforeRead = async (_, _) => {
            if (Interlocked.Increment(ref reads) == 1) {
                // While A replays 2..4, another replica receives 6 and extends the horizon.
                concurrent = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
                    () => harness.ApplyAsync(6, workerId: "replica-b"));
            }
        };

        CheckpointedProjectionResult<CheckpointedTestState> result = await RecoverAsync(harness, "worker-a");

        concurrent.ShouldNotBeNull().Reason.ShouldBe(CheckpointedProjectionUnavailableReason.RecoveryLeaseHeld);
        result.Outcome.ShouldBe(CheckpointedProjectionOutcome.Recovered);
        CheckpointedProjectionRecord<CheckpointedTestState> record = harness.Record.ShouldNotBeNull();
        record.LastContiguousSequence.ShouldBe(6);
        record.State.ShouldNotBeNull().Sequences.ShouldBe([1L, 2L, 3L, 4L, 5L, 6L]);
        record.Pending.ShouldBeEmpty();

        // The redelivery of 6 that was left unacknowledged is now an identical accepted duplicate.
        harness.Gateway.BeforeRead = null;
        (await harness.ApplyAsync(6, workerId: "replica-b")).Outcome.ShouldBe(CheckpointedProjectionOutcome.Duplicate);
    }

    [Fact]
    public async Task RecoveryOfAHealthyRecordWritesNothing() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(2);
        long version = harness.Record.ShouldNotBeNull().Version;

        CheckpointedProjectionResult<CheckpointedTestState> result = await RecoverAsync(harness, "worker-a");

        result.Outcome.ShouldBe(CheckpointedProjectionOutcome.Recovered);
        harness.Record.ShouldNotBeNull().Version.ShouldBe(version);
        harness.Gateway.Requests.ShouldBeEmpty();
    }

    private static async Task<CheckpointedProjectionHarness> GappedAsync(long through, long[] pending) {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);
        harness.Gateway.AppendRange(2, through);
        foreach (long sequence in pending) {
            _ = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
                () => harness.ApplyAsync(sequence, withHistory: false));
        }

        CheckpointedProjectionRecord<CheckpointedTestState> record = harness.Record.ShouldNotBeNull();
        record.Health.ShouldBe(CheckpointedProjectionHealth.Gapped);
        record.RecoveryThrough.ShouldBe(through);
        return harness;
    }

    private static (TaskCompletionSource Entered, TaskCompletionSource Release) BlockFirstRead(CheckpointedProjectionHarness harness) {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        harness.Gateway.BeforeRead = async (_, cancellationToken) => {
            if (Interlocked.Increment(ref reads) == 1) {
                entered.TrySetResult();
                await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        };
        return (entered, release);
    }

    private static Task<CheckpointedProjectionResult<CheckpointedTestState>> RecoverAsync(CheckpointedProjectionHarness harness, string workerId)
        => harness.Worker(workerId).RecoverAsync(Store, Key, CheckpointedProjectionHarness.Stream, TestContext.Current.CancellationToken);
}
