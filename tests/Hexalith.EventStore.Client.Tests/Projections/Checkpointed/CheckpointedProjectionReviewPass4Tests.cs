using System.Text.Json.Nodes;

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
    [InlineData("pending-null")]
    [InlineData("last-accepted-at-zero")]
    [InlineData("last-accepted-without-message")]
    [InlineData("last-accepted-without-type")]
    [InlineData("last-accepted-without-fingerprint")]
    [InlineData("negative-checkpoint")]
    [InlineData("negative-version")]
    [InlineData("negative-fence")]
    [InlineData("missing-last-accepted")]
    [InlineData("healthy-expected")]
    [InlineData("healthy-recovery-through")]
    [InlineData("healthy-corruption-reason")]
    [InlineData("healthy-pending")]
    [InlineData("healthy-lease")]
    [InlineData("missing-expected")]
    [InlineData("missing-recovery-through")]
    [InlineData("recovery-through-before-expected")]
    [InlineData("wrong-expected")]
    [InlineData("gapped-corruption-reason")]
    [InlineData("empty-pending")]
    [InlineData("null-pending-entry")]
    [InlineData("null-pending-evidence")]
    [InlineData("pending-without-message")]
    [InlineData("pending-without-type")]
    [InlineData("pending-without-fingerprint")]
    [InlineData("last-pending-before-recovery-through")]
    [InlineData("unsorted-pending")]
    [InlineData("out-of-range-pending")]
    [InlineData("corrupt-lease")]
    [InlineData("corrupt-without-reason")]
    [InlineData("lease-without-worker")]
    [InlineData("zero-lease-token")]
    [InlineData("negative-lease-token")]
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
        CheckpointedProjectionEvidence accepted = CheckpointedProjectionHarness.Delivery(1).Evidence;
        CheckpointedProjectionRecord<CheckpointedTestState> healthy = empty with { LastContiguousSequence = 1, LastAccepted = accepted };
        CheckpointedProjectionRecord<CheckpointedTestState> malformed = defect switch
        {
            "pending-null" => empty with { Pending = null! },
            "last-accepted-at-zero" => empty with { LastAccepted = accepted },
            "last-accepted-without-message" => healthy with { LastAccepted = accepted with { MessageId = " " } },
            "last-accepted-without-type" => healthy with { LastAccepted = accepted with { EventType = " " } },
            "last-accepted-without-fingerprint" => healthy with { LastAccepted = accepted with { PayloadFingerprint = " " } },
            "negative-checkpoint" => healthy with { LastContiguousSequence = -1 },
            "negative-version" => empty with { Version = -1 },
            "negative-fence" => empty with { LastFencingToken = -1 },
            "missing-last-accepted" => empty with { LastContiguousSequence = 1 },
            "healthy-expected" => empty with { Expected = 1 },
            "healthy-recovery-through" => empty with { RecoveryThrough = 1 },
            "healthy-corruption-reason" => empty with { CorruptionReason = CheckpointedProjectionCorruptionReason.HistoryMissing },
            "healthy-pending" => empty with { Pending = [pending2] },
            "healthy-lease" => empty with { Lease = lease, LastFencingToken = 1 },
            "missing-expected" => gapped with { Expected = null },
            "missing-recovery-through" => gapped with { RecoveryThrough = null },
            "recovery-through-before-expected" => gapped with { RecoveryThrough = 0 },
            "wrong-expected" => gapped with { Expected = 2 },
            "gapped-corruption-reason" => gapped with { CorruptionReason = CheckpointedProjectionCorruptionReason.HistoryMissing },
            "empty-pending" => gapped with { Pending = [] },
            "null-pending-entry" => gapped with { Pending = [null!] },
            "null-pending-evidence" => gapped with { Pending = [pending2 with { Evidence = null! }] },
            "pending-without-message" => gapped with { Pending = [pending2 with { Evidence = pending2.Evidence with { MessageId = " " } }] },
            "pending-without-type" => gapped with { Pending = [pending2 with { Evidence = pending2.Evidence with { EventType = " " } }] },
            "pending-without-fingerprint" => gapped with { Pending = [pending2 with { Evidence = pending2.Evidence with { PayloadFingerprint = " " } }] },
            "last-pending-before-recovery-through" => gapped with { RecoveryThrough = 3 },
            "unsorted-pending" => gapped with { Pending = [pending2, new(1, CheckpointedProjectionHarness.Delivery(1).Evidence)] },
            "out-of-range-pending" => gapped with { Pending = [new(3, CheckpointedProjectionHarness.Delivery(3).Evidence)] },
            "corrupt-lease" => corrupt with { Lease = lease, LastFencingToken = 1 },
            "corrupt-without-reason" => corrupt with { CorruptionReason = null },
            "lease-without-worker" => gapped with { Lease = lease with { WorkerId = " " }, LastFencingToken = 1 },
            "zero-lease-token" => gapped with { Lease = lease with { FencingToken = 0 } },
            "negative-lease-token" => gapped with { Lease = lease with { FencingToken = -1 } },
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

    [Fact]
    public async Task StoredRecordThatCannotDeserializeRejectsApplyAndReadAsMalformed()
    {
        var harness = new CheckpointedProjectionHarness();
        harness.Store.SeedRaw(Store, Key, new JsonObject { ["schemaVersion"] = 1, ["health"] = (int)CheckpointedProjectionHealth.Healthy, ["pending"] = 5 });
        byte[] before = harness.Store.SnapshotRaw(Store, Key).ShouldNotBeNull();

        CheckpointedProjectionUnavailableException apply = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => harness.ApplyAsync(1));
        CheckpointedProjectionUnavailableException read = await Should.ThrowAsync<CheckpointedProjectionUnavailableException>(
            () => harness.Writer().ReadAsync(Store, Key, TestContext.Current.CancellationToken));

        apply.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.MalformedRecord);
        read.Reason.ShouldBe(CheckpointedProjectionUnavailableReason.MalformedRecord);
        harness.Store.SnapshotRaw(Store, Key).ShouldBe(before);
        harness.Gateway.Requests.ShouldBeEmpty();
    }
}
