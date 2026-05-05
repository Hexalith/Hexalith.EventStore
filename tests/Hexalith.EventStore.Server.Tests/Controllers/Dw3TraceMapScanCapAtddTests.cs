using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;

using Microsoft.AspNetCore.Mvc;

using NSubstitute;

using Shouldly;

using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Controllers;

/// <summary>
/// DW3 ATDD red-phase scaffolds for trace-map partial-coverage honesty (AC #8).
/// The current AdminTraceQueryController only sets ScanCapped=true when
/// expectedEventCount.HasValue AND fewer events were found in the scan
/// window. Streams larger than MaxEventScan can hide older same-correlation
/// events without ScanCapped=true when expectedEventCount is null OR matches
/// the tail-window count. AC #8 demands these cases be observable.
/// </summary>
public class Dw3TraceMapScanCapAtddTests {
    private const string _baseSkip = "ATDD red phase — DW3 ";
    private const int _maxEventScan = 10_000;
    private const string _correlationId = "corr-trace-1";

    private static ServerEventEnvelope[] BuildLongStreamWithCorrelationAtBoth(
        int totalEvents,
        int correlationEventsAtHead,
        int correlationEventsAtTail) {
        // Head events have the correlation ID at the OLDER end of the stream;
        // tail events have it at the NEWER end. The scan starts from the latest
        // event and walks backward up to MaxEventScan items, so head events are
        // beyond the scan window when totalEvents > MaxEventScan.
        ServerEventEnvelope[] envelopes = new ServerEventEnvelope[totalEvents];
        for (int i = 0; i < totalEvents; i++) {
            int seq = i + 1;
            string corr = (i < correlationEventsAtHead || i >= totalEvents - correlationEventsAtTail)
                ? _correlationId
                : $"other-{seq}";
            envelopes[i] = Dw3TestUtilities.BuildEnvelope(seq, """{"x":1}""", corrId: corr);
        }

        return envelopes;
    }

    // ---------------------------------------------------------------
    // AC #8 — partial coverage when expectedEventCount is null
    // ---------------------------------------------------------------

