using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Projections.Checkpointed;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections.Checkpointed;

/// <summary>
/// AD-6 delivery matrix for the generic checkpointed projection writer over the real-ETag in-memory store.
/// </summary>
public sealed class CheckpointedProjectionWriterTests {
    private const string Store = CheckpointedProjectionHarness.StoreName;
    private const string Key = CheckpointedProjectionHarness.Key;

    [Fact]
    public async Task ExactNextDeliveryCommitsTransitionAndCheckpointInOneRecord() {
        var harness = new CheckpointedProjectionHarness();

        CheckpointedProjectionResult<CheckpointedTestState> first = await harness.ApplyAsync(1);
        CheckpointedProjectionResult<CheckpointedTestState> second = await harness.ApplyAsync(2);

        first.Outcome.ShouldBe(CheckpointedProjectionOutcome.Applied);
        second.Outcome.ShouldBe(CheckpointedProjectionOutcome.Applied);
        CheckpointedProjectionRecord<CheckpointedTestState> record = harness.Record.ShouldNotBeNull();
        record.Health.ShouldBe(CheckpointedProjectionHealth.Healthy);
        record.LastContiguousSequence.ShouldBe(2);
        record.LastAccepted.ShouldBe(CheckpointedProjectionHarness.Delivery(2).Evidence);
        record.State.ShouldNotBeNull().Sequences.ShouldBe([1L, 2L]);
        record.Version.ShouldBe(2);
        record.IsWellFormed().ShouldBeTrue();
        harness.Gateway.Requests.ShouldBeEmpty("exact-next deliveries never read history");
    }

    [Fact]
    public async Task IdenticalAcceptedDuplicateIsANoOp() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(2);
        CheckpointedProjectionRecord<CheckpointedTestState> before = harness.Record.ShouldNotBeNull();

        CheckpointedProjectionResult<CheckpointedTestState> duplicate = await harness.ApplyAsync(2, workerId: "replica-b");

