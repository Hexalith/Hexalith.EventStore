#pragma warning disable CS8620 // Nullability mismatch in NSubstitute Returns() with nullable Dapr client methods

using System.Net;

using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Services;

public class DaprConsistencyCommandServiceTests {
    private const string StateStoreName = "statestore";

    private static DaprConsistencyCommandService CreateService(
        DaprClient? daprClient = null,
        IStreamQueryService? streamQueryService = null) {
        daprClient ??= Substitute.For<DaprClient>();
        streamQueryService ??= Substitute.For<IStreamQueryService>();
        IOptions<AdminServerOptions> options = Options.Create(new AdminServerOptions {
            StateStoreName = StateStoreName,
        });

        return new DaprConsistencyCommandService(
            daprClient,
            streamQueryService,
            options,
            NullLogger<DaprConsistencyCommandService>.Instance);
    }

    [Fact]
    public async Task TriggerCheck_StoresCheckRecord_WithPendingStatus() {
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();

        // Empty index — no active checks
        _ = daprClient.GetStateAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<string>?)null);

        // ETag-based index save
        _ = daprClient.GetStateAndETagAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (new List<string>(), string.Empty));

        _ = daprClient.TrySaveStateAsync(
                StateStoreName, "admin:consistency:index", Arg.Any<List<string>>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);

        AdminOperationResult result = await service.TriggerCheckAsync(
            "tenant-a", null, [ConsistencyCheckType.SequenceContinuity]);

        result.Success.ShouldBeTrue();
        result.OperationId.ShouldNotBeNullOrWhiteSpace();

        // Verify state was saved with the check ID key
        await daprClient.Received().SaveStateAsync(
            StateStoreName,
            Arg.Is<string>(k => k.StartsWith("admin:consistency:")),
            Arg.Any<ConsistencyCheckResult>(),
            metadata: Arg.Any<Dictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggerCheck_ReturnsConflict_WhenActiveCheckExists() {
        DaprClient daprClient = Substitute.For<DaprClient>();

        // Index with one active check
        List<string> index = ["active-check"];
        _ = daprClient.GetStateAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => index);

        ConsistencyCheckResult activeCheck = new(
            "active-check",
            ConsistencyCheckStatus.Running,
            "tenant-a",
            null,
            [ConsistencyCheckType.SequenceContinuity],
            DateTimeOffset.UtcNow.AddMinutes(-5),
            null,
            DateTimeOffset.UtcNow.AddMinutes(25), // Not timed out
            10,
            0,
            [],
            false,
            null);

        _ = daprClient.GetStateAsync<ConsistencyCheckResult>(
                StateStoreName, "admin:consistency:active-check", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => activeCheck);

        DaprConsistencyCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.TriggerCheckAsync(
            "tenant-a", null, [ConsistencyCheckType.SequenceContinuity]);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("Conflict");
    }

    [Fact]
    public async Task TriggerCheck_ReturnsOperationResult_WithCheckId() {
        DaprClient daprClient = Substitute.For<DaprClient>();

        _ = daprClient.GetStateAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<string>?)null);

        _ = daprClient.GetStateAndETagAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (new List<string>(), string.Empty));

        _ = daprClient.TrySaveStateAsync(
                StateStoreName, "admin:consistency:index", Arg.Any<List<string>>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);

        DaprConsistencyCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.TriggerCheckAsync(
            "tenant-a", "orders", [ConsistencyCheckType.MetadataConsistency]);

        result.Success.ShouldBeTrue();
        result.OperationId.Length.ShouldBe(26); // ULID is 26 chars
    }

    [Fact]
    public async Task TriggerCheck_AppendsToIndex_WithETag() {
        DaprClient daprClient = Substitute.For<DaprClient>();

        _ = daprClient.GetStateAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<string>?)null);

        List<string> existingIndex = ["old-check"];
        _ = daprClient.GetStateAndETagAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (existingIndex, "etag-123"));

        List<string>? savedIndex = null;
        _ = daprClient.TrySaveStateAsync(
                StateStoreName, "admin:consistency:index", Arg.Do<List<string>>(i => savedIndex = i), "etag-123", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);

        DaprConsistencyCommandService service = CreateService(daprClient);

        _ = await service.TriggerCheckAsync(
            "tenant-a", null, [ConsistencyCheckType.SequenceContinuity]);

        _ = savedIndex.ShouldNotBeNull();
        savedIndex.Count.ShouldBe(2); // new check + old-check
        savedIndex[1].ShouldBe("old-check"); // old check moved to position 1
    }

    [Fact]
    public async Task CancelCheck_UpdatesStatus_WhenRunning() {
        DaprClient daprClient = Substitute.For<DaprClient>();

        ConsistencyCheckResult runningCheck = new(
            "check-1",
            ConsistencyCheckStatus.Running,
            "tenant-a",
            null,
            [ConsistencyCheckType.SequenceContinuity],
            DateTimeOffset.UtcNow.AddMinutes(-5),
            null,
            DateTimeOffset.UtcNow.AddMinutes(25),
            10,
            0,
            [],
            false,
            null);

        _ = daprClient.GetStateAsync<ConsistencyCheckResult>(
                StateStoreName, "admin:consistency:check-1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => runningCheck);

        DaprConsistencyCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.CancelCheckAsync("check-1");

        result.Success.ShouldBeTrue();

        await daprClient.Received().SaveStateAsync(
            StateStoreName,
            "admin:consistency:check-1",
            Arg.Is<ConsistencyCheckResult>(r => r.Status == ConsistencyCheckStatus.Cancelled),
            metadata: Arg.Any<Dictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelCheck_ReturnsError_WhenCompleted() {
        DaprClient daprClient = Substitute.For<DaprClient>();

        ConsistencyCheckResult completedCheck = new(
            "check-1",
            ConsistencyCheckStatus.Completed,
            "tenant-a",
            null,
            [ConsistencyCheckType.SequenceContinuity],
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(30),
            50,
            2,
            [],
            false,
            null);

        _ = daprClient.GetStateAsync<ConsistencyCheckResult>(
                StateStoreName, "admin:consistency:check-1", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => completedCheck);

        DaprConsistencyCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.CancelCheckAsync("check-1");

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("InvalidOperation");
    }

    [Fact]
    public async Task CancelCheck_ReturnsNotFound_WhenMissing() {
        DaprClient daprClient = Substitute.For<DaprClient>();

        _ = daprClient.GetStateAsync<ConsistencyCheckResult>(
                StateStoreName, "admin:consistency:nonexistent", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (ConsistencyCheckResult?)null);

        DaprConsistencyCommandService service = CreateService(daprClient);

        AdminOperationResult result = await service.CancelCheckAsync("nonexistent");

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("NotFound");
    }

    // ===== DW12: Actor-state contract for sequence continuity & metadata checks =====
    // The check must use IStreamQueryService.GetStreamTimelineAsync, must not read raw
    // {tenant}:{domain}:{aggregateId}:events:* or :metadata DAPR state keys, and must
    // honor identity, paging, ordering, duplicates, cancellation, and query failures.

    private const string DaprEventsKeyFragment = ":events:";
    private const string DaprMetadataKeyFragment = ":metadata";

    private static StreamSummary CreateStream(
        string tenantId = "tenant-a",
        string domain = "counter",
        string aggregateId = "counter-1",
        long eventCount = 18,
        long lastSeq = 18,
        bool hasSnapshot = false)
        => new(
            tenantId,
            domain,
            aggregateId,
            lastSeq,
            DateTimeOffset.UtcNow,
            eventCount,
            hasSnapshot,
            StreamStatus.Active);

    private static TimelineEntry MakeEvent(long sequence)
        => new(
            sequence,
            DateTimeOffset.UtcNow,
            TimelineEntryType.Event,
            "Hexalith.Sample.Counter.IncrementedV1",
            "01H8XEABC0CORR" + sequence.ToString("D10", System.Globalization.CultureInfo.InvariantCulture),
            null);

    private static PagedResult<TimelineEntry> Page(IEnumerable<long> seqs, int? totalCount = null, string? continuation = null) {
        IReadOnlyList<TimelineEntry> items = [.. seqs.Select(MakeEvent)];
        return new PagedResult<TimelineEntry>(items, totalCount ?? items.Count, continuation);
    }

    private static PagedResult<StreamSummary> StreamPage(params StreamSummary[] streams)
        => new(streams, streams.Length, null);

    private static void ConfigureDefaultDaprNoise(DaprClient daprClient) {
        _ = daprClient.GetStateAsync<ConsistencyCheckResult>(
                StateStoreName, Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (ConsistencyCheckResult?)null);
        _ = daprClient.GetStateAsync<List<string>>(
                StateStoreName, Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<string>?)null);
        _ = daprClient.GetStateAndETagAsync<List<string>>(
                StateStoreName, Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (new List<string>(), string.Empty));
        _ = daprClient.TrySaveStateAsync(
                StateStoreName, Arg.Any<string>(), Arg.Any<List<string>>(), Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);
    }

    [Fact]
    public async Task CheckStream_HealthyEighteenEvents_NoSequenceAnomalies_ViaTimeline() {
        // AC1, AC3, AC5: timeline returns sequences 1..18; no missing-event anomaly is emitted.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        ConfigureDefaultDaprNoise(daprClient);

        StreamSummary stream = CreateStream(eventCount: 18, lastSeq: 18);
        _ = streamQuery.GetStreamTimelineAsync(
                stream.TenantId, stream.Domain, stream.AggregateId,
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Enumerable.Range(1, 18).Select(i => (long)i))));
        _ = streamQuery.GetStreamTimelineAsync(
                "tenant-a", "counter", "counter-1",
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Enumerable.Range(1, 18).Select(i => (long)i))));

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);
        ConsistencyStreamCheckOutcome outcome = await service.CheckStreamAsync(
            stream,
            [ConsistencyCheckType.SequenceContinuity],
            CancellationToken.None);

        outcome.Anomalies.ShouldBeEmpty();
        outcome.EvaluatedRange.ShouldNotBeNull();
        outcome.EvaluatedRange!.Value.From.ShouldBe(1);
        outcome.EvaluatedRange.Value.To.ShouldBe(18);
        outcome.EvaluatedEventCount.ShouldBe(18);
    }

    [Fact]
    public async Task CheckStream_DoesNotReadRawEventOrMetadataKeys_ForActorBackedStream() {
        // AC2, AC6: no DaprClient.GetStateAsync<T> read against `:events:` or `:metadata` keys.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        ConfigureDefaultDaprNoise(daprClient);

        StreamSummary stream = CreateStream(eventCount: 18, lastSeq: 18);
        _ = streamQuery.GetStreamTimelineAsync(
                stream.TenantId, stream.Domain, stream.AggregateId,
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Enumerable.Range(1, 18).Select(i => (long)i))));

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);
        _ = await service.CheckStreamAsync(
            stream,
            [ConsistencyCheckType.SequenceContinuity, ConsistencyCheckType.MetadataConsistency],
            CancellationToken.None);

        // Verify no raw event keys were read.
        await daprClient.DidNotReceive().GetStateAsync<string>(
            StateStoreName,
            Arg.Is<string>(k => k.Contains(DaprEventsKeyFragment, StringComparison.Ordinal)),
            cancellationToken: Arg.Any<CancellationToken>());
        await daprClient.DidNotReceive().GetStateAsync<object>(
            StateStoreName,
            Arg.Is<string>(k => k.Contains(DaprEventsKeyFragment, StringComparison.Ordinal)),
            cancellationToken: Arg.Any<CancellationToken>());

        // Verify no raw metadata keys were read.
        await daprClient.DidNotReceive().GetStateAsync<object>(
            StateStoreName,
            Arg.Is<string>(k => k.EndsWith(DaprMetadataKeyFragment, StringComparison.Ordinal)),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckStream_UsesScopedQueryCallAsIdentityBoundary_AdjacentTenantNotProbed() {
        // AC11 decision: TimelineEntry does not echo tenant/domain/aggregate; the scoped
        // IStreamQueryService call is the supported identity boundary for DW12.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        ConfigureDefaultDaprNoise(daprClient);

        StreamSummary target = CreateStream("tenant-a", "counter", "counter-1", 18, 18);

        // Adjacent stream is configured but the check should never call it.
        _ = streamQuery.GetStreamTimelineAsync(
                "tenant-b", Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Enumerable.Range(1, 5).Select(i => (long)i))));

        _ = streamQuery.GetStreamTimelineAsync(
                "tenant-a", "counter", "counter-1",
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Enumerable.Range(1, 18).Select(i => (long)i))));

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);
        ConsistencyStreamCheckOutcome outcome = await service.CheckStreamAsync(
            target,
            [ConsistencyCheckType.SequenceContinuity],
            CancellationToken.None);

        outcome.Anomalies.ShouldBeEmpty();
        await streamQuery.Received().GetStreamTimelineAsync(
            "tenant-a", "counter", "counter-1",
            Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await streamQuery.DidNotReceive().GetStreamTimelineAsync(
            "tenant-b", Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckStream_OutOfOrderTimeline_DoesNotReportFalseGap() {
        // AC9: 1,3,2,4 — after order normalization, no missing-event anomaly.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        ConfigureDefaultDaprNoise(daprClient);

        StreamSummary stream = CreateStream(eventCount: 4, lastSeq: 4);
        _ = streamQuery.GetStreamTimelineAsync(
                stream.TenantId, stream.Domain, stream.AggregateId,
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(new long[] { 1, 3, 2, 4 })));

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);
        ConsistencyStreamCheckOutcome outcome = await service.CheckStreamAsync(
            stream,
            [ConsistencyCheckType.SequenceContinuity],
            CancellationToken.None);

        outcome.Anomalies.ShouldNotContain(a =>
            a.CheckType == ConsistencyCheckType.SequenceContinuity
            && a.ExpectedSequence != null
            && a.ActualSequence == null);
    }

    [Fact]
    public async Task CheckStream_RealGap_ReportsMissingSequenceAsDataIntegrityFinding() {
        // AC9: timeline returns 1,2,4 — missing sequence 3 is reported.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        ConfigureDefaultDaprNoise(daprClient);

        StreamSummary stream = CreateStream(eventCount: 3, lastSeq: 4);
        _ = streamQuery.GetStreamTimelineAsync(
                stream.TenantId, stream.Domain, stream.AggregateId,
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(new long[] { 1, 2, 4 })));

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);
        ConsistencyStreamCheckOutcome outcome = await service.CheckStreamAsync(
            stream,
            [ConsistencyCheckType.SequenceContinuity],
            CancellationToken.None);

        outcome.Anomalies.ShouldContain(a =>
            a.CheckType == ConsistencyCheckType.SequenceContinuity
            && a.ExpectedSequence == 3
            && a.ActualSequence == null);
    }

    [Fact]
    public async Task CheckStream_DuplicateAndGap_ReportsBothAsDistinctFindings() {
        // AC9: timeline returns 1,2,2,4 — duplicate 2 and missing 3 reported separately.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        ConfigureDefaultDaprNoise(daprClient);

        StreamSummary stream = CreateStream(eventCount: 4, lastSeq: 4);
        _ = streamQuery.GetStreamTimelineAsync(
                stream.TenantId, stream.Domain, stream.AggregateId,
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(new long[] { 1, 2, 2, 4 })));

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);
        ConsistencyStreamCheckOutcome outcome = await service.CheckStreamAsync(
            stream,
            [ConsistencyCheckType.SequenceContinuity],
            CancellationToken.None);

        outcome.Anomalies.ShouldContain(a =>
            a.CheckType == ConsistencyCheckType.SequenceContinuity
            && a.ExpectedSequence == 3
            && a.ActualSequence == null
            && a.Description.Contains("Missing", StringComparison.OrdinalIgnoreCase));
        outcome.Anomalies.ShouldContain(a =>
            a.CheckType == ConsistencyCheckType.SequenceContinuity
            && a.ActualSequence == 2
            && a.Description.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CheckStream_PartialPagedTimeline_ReturnsInconclusive_NeverHealthy() {
        // AC12: continuationToken signals more data; the check pages.
        // If safety guard trips, result is Inconclusive (Warning), not healthy.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        ConfigureDefaultDaprNoise(daprClient);

        StreamSummary stream = CreateStream(eventCount: 5_000, lastSeq: 5_000);

        // First and only page returns 1..1000 but signals more via continuation token.
        // To force the "inconclusive" branch deterministically we configure the substitute
        // to keep returning the same page with a non-null continuation token regardless of
        // requested fromSequence so the safety guard trips.
        _ = streamQuery.GetStreamTimelineAsync(
                stream.TenantId, stream.Domain, stream.AggregateId,
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Enumerable.Range(1, 1_000).Select(i => (long)i), totalCount: 5_000, continuation: "1001")));

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);
        ConsistencyStreamCheckOutcome outcome = await service.CheckStreamAsync(
            stream,
            [ConsistencyCheckType.SequenceContinuity],
            CancellationToken.None);

        outcome.Anomalies.ShouldContain(a =>
            a.CheckType == ConsistencyCheckType.SequenceContinuity
            && a.Severity == AnomalySeverity.Warning
            && a.Description.Contains("Inconclusive", StringComparison.OrdinalIgnoreCase));
        outcome.Anomalies.ShouldNotContain(a =>
            a.CheckType == ConsistencyCheckType.SequenceContinuity
            && a.ExpectedSequence != null
            && a.ActualSequence == null
            && a.Severity != AnomalySeverity.Warning);
    }

    [Fact]
    public async Task CheckStream_TotalCountCoverageMismatch_ReturnsInconclusive_NotMissingEvents() {
        // AC12: total-count evidence says this page is incomplete even without a continuation token.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        ConfigureDefaultDaprNoise(daprClient);

        StreamSummary stream = CreateStream(eventCount: 5_000, lastSeq: 5_000);
        _ = streamQuery.GetStreamTimelineAsync(
                stream.TenantId, stream.Domain, stream.AggregateId,
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Enumerable.Range(1, 1_000).Select(i => (long)i), totalCount: 5_000)));

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);
        ConsistencyStreamCheckOutcome outcome = await service.CheckStreamAsync(
            stream,
            [ConsistencyCheckType.SequenceContinuity],
            CancellationToken.None);

        outcome.Anomalies.ShouldContain(a =>
            a.CheckType == ConsistencyCheckType.SequenceContinuity
            && a.Severity == AnomalySeverity.Warning
            && a.Description.Contains("Inconclusive", StringComparison.OrdinalIgnoreCase));
        outcome.Anomalies.ShouldNotContain(a =>
            a.CheckType == ConsistencyCheckType.SequenceContinuity
            && a.ExpectedSequence == 1001
            && a.ActualSequence == null);
    }

    [Fact]
    public async Task CheckStream_CancellationTokenPropagatedToTimelineService() {
        // AC13: cancellation flows into the supported query call.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        ConfigureDefaultDaprNoise(daprClient);

        using CancellationTokenSource cts = new();
        cts.Cancel();

        StreamSummary stream = CreateStream();
        _ = streamQuery.GetStreamTimelineAsync(
                stream.TenantId, stream.Domain, stream.AggregateId,
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(),
                Arg.Is<CancellationToken>(t => t.IsCancellationRequested))
            .Returns<Task<PagedResult<TimelineEntry>>>(_ => throw new OperationCanceledException());

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);

        await Should.ThrowAsync<OperationCanceledException>(async () => await service.CheckStreamAsync(
            stream,
            [ConsistencyCheckType.SequenceContinuity],
            cts.Token));

        await streamQuery.Received().GetStreamTimelineAsync(
            stream.TenantId, stream.Domain, stream.AggregateId,
            Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(),
            Arg.Is<CancellationToken>(t => t.IsCancellationRequested));
    }

    [Fact]
    public async Task CheckStream_TimelineException_EmitsQueryFailureWarning_NotMissingEvent() {
        // AC8: query exception is reported as a check-limitation/operational warning, not as
        // "missing events 1..N" and never falls back to raw DAPR key reads.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        ConfigureDefaultDaprNoise(daprClient);

        StreamSummary stream = CreateStream(eventCount: 5, lastSeq: 5);
        _ = streamQuery.GetStreamTimelineAsync(
                stream.TenantId, stream.Domain, stream.AggregateId,
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("simulated query failure"));

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);
        ConsistencyStreamCheckOutcome outcome = await service.CheckStreamAsync(
            stream,
            [ConsistencyCheckType.SequenceContinuity],
            CancellationToken.None);

        outcome.Anomalies.ShouldContain(a =>
            a.CheckType == ConsistencyCheckType.SequenceContinuity
            && a.Severity == AnomalySeverity.Warning);
        outcome.Anomalies.ShouldNotContain(a =>
            a.CheckType == ConsistencyCheckType.SequenceContinuity
            && a.ExpectedSequence == 1
            && a.Severity == AnomalySeverity.Critical);
        await daprClient.DidNotReceive().GetStateAsync<string>(
            StateStoreName,
            Arg.Is<string>(k => k.Contains(DaprEventsKeyFragment, StringComparison.Ordinal)),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckStream_AuthorizationFailure_EmitsDistinctDiagnostic() {
        // AC8: authorization failure is classified separately from generic query failures.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        ConfigureDefaultDaprNoise(daprClient);

        StreamSummary stream = CreateStream(eventCount: 5, lastSeq: 5);
        _ = streamQuery.GetStreamTimelineAsync(
                stream.TenantId, stream.Domain, stream.AggregateId,
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("forbidden", null, HttpStatusCode.Forbidden));

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);
        ConsistencyStreamCheckOutcome outcome = await service.CheckStreamAsync(
            stream,
            [ConsistencyCheckType.SequenceContinuity],
            CancellationToken.None);

        outcome.Anomalies.ShouldContain(a =>
            a.CheckType == ConsistencyCheckType.SequenceContinuity
            && a.Severity == AnomalySeverity.Error
            && a.Details != null
            && a.Details.Contains("AuthorizationFailure", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CheckStream_StreamNotFound_EmitsDistinctDiagnostic() {
        // AC8: stream-not-found is distinct and is not converted into missing events 1..N.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        ConfigureDefaultDaprNoise(daprClient);

        StreamSummary stream = CreateStream(eventCount: 5, lastSeq: 5);
        _ = streamQuery.GetStreamTimelineAsync(
                stream.TenantId, stream.Domain, stream.AggregateId,
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("missing", null, HttpStatusCode.NotFound));

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);
        ConsistencyStreamCheckOutcome outcome = await service.CheckStreamAsync(
            stream,
            [ConsistencyCheckType.SequenceContinuity],
            CancellationToken.None);

        outcome.Anomalies.ShouldContain(a =>
            a.CheckType == ConsistencyCheckType.SequenceContinuity
            && a.Details != null
            && a.Details.Contains("StreamNotFound", StringComparison.Ordinal));
        outcome.Anomalies.ShouldNotContain(a =>
            a.CheckType == ConsistencyCheckType.SequenceContinuity
            && a.ExpectedSequence == 1
            && a.ActualSequence == null
            && a.Severity == AnomalySeverity.Critical);
    }

    [Fact]
    public async Task CheckStream_EmptyStream_NoAnomalies() {
        // AC8: empty stream (LastEventSequence == 0) — skip continuity, no anomalies.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        ConfigureDefaultDaprNoise(daprClient);

        StreamSummary stream = CreateStream(eventCount: 0, lastSeq: 0);

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);
        ConsistencyStreamCheckOutcome outcome = await service.CheckStreamAsync(
            stream,
            [ConsistencyCheckType.SequenceContinuity, ConsistencyCheckType.MetadataConsistency],
            CancellationToken.None);

        outcome.Anomalies.ShouldBeEmpty();
    }

    [Fact]
    public async Task CheckMetadata_HealthyStreamSummaryWithSuccessfulStreamRead_NoAnomaly() {
        // AC4: StreamSummary EventCount == LastEventSequence plus successful supported
        // stream read is Verified, no anomaly.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        ConfigureDefaultDaprNoise(daprClient);

        StreamSummary stream = CreateStream(eventCount: 18, lastSeq: 18);
        _ = streamQuery.GetStreamTimelineAsync(
                stream.TenantId, stream.Domain, stream.AggregateId,
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Enumerable.Range(1, 18).Select(i => (long)i))));

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);
        ConsistencyStreamCheckOutcome outcome = await service.CheckStreamAsync(
            stream,
            [ConsistencyCheckType.SequenceContinuity, ConsistencyCheckType.MetadataConsistency],
            CancellationToken.None);

        outcome.Anomalies.ShouldNotContain(a => a.CheckType == ConsistencyCheckType.MetadataConsistency);
    }

    [Fact]
    public async Task CheckMetadata_WithoutSupportedStreamRead_ReturnsInconclusiveWarning() {
        // AC4: without a metadata contract or successful stream read proof, metadata is
        // visibly Inconclusive instead of silently verified from summary fields alone.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        ConfigureDefaultDaprNoise(daprClient);

        StreamSummary stream = CreateStream(eventCount: 18, lastSeq: 18);
        _ = streamQuery.GetStreamTimelineAsync(
                stream.TenantId, stream.Domain, stream.AggregateId,
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Enumerable.Range(1, 18).Select(i => (long)i))));

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);
        ConsistencyStreamCheckOutcome outcome = await service.CheckStreamAsync(
            stream,
            [ConsistencyCheckType.MetadataConsistency],
            CancellationToken.None);

        outcome.Anomalies.ShouldContain(a =>
            a.CheckType == ConsistencyCheckType.MetadataConsistency
            && a.Severity == AnomalySeverity.Warning
            && a.Description.Contains("Inconclusive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CheckMetadata_DoesNotProbeRawMetadataKey() {
        // AC2, AC6: metadata check never reads `:metadata` raw state keys.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        ConfigureDefaultDaprNoise(daprClient);

        StreamSummary stream = CreateStream(eventCount: 18, lastSeq: 18);

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);
        _ = await service.CheckStreamAsync(
            stream,
            [ConsistencyCheckType.MetadataConsistency],
            CancellationToken.None);

        await daprClient.DidNotReceive().GetStateAsync<object>(
            StateStoreName,
            Arg.Is<string>(k => k.EndsWith(DaprMetadataKeyFragment, StringComparison.Ordinal)),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckMetadata_EventCountMismatch_ReportsAnomalyViaSupportedSummarySignal() {
        // AC4: StreamSummary EventCount != LastEventSequence — real metadata inconsistency.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        ConfigureDefaultDaprNoise(daprClient);

        StreamSummary stream = CreateStream(eventCount: 17, lastSeq: 18);
        _ = streamQuery.GetStreamTimelineAsync(
                stream.TenantId, stream.Domain, stream.AggregateId,
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Enumerable.Range(1, 18).Select(i => (long)i))));

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);
        ConsistencyStreamCheckOutcome outcome = await service.CheckStreamAsync(
            stream,
            [ConsistencyCheckType.SequenceContinuity, ConsistencyCheckType.MetadataConsistency],
            CancellationToken.None);

        outcome.Anomalies.ShouldContain(a =>
            a.CheckType == ConsistencyCheckType.MetadataConsistency
            && a.Severity >= AnomalySeverity.Error);
    }

    [Fact]
    public async Task CheckStream_SequenceContinuityRangeIsRecorded_NotInferredFromItemCount() {
        // AC5: the evaluated sequence range and checked-event count are explicit, not item count.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        ConfigureDefaultDaprNoise(daprClient);

        StreamSummary stream = CreateStream(eventCount: 18, lastSeq: 18);
        _ = streamQuery.GetStreamTimelineAsync(
                stream.TenantId, stream.Domain, stream.AggregateId,
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page(Enumerable.Range(1, 18).Select(i => (long)i))));

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);
        ConsistencyStreamCheckOutcome outcome = await service.CheckStreamAsync(
            stream,
            [ConsistencyCheckType.SequenceContinuity],
            CancellationToken.None);

        outcome.EvaluatedEventCount.ShouldBe(18);
        outcome.EvaluatedRange.ShouldNotBeNull();
        outcome.EvaluatedRange!.Value.From.ShouldBe(1);
        outcome.EvaluatedRange.Value.To.ShouldBe(18);
    }

    [Fact]
    public async Task CheckStream_MissingEventFindings_AreBoundedByAnomalyCap() {
        // Review patch: a huge broken stream must not allocate/report one anomaly per
        // missing sequence before the result-level truncation step.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();
        ConfigureDefaultDaprNoise(daprClient);

        StreamSummary stream = CreateStream(eventCount: 1, lastSeq: 100_000);
        _ = streamQuery.GetStreamTimelineAsync(
                stream.TenantId, stream.Domain, stream.AggregateId,
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Page([1], totalCount: 1)));

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);
        ConsistencyStreamCheckOutcome outcome = await service.CheckStreamAsync(
            stream,
            [ConsistencyCheckType.SequenceContinuity],
            CancellationToken.None);

        outcome.Anomalies.Count.ShouldBeLessThanOrEqualTo(500);
        outcome.Anomalies.ShouldContain(a =>
            a.CheckType == ConsistencyCheckType.SequenceContinuity
            && a.ExpectedSequence == 2);
    }

    [Fact]
    public async Task CancelCheck_CancelsActiveBackgroundScanToken() {
        // AC13: production cancellation interrupts an in-flight supported stream read,
        // not only the direct CheckStreamAsync helper used by lower-level tests.
        DaprClient daprClient = Substitute.For<DaprClient>();
        IStreamQueryService streamQuery = Substitute.For<IStreamQueryService>();

        ConsistencyCheckResult? current = null;
        _ = daprClient.GetStateAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (List<string>?)null);
        _ = daprClient.GetStateAndETagAsync<List<string>>(
                StateStoreName, "admin:consistency:index", cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (new List<string>(), string.Empty));
        _ = daprClient.TrySaveStateAsync(
                StateStoreName, "admin:consistency:index", Arg.Any<List<string>>(), Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);
        _ = daprClient.GetStateAsync<ConsistencyCheckResult>(
                StateStoreName,
                Arg.Is<string>(k => k.StartsWith("admin:consistency:", StringComparison.Ordinal)),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => current);
        _ = daprClient.SaveStateAsync(
                StateStoreName,
                Arg.Is<string>(k => k.StartsWith("admin:consistency:", StringComparison.Ordinal)),
                Arg.Do<ConsistencyCheckResult>(r => current = r),
                metadata: Arg.Any<Dictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        StreamSummary stream = CreateStream(eventCount: 18, lastSeq: 18);
        _ = streamQuery.GetRecentlyActiveStreamsAsync("tenant-a", "counter", 1, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(StreamPage(stream)));
        _ = streamQuery.GetRecentlyActiveStreamsAsync("tenant-a", "counter", 10000, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(StreamPage(stream)));

        TaskCompletionSource<CancellationToken> observedTimelineToken = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> timelineCancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = streamQuery.GetStreamTimelineAsync(
                stream.TenantId, stream.Domain, stream.AggregateId,
                Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => {
                CancellationToken token = call.ArgAt<CancellationToken>(6);
                _ = observedTimelineToken.TrySetResult(token);
                TaskCompletionSource<PagedResult<TimelineEntry>> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _ = token.Register(() => {
                    _ = timelineCancelled.TrySetResult(true);
                    _ = pending.TrySetCanceled(token);
                });
                return pending.Task;
            });

        DaprConsistencyCommandService service = CreateService(daprClient, streamQuery);
        AdminOperationResult trigger = await service.TriggerCheckAsync(
            "tenant-a",
            "counter",
            [ConsistencyCheckType.SequenceContinuity]);

        CancellationToken scanToken = await observedTimelineToken.Task.WaitAsync(TimeSpan.FromSeconds(5));
        scanToken.IsCancellationRequested.ShouldBeFalse();

        AdminOperationResult cancel = await service.CancelCheckAsync(trigger.OperationId);

        cancel.Success.ShouldBeTrue();
        (await timelineCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBeTrue();
        scanToken.IsCancellationRequested.ShouldBeTrue();
    }
}