    [Fact(Skip = _baseSkip + "AC#8 (scan-cap with null expectedEventCount). Remove Skip when implementing.")]
    public async Task TraceMap_StreamExceedsScanCap_NullExpectedEventCount_SetsScanCappedTrue() {
        // 10_500 events; correlation appears at 5 head events (indices 0..4) and
        // 2 tail events (indices 10_498..10_499). With MaxEventScan=10_000, the
        // scan window is indices 500..10_499 — head events are hidden.
        ServerEventEnvelope[] stream = BuildLongStreamWithCorrelationAtBoth(
            totalEvents: _maxEventScan + 500,
            correlationEventsAtHead: 5,
            correlationEventsAtTail: 2);

        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns(stream);

        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        // expectedEventCount = null (CommandStatusRecord with null EventCount).
        _ = commandStatusStore
            .ReadStatusAsync(Dw3TestUtilities.TenantId, _correlationId, Arg.Any<CancellationToken>())
            .Returns(new CommandStatusRecord(
                Status: CommandStatus.Completed,
                Timestamp: DateTimeOffset.UtcNow,
                AggregateId: Dw3TestUtilities.AggregateId,
                EventCount: null,
                RejectionEventType: null,
                FailureReason: null,
                TimeoutDuration: null));

        AdminTraceQueryController controller = Dw3TestUtilities.CreateTraceController(actor, commandStatusStore);

        IActionResult result = await controller.GetCorrelationTraceMap(
            Dw3TestUtilities.TenantId, _correlationId,
            domain: Dw3TestUtilities.Domain,
            aggregateId: Dw3TestUtilities.AggregateId,
            ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        CorrelationTraceMap map = ok.Value.ShouldBeOfType<CorrelationTraceMap>();

        // RED: current code only sets ScanCapped when expectedEventCount.HasValue.
        // AC #8 requires this to be honest regardless: when totalStreamEvents
        // exceeds MaxEventScan, ScanCapped MUST be true.
        map.ScanCapped.ShouldBeTrue(
            "DW3 AC#8: ScanCapped must be true whenever the stream exceeds MaxEventScan, "
            + "even when expectedEventCount is null.");
        map.ScanCapMessage.ShouldNotBeNullOrEmpty();
    }

    [Fact(Skip = _baseSkip + "AC#8 (scan-cap with older-only correlation). Remove Skip when implementing.")]
    public async Task TraceMap_CorrelationOlderThanScanWindow_ProducesEmptyEvents_ScanCappedTrue() {
        // Correlation events ONLY at the head (older end), all hidden by the cap.
        ServerEventEnvelope[] stream = BuildLongStreamWithCorrelationAtBoth(
            totalEvents: _maxEventScan + 100,
            correlationEventsAtHead: 3,
            correlationEventsAtTail: 0);

        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns(stream);

        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        _ = commandStatusStore
            .ReadStatusAsync(Dw3TestUtilities.TenantId, _correlationId, Arg.Any<CancellationToken>())
            .Returns(new CommandStatusRecord(
                Status: CommandStatus.Completed,
                Timestamp: DateTimeOffset.UtcNow,
                AggregateId: Dw3TestUtilities.AggregateId,
                EventCount: 3,
                RejectionEventType: null,
                FailureReason: null,
                TimeoutDuration: null));

        AdminTraceQueryController controller = Dw3TestUtilities.CreateTraceController(actor, commandStatusStore);

        IActionResult result = await controller.GetCorrelationTraceMap(
            Dw3TestUtilities.TenantId, _correlationId,
            domain: Dw3TestUtilities.Domain,
            aggregateId: Dw3TestUtilities.AggregateId,
            ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        CorrelationTraceMap map = ok.Value.ShouldBeOfType<CorrelationTraceMap>();

        map.ProducedEvents.ShouldBeEmpty();
        map.ScanCapped.ShouldBeTrue(
            "DW3 AC#8: when correlation events exist only beyond the scan window, "
            + "ScanCapped must be true and ProducedEvents must be empty (not silently zero).");
    }

    [Fact(Skip = _baseSkip + "AC#8 (scan-cap when tail count matches expected). Remove Skip when implementing.")]
    public async Task TraceMap_ExpectedCountMatchesFoundButOlderEventsExist_StillScanCapped() {
        // expectedEventCount = 2, both expected events are in tail (newer end),
        // BUT older same-correlation events also exist beyond the scan window.
        // Today producedEvents.Count == expectedEventCount.Value, so the
        // condition "producedEvents.Count < expectedEventCount.Value" is false
        // and ScanCapped stays false — hiding the older events.
        ServerEventEnvelope[] stream = BuildLongStreamWithCorrelationAtBoth(
            totalEvents: _maxEventScan + 200,
            correlationEventsAtHead: 4,
            correlationEventsAtTail: 2);

        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns(stream);

        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        _ = commandStatusStore
            .ReadStatusAsync(Dw3TestUtilities.TenantId, _correlationId, Arg.Any<CancellationToken>())
            .Returns(new CommandStatusRecord(
                Status: CommandStatus.Completed,
                Timestamp: DateTimeOffset.UtcNow,
                AggregateId: Dw3TestUtilities.AggregateId,
                EventCount: 2,
                RejectionEventType: null,
                FailureReason: null,
                TimeoutDuration: null));

        AdminTraceQueryController controller = Dw3TestUtilities.CreateTraceController(actor, commandStatusStore);

        IActionResult result = await controller.GetCorrelationTraceMap(
            Dw3TestUtilities.TenantId, _correlationId,
            domain: Dw3TestUtilities.Domain,
            aggregateId: Dw3TestUtilities.AggregateId,
            ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        CorrelationTraceMap map = ok.Value.ShouldBeOfType<CorrelationTraceMap>();

        map.ScanCapped.ShouldBeTrue(
            "DW3 AC#8: when scanStart>0 (stream exceeds MaxEventScan) the trace "
            + "map must report ScanCapped=true regardless of expectedEventCount alignment, "
            + "because older same-correlation events may still exist beyond the cap.");
    }

    [Fact(Skip = _baseSkip + "AC#8 (scan-cap message format). Remove Skip when implementing.")]
    public async Task TraceMap_ScanCapMessage_ContainsStableTruncationVocabulary() {
        ServerEventEnvelope[] stream = BuildLongStreamWithCorrelationAtBoth(
            totalEvents: _maxEventScan + 100,
            correlationEventsAtHead: 1,
            correlationEventsAtTail: 0);

        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns(stream);

        ICommandStatusStore commandStatusStore = Substitute.For<ICommandStatusStore>();
        _ = commandStatusStore
            .ReadStatusAsync(Dw3TestUtilities.TenantId, _correlationId, Arg.Any<CancellationToken>())
            .Returns(new CommandStatusRecord(
                Status: CommandStatus.Completed,
                Timestamp: DateTimeOffset.UtcNow,
                AggregateId: Dw3TestUtilities.AggregateId,
                EventCount: 1,
                RejectionEventType: null,
                FailureReason: null,
                TimeoutDuration: null));

        AdminTraceQueryController controller = Dw3TestUtilities.CreateTraceController(actor, commandStatusStore);

        IActionResult result = await controller.GetCorrelationTraceMap(
            Dw3TestUtilities.TenantId, _correlationId,
            domain: Dw3TestUtilities.Domain,
            aggregateId: Dw3TestUtilities.AggregateId,
            ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        CorrelationTraceMap map = ok.Value.ShouldBeOfType<CorrelationTraceMap>();

        map.ScanCapMessage.ShouldNotBeNull();
        // Party-mode handoff: text equivalent to
        // "Result truncated: scan cap reached at {count} events."
        // Either that exact phrasing or a stable-vocabulary alternative containing
        // both "truncated" (or "limited") and "scan" must appear.
        string msg = map.ScanCapMessage!.ToLowerInvariant();
        (msg.Contains("truncat") || msg.Contains("limited") || msg.Contains("cap"))
            .ShouldBeTrue("DW3 AC#8: scan-cap message must contain stable truncation vocabulary.");
        msg.ShouldContain("scan");
    }
}