        duplicate.Outcome.ShouldBe(CheckpointedProjectionOutcome.Duplicate);
        harness.Record.ShouldNotBeNull().Version.ShouldBe(before.Version);
        harness.Transition.Applications.ShouldBe(2);
    }

    [Theory]
    [InlineData("tampered-payload", null)]
    [InlineData(null, "another-message")]
    public async Task EqualSequenceMismatchPersistsTerminalCorrupt(string? payload, string? messageId) {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(2);

        CheckpointedProjectionResult<CheckpointedTestState> result = await harness.ApplyAsync(2, payload, messageId);

        result.Outcome.ShouldBe(CheckpointedProjectionOutcome.Corrupt);
        CheckpointedProjectionRecord<CheckpointedTestState> record = harness.Record.ShouldNotBeNull();
        record.Health.ShouldBe(CheckpointedProjectionHealth.Corrupt);
        record.CorruptionReason.ShouldBe(CheckpointedProjectionCorruptionReason.AcceptedIdentityMismatch);
        record.CorruptSequence.ShouldBe(2);
        record.LastContiguousSequence.ShouldBe(2);
    }

    [Fact]
    public async Task CorruptIsTerminalForLaterDeliveries() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);
        (await harness.ApplyAsync(1, "tampered")).Outcome.ShouldBe(CheckpointedProjectionOutcome.Corrupt);
        long version = harness.Record.ShouldNotBeNull().Version;
        harness.Gateway.Append(2);

        CheckpointedProjectionResult<CheckpointedTestState> later = await harness.ApplyAsync(2);

        later.Outcome.ShouldBe(CheckpointedProjectionOutcome.Corrupt);
        harness.Record.ShouldNotBeNull().Version.ShouldBe(version);
        harness.Record!.LastContiguousSequence.ShouldBe(1);
    }

    [Fact]
    public async Task OlderDeliveryMatchingAuthoritativeHistoryIsDuplicateAndNeverReapplied() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(3);
        long version = harness.Record.ShouldNotBeNull().Version;

        CheckpointedProjectionResult<CheckpointedTestState> result = await harness.ApplyAsync(1, workerId: "replica-b");

        result.Outcome.ShouldBe(CheckpointedProjectionOutcome.Duplicate);
        harness.Record.ShouldNotBeNull().Version.ShouldBe(version);
        harness.Transition.Applications.ShouldBe(3);
        StreamReadRequestShouldTarget(harness, fromExclusive: 0, toInclusive: 1);
    }

    [Fact]
    public async Task OlderDeliveryContradictingAuthoritativeHistoryPersistsCorrupt() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(3);

        CheckpointedProjectionResult<CheckpointedTestState> result = await harness.ApplyAsync(1, "forged");

        result.Outcome.ShouldBe(CheckpointedProjectionOutcome.Corrupt);
        harness.Record.ShouldNotBeNull().CorruptionReason.ShouldBe(CheckpointedProjectionCorruptionReason.HistoryIdentityMismatch);
    }

    [Fact]
    public async Task OlderDeliveryMissingFromAuthoritativeHistoryPersistsCorrupt() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(3);
        harness.Gateway.Remove(2);

        CheckpointedProjectionResult<CheckpointedTestState> result = await harness.ApplyAsync(2);

        result.Outcome.ShouldBe(CheckpointedProjectionOutcome.Corrupt);
        harness.Record.ShouldNotBeNull().CorruptionReason.ShouldBe(CheckpointedProjectionCorruptionReason.HistoryMissing);
    }

    [Fact]
    public async Task OlderDeliveryWithUnavailableHistoryIsRetryableAndChangesNothing() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(3);
        long version = harness.Record.ShouldNotBeNull().Version;
        harness.Gateway.Unavailable = true;

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => harness.ApplyAsync(1));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
        harness.Record.ShouldNotBeNull().Version.ShouldBe(version);
        harness.Record!.Health.ShouldBe(CheckpointedProjectionHealth.Healthy);
    }

    [Fact]
    public async Task FutureDeliveryPersistsGapBeforeRecoveryAndRedeliveryRecovers() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);
        harness.Gateway.AppendRange(2, 4);
        harness.Gateway.Unavailable = true;

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => harness.ApplyAsync(4));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
        CheckpointedProjectionRecord<CheckpointedTestState> gapped = harness.Record.ShouldNotBeNull();
        gapped.Health.ShouldBe(CheckpointedProjectionHealth.Gapped);
        gapped.Expected.ShouldBe(2);
        gapped.RecoveryThrough.ShouldBe(4);
        gapped.Pending.ShouldHaveSingleItem().ShouldBe(new CheckpointedProjectionPendingEvidence(4, CheckpointedProjectionHarness.Delivery(4).Evidence));
        gapped.Lease.ShouldBeNull("a failed recovery releases its lease");
        gapped.LastContiguousSequence.ShouldBe(1);

        harness.Gateway.Unavailable = false;
        CheckpointedProjectionResult<CheckpointedTestState> redelivered = await harness.ApplyAsync(4, workerId: "replica-b");

        redelivered.Outcome.ShouldBe(CheckpointedProjectionOutcome.Recovered);
        CheckpointedProjectionRecord<CheckpointedTestState> healthy = harness.Record.ShouldNotBeNull();
        healthy.Health.ShouldBe(CheckpointedProjectionHealth.Healthy);
        healthy.LastContiguousSequence.ShouldBe(4);
        healthy.State.ShouldNotBeNull().Sequences.ShouldBe([1L, 2L, 3L, 4L]);
        healthy.Pending.ShouldBeEmpty();
        healthy.IsWellFormed().ShouldBeTrue();
    }

    [Fact]
    public async Task ThreeInterleavedFutureDeliveriesKeepImmutableExpectedAndMonotonicHorizon() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);
        harness.Gateway.AppendRange(2, 5);
        long[] arrivals = [5, 3, 4, 3];
        long?[] horizons = [5, 5, 5, 5];

        for (int index = 0; index < arrivals.Length; index++) {
            _ = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
                () => harness.ApplyAsync(arrivals[index], workerId: $"replica-{index % 2}", withHistory: false));
            CheckpointedProjectionRecord<CheckpointedTestState> record = harness.Record.ShouldNotBeNull();
            record.Health.ShouldBe(CheckpointedProjectionHealth.Gapped);
            record.Expected.ShouldBe(2);
            record.RecoveryThrough.ShouldBe(horizons[index]);
        }

        CheckpointedProjectionRecord<CheckpointedTestState> merged = harness.Record.ShouldNotBeNull();
        merged.Pending.Select(static p => p.Sequence).ShouldBe([3L, 4L, 5L]);
        merged.Version.ShouldBe(4, "the identical repeated evidence for sequence 3 was a no-op");

        CheckpointedProjectionResult<CheckpointedTestState> recovered = await harness.ApplyAsync(3);

        recovered.Outcome.ShouldBe(CheckpointedProjectionOutcome.Recovered);
        harness.Record.ShouldNotBeNull().LastContiguousSequence.ShouldBe(5);
        harness.Record!.State.ShouldNotBeNull().Sequences.ShouldBe([1L, 2L, 3L, 4L, 5L]);
    }

    [Fact]
    public async Task ConflictingPendingEvidencePersistsCorrupt() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);
        _ = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(() => harness.ApplyAsync(3, withHistory: false));

        CheckpointedProjectionResult<CheckpointedTestState> conflict = await harness.ApplyAsync(3, "different", withHistory: false);

        conflict.Outcome.ShouldBe(CheckpointedProjectionOutcome.Corrupt);
        harness.Record.ShouldNotBeNull().CorruptionReason.ShouldBe(CheckpointedProjectionCorruptionReason.PendingEvidenceConflict);
    }

    [Fact]
    public async Task ConcurrentReverseSaveCannotRegressTheCheckpoint() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);
        harness.Gateway.AppendRange(2, 3);
        CheckpointedProjectionResult<CheckpointedTestState>? competing = null;
        harness.Store.ConcurrentWriteBeforeTrySave = () => {
            harness.Store.ConcurrentWriteBeforeTrySave = null;

            // The later delivery reaches another replica and commits first (gap, then fenced recovery).
            competing = harness.ApplyAsync(3, workerId: "replica-b").GetAwaiter().GetResult();
        };

        CheckpointedProjectionResult<CheckpointedTestState> earlier = await harness.ApplyAsync(2, workerId: "replica-a");

        competing.ShouldNotBeNull().Outcome.ShouldBe(CheckpointedProjectionOutcome.Recovered);
        earlier.Outcome.ShouldBe(CheckpointedProjectionOutcome.Duplicate, "the stale save lost its compare-and-swap and was never reapplied");
        CheckpointedProjectionRecord<CheckpointedTestState> record = harness.Record.ShouldNotBeNull();
        record.LastContiguousSequence.ShouldBe(3);
        record.State.ShouldNotBeNull().Sequences.ShouldBe([1L, 2L, 3L]);
    }

    [Fact]
    public async Task RetryExhaustionIsRetryableAfterThreeTotalAttempts() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);
        int attempts = 0;
        harness.Store.ConcurrentWriteBeforeTrySave = () => {
            attempts++;
            CheckpointedProjectionRecord<CheckpointedTestState> current = harness.Record!;
            harness.Store.SeedRaw(Store, Key, current with { Version = current.Version + 100 });
        };

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => harness.ApplyAsync(2));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.ConcurrencyExhausted);
        attempts.ShouldBe(ReadModelWritePolicy.DefaultMaxAttempts);
        harness.Record.ShouldNotBeNull().LastContiguousSequence.ShouldBe(1);
    }

    [Fact]
    public async Task ProviderFailureIsRetryableUnavailable() {
        IReadModelStore store = Substitute.For<IReadModelStore>();
        _ = store.GetAsync<CheckpointedProjectionRecord<CheckpointedTestState>>(Store, Key, Arg.Any<CancellationToken>())
            .Returns<ReadModelEntry<CheckpointedProjectionRecord<CheckpointedTestState>>>(_ => throw new InvalidOperationException("synthetic provider outage"));
        var writer = new CheckpointedProjectionWriter<CheckpointedTestState>(store, new CheckpointedTestTransition());

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => writer.ApplyAsync(Store, Key, CheckpointedProjectionHarness.Delivery(1), TestContext.Current.CancellationToken));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.ProviderUnavailable);
        _ = await store.DidNotReceiveWithAnyArgs().TrySaveAsync<CheckpointedProjectionRecord<CheckpointedTestState>>(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task MalformedRecordIsRetryableUnavailableAndIsNeverOverwritten() {
        var harness = new CheckpointedProjectionHarness();
        var malformed = new CheckpointedProjectionRecord<CheckpointedTestState> { Health = (CheckpointedProjectionHealth)0, LastContiguousSequence = 4 };
        harness.Store.SeedRaw(Store, Key, malformed);

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => harness.ApplyAsync(1));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.MalformedRecord);
        harness.Record.ShouldNotBeNull().Health.ShouldBe((CheckpointedProjectionHealth)0);
    }

    [Fact]
    public async Task ReadNeverStartsRecovery() {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);
        harness.Gateway.AppendRange(2, 3);
        _ = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(() => harness.ApplyAsync(3, withHistory: false));
        int reads = harness.Gateway.Requests.Count;

        CheckpointedProjectionRecord<CheckpointedTestState>? observed = await harness.Writer().ReadAsync(Store, Key, TestContext.Current.CancellationToken);

        observed.ShouldNotBeNull().Health.ShouldBe(CheckpointedProjectionHealth.Gapped);
        observed.Lease.ShouldBeNull();
        harness.Gateway.Requests.Count.ShouldBe(reads);
        harness.Record.ShouldNotBeNull().Version.ShouldBe(observed.Version);
    }

    [Fact]
    public async Task TwoReplicasAndARestartedHostConvergeOnOnePersistedRecord() {
        var harness = new CheckpointedProjectionHarness();
        harness.Gateway.AppendRange(1, 6);
        (long Sequence, string Replica)[] schedule = [(1, "a"), (3, "b"), (2, "a"), (5, "b"), (4, "a"), (6, "b"), (2, "b"), (6, "a")];

        foreach ((long sequence, string replica) in schedule) {
            _ = await harness.ApplyAsync(sequence, workerId: "replica-" + replica);
        }

        CheckpointedProjectionRecord<CheckpointedTestState> record = harness.Record.ShouldNotBeNull();
        record.Health.ShouldBe(CheckpointedProjectionHealth.Healthy);
        record.LastContiguousSequence.ShouldBe(6);
        record.State.ShouldNotBeNull().Sequences.ShouldBe([1L, 2L, 3L, 4L, 5L, 6L]);

        // A restarted host and a second replica read the same persisted decision without redelivery.
        CheckpointedProjectionRecord<CheckpointedTestState>? restarted = await harness.Writer("replica-restarted").ReadAsync(Store, Key, TestContext.Current.CancellationToken);
        CheckpointedProjectionRecord<CheckpointedTestState>? second = await harness.Writer("replica-b").ReadAsync(Store, Key, TestContext.Current.CancellationToken);
        restarted.ShouldNotBeNull().Version.ShouldBe(record.Version);
        second.ShouldNotBeNull().LastAccepted.ShouldBe(record.LastAccepted);
        restarted.State!.Values.ShouldBe(second.State!.Values);
    }

    private static void StreamReadRequestShouldTarget(CheckpointedProjectionHarness harness, long fromExclusive, long toInclusive) {
        Contracts.Streams.StreamReadRequest request = harness.Gateway.Requests.ShouldHaveSingleItem();
        request.Tenant.ShouldBe(CheckpointedHistoryGateway.Tenant);
        request.Domain.ShouldBe(CheckpointedHistoryGateway.Domain);
        request.AggregateId.ShouldBe(CheckpointedHistoryGateway.Aggregate);
        request.FromSequence.ShouldBe(fromExclusive);
        request.ToSequence.ShouldBe(toInclusive);
    }
}
