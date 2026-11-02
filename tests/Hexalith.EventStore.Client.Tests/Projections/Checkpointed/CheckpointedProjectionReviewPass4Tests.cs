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

    [Theory]
    [InlineData("tenant")]
    [InlineData("domain")]
    [InlineData("aggregate")]
    [InlineData("from")]
    [InlineData("to")]
    [InlineData("sequence")]
    [InlineData("message")]
    [InlineData("type")]
    [InlineData("format")]
    public async Task MalformedHistoryPageStaysRetryableAndNeverCorrupts(string defect)
    {
        var harness = new CheckpointedProjectionHarness();
        await harness.SeedHealthyThroughAsync(1);
        harness.Gateway.AppendRange(2, 3);
        harness.Gateway.PageTransform = page => defect switch
        {
            "tenant" => page with { Tenant = "other" },
            "domain" => page with { Domain = "other" },
            "aggregate" => page with { AggregateId = "other" },
            "from" => page with { Metadata = page.Metadata with { FromSequence = 99 } },
            "to" => page with { Metadata = page.Metadata with { ToSequence = 99 } },
            "sequence" => page with { Events = [page.Events[0] with { SequenceNumber = 4 }, .. page.Events.Skip(1)] },
            "message" => page with { Events = [page.Events[0] with { MessageId = " " }, .. page.Events.Skip(1)] },
            "type" => page with { Events = [page.Events[0] with { EventTypeName = " " }, .. page.Events.Skip(1)] },
            "format" => page with { Events = [page.Events[0] with { SerializationFormat = " " }, .. page.Events.Skip(1)] },
            _ => throw new ArgumentOutOfRangeException(nameof(defect)),
        };

        CheckpointedProjectionUnavailableException failure = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => harness.ApplyAsync(3));

        failure.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.HistoryUnavailable);
        CheckpointedProjectionRecord<CheckpointedTestState> record = harness.Record.ShouldNotBeNull();
        record.Health.ShouldBe(CheckpointedProjectionHealth.Gapped);
        record.CorruptionReason.ShouldBeNull();
    }

    [Theory]
    [InlineData("missing-last-accepted")]
    [InlineData("healthy-pending")]
    [InlineData("healthy-lease")]
    [InlineData("wrong-expected")]
    [InlineData("unsorted-pending")]
    [InlineData("out-of-range-pending")]
    [InlineData("corrupt-lease")]
    [InlineData("corrupt-without-reason")]
    [InlineData("lease-token-above-fence")]
    public async Task EveryMalformedRecordRuleRejectsApplyAndRead(string defect)
    {
        var harness = new CheckpointedProjectionHarness();
        CheckpointedProjectionRecord<CheckpointedTestState> empty = CheckpointedProjectionRecord<CheckpointedTestState>.CreateEmpty();
        CheckpointedProjectionPendingEvidence pending2 = new(2, CheckpointedProjectionHarness.Delivery(2).Evidence);
        CheckpointedProjectionRecord<CheckpointedTestState> gapped = empty with
        {
            Health = CheckpointedProjectionHealth.Gapped,
            Expected = 1,
            RecoveryThrough = 2,
            Pending = [pending2],
        };
        CheckpointedProjectionRecord<CheckpointedTestState> corrupt = empty with
        {
            Health = CheckpointedProjectionHealth.Corrupt,
            CorruptionReason = CheckpointedProjectionCorruptionReason.HistoryMissing,
        };
        CheckpointedProjectionLease lease = new("worker", 1, 1, harness.Clock.UtcNow.AddSeconds(30));
        CheckpointedProjectionRecord<CheckpointedTestState> malformed = defect switch
        {
            "missing-last-accepted" => empty with { LastContiguousSequence = 1 },
            "healthy-pending" => empty with { Pending = [pending2] },
            "healthy-lease" => empty with { Lease = lease, LastFencingToken = 1 },
            "wrong-expected" => gapped with { Expected = 2 },
            "unsorted-pending" => gapped with { Pending = [pending2, new(1, CheckpointedProjectionHarness.Delivery(1).Evidence)] },
            "out-of-range-pending" => gapped with { Pending = [new(3, CheckpointedProjectionHarness.Delivery(3).Evidence)] },
            "corrupt-lease" => corrupt with { Lease = lease, LastFencingToken = 1 },
            "corrupt-without-reason" => corrupt with { CorruptionReason = null },
            "lease-token-above-fence" => gapped with { Lease = lease },
            _ => throw new ArgumentOutOfRangeException(nameof(defect)),
        };
        malformed.SchemaVersion.ShouldBe(1);
        malformed.IsWellFormed().ShouldBeFalse();
        harness.Store.SeedRaw(CheckpointedProjectionHarness.StoreName, CheckpointedProjectionHarness.Key, malformed);

        CheckpointedProjectionUnavailableException apply = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => harness.ApplyAsync(1));
        CheckpointedProjectionUnavailableException read = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => harness.Writer().ReadAsync(CheckpointedProjectionHarness.StoreName, CheckpointedProjectionHarness.Key, TestContext.Current.CancellationToken));

        apply.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.MalformedRecord);
        read.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.MalformedRecord);
    }
}
