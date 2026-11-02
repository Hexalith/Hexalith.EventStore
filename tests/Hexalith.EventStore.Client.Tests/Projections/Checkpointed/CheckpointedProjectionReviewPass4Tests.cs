using Hexalith.EventStore.Client.Projections.Checkpointed;
using Hexalith.EventStore.Contracts.Streams;

using Shouldly;

namespace Hexalith.EventStore.Client.Tests.Projections.Checkpointed;

/// <summary>Proves bounded recovery and ambiguous history remain retryable without corrupting a tenant record.</summary>
public sealed class CheckpointedProjectionReviewPass4Tests
{
    private const string Store = CheckpointedProjectionHarness.StoreName;
    private const string Key = CheckpointedProjectionHarness.Key;

    [Fact]
    public async Task ThrowingTransitionKeepsTheRecordGappedAndReleasesTheLease()
    {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);
        harness.Gateway.AppendRange(2, 3);
        _ = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(() => harness.ApplyAsync(3, withHistory: false));
        var worker = new CheckpointedProjectionRecoveryWorker<CheckpointedTestState>(
            harness.Store,
            new CheckpointedThrowingTransition(),
            new CheckpointedProjectionHistory(harness.Gateway),
            CheckpointedProjectionHarness.Options("failing-transition"),
            harness.Clock);

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => worker.RecoverAsync(Store, Key, CheckpointedProjectionHarness.Stream, TestContext.Current.CancellationToken));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.TransitionFailed);
        CheckpointedProjectionRecord<CheckpointedTestState> record = harness.Record.ShouldNotBeNull();
        record.Health.ShouldBe(CheckpointedProjectionHealth.Gapped);
        record.CorruptionReason.ShouldBeNull();
        record.Lease.ShouldBeNull();
        record.LastContiguousSequence.ShouldBe(1);
        record.Expected.ShouldBe(2);
        record.RecoveryThrough.ShouldBe(3);
    }

    [Fact]
    public async Task HistoryPageBudgetExhaustionKeepsTheRecordGapped()
    {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);
        harness.Gateway.AppendRange(2, 4);
        _ = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(() => harness.ApplyAsync(4, withHistory: false));
        CheckpointedProjectionOptions options = CheckpointedProjectionHarness.Options("one-page-worker");
        options.HistoryPageSize = 1;
        options.MaxHistoryPages = 1;
        var worker = new CheckpointedProjectionRecoveryWorker<CheckpointedTestState>(
            harness.Store, harness.Transition, new CheckpointedProjectionHistory(harness.Gateway, options), options, harness.Clock);

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => worker.RecoverAsync(Store, Key, CheckpointedProjectionHarness.Stream, TestContext.Current.CancellationToken));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
        harness.Gateway.Requests.Count.ShouldBe(1);
        CheckpointedProjectionRecord<CheckpointedTestState> record = harness.Record.ShouldNotBeNull();
        record.Health.ShouldBe(CheckpointedProjectionHealth.Gapped);
        record.CorruptionReason.ShouldBeNull();
        record.Lease.ShouldBeNull();
    }

    [Fact]
    public async Task HorizonExtensionBudgetExhaustionReleasesTheLease()
    {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);
        harness.Gateway.AppendRange(2, 68);
        _ = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(() => harness.ApplyAsync(3, withHistory: false));
        long next = 4;
        harness.Gateway.BeforeRead = async (_, _) =>
        {
            if (next <= 68)
            {
                long sequence = next++;
                CheckpointedProjectionUnavailableException unavailable = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
                    () => harness.ApplyAsync(sequence, withHistory: false));
                unavailable.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
            }
        };

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => harness.Worker("extending-worker").RecoverAsync(Store, Key, CheckpointedProjectionHarness.Stream, TestContext.Current.CancellationToken));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.ConcurrencyExhausted);
        next.ShouldBe(69);
        CheckpointedProjectionRecord<CheckpointedTestState> record = harness.Record.ShouldNotBeNull();
        record.Health.ShouldBe(CheckpointedProjectionHealth.Gapped);
        record.RecoveryThrough.ShouldBe(68);
        record.CorruptionReason.ShouldBeNull();
        record.Lease.ShouldBeNull();
    }

    [Fact]
    public async Task ConflictingEventsAtOneHistorySequenceAreUnavailable()
    {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);
        harness.Gateway.AppendRange(2, 3);
        harness.Gateway.DuplicateFirstEventWithConflictingPayload = true;

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => harness.ApplyAsync(3));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
        CheckpointedProjectionRecord<CheckpointedTestState> record = harness.Record.ShouldNotBeNull();
        record.Health.ShouldBe(CheckpointedProjectionHealth.Gapped);
        record.CorruptionReason.ShouldBeNull();
        record.Lease.ShouldBeNull();
    }

    [Theory]
    [InlineData("xml", 1)]
    [InlineData("json", 2)]
    public async Task EqualEvidenceWithConflictingReplayMetadataIsUnavailable(string format, int version)
    {
        var harness = new CheckpointedProjectionHarness();
        harness.Gateway.Append(1);
        harness.Gateway.DuplicateFirstEventTransform = original => original with {
            SerializationFormat = format,
            PayloadVersion = version,
        };

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => new CheckpointedProjectionHistory(harness.Gateway)
                .ReadRangeAsync(CheckpointedProjectionHarness.Stream, 1, 1, TestContext.Current.CancellationToken));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
    }

    [Fact]
    public void PublicHistoryConversionRejectsNullPayload()
    {
        var streamEvent = new StreamReadEvent(
            1, CheckpointedHistoryGateway.EventType, null!, "json", 1, "message-0001", null, null,
            DateTimeOffset.UnixEpoch, null);

        CheckpointedProjectionUnavailableException failure = Should.Throw<CheckpointedProjectionUnavailableException>(
            () => CheckpointedProjectionEvent.FromHistory(CheckpointedProjectionHarness.Stream, streamEvent));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
    }
}
