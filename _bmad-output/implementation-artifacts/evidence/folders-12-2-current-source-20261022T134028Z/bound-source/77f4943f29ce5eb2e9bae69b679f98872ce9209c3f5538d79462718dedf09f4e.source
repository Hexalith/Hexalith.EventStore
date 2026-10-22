using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Actors;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using NSubstitute;

using Shouldly;

using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Controllers;

/// <summary>
/// DW3 ATDD red-phase scaffolds for per-surface large-stream behavior (AC #6, #7).
/// Each public debugging endpoint must have an explicit, tested behavior when
/// called with an over-sized stream or out-of-range sequence. The
/// <c>GetEventsAsync(0)</c> disposition matrix is asserted as a static contract
/// in <see cref="GetEventsAsyncDisposition_AllSurfacesHaveExplicitDisposition"/>.
/// </summary>
public class Dw3LargeStreamSurfaceAtddTests {
    // ---------------------------------------------------------------
    // AC #7 — disposition matrix is exhaustive and bounded
    // ---------------------------------------------------------------

    [Fact]
    public void GetEventsAsyncDisposition_AllSurfacesHaveExplicitDisposition() {
        // Surfaces calling GetEventsAsync(0) per source-code reconnaissance
        // (see ATDD checklist Step 03).
        IReadOnlyList<string> expectedSurfaces = ["timeline", "event-detail", "blame", "bisect", "step", "sandbox", "diff-helper", "trace-map"];

        foreach (string surface in expectedSurfaces) {
            Dw3TestUtilities.Dw3GetEventsAsyncDispositionMatrix.ShouldContainKey(surface,
                $"DW3 AC#7: surface '{surface}' must have an explicit GetEventsAsync(0) disposition.");
        }

        IReadOnlySet<string> allowed = new HashSet<string>(StringComparer.Ordinal) {
            "preserve-legacy",
            "reject-direct-input",
            "bounded-range-read",
            "accepted-debt",
            "future-actor-api",
        };

        foreach (KeyValuePair<string, string> kvp in Dw3TestUtilities.Dw3GetEventsAsyncDispositionMatrix) {
            allowed.ShouldContain(kvp.Value,
                $"DW3 AC#7: surface '{kvp.Key}' has unrecognized disposition '{kvp.Value}'.");
        }
    }

    // ---------------------------------------------------------------
    // AC #6 — per-surface behavior on oversize / out-of-range
    // ---------------------------------------------------------------

    [Fact]
    public async Task Blame_StreamLengthExceedsMaxEvents_ResponseFlagsIsTruncated() {
        ServerEventEnvelope[] envelopes = [.. Enumerable.Range(1, 101)
            .Select(i => Dw3TestUtilities.BuildEnvelope(i, $$"""{"counter":{{i}}}"""))];
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns(envelopes);
        AdminStreamQueryController controller = Dw3TestUtilities.CreateStreamController(actor);

        IActionResult result = await controller.GetAggregateBlameAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            at: null, maxEvents: 50, maxFields: 5_000, ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        AggregateBlameView view = ok.Value.ShouldBeOfType<AggregateBlameView>();
        view.IsTruncated.ShouldBeTrue("DW3 AC#6: blame must report IsTruncated=true when stream length exceeds maxEvents.");
    }

    [Fact]
    public async Task Timeline_StreamLengthExceedsCount_ResponseExposesTruncationSignal() {
        ServerEventEnvelope[] envelopes = [.. Enumerable.Range(1, 200)
            .Select(i => Dw3TestUtilities.BuildEnvelope(i))];
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns(envelopes);
        AdminStreamQueryController controller = Dw3TestUtilities.CreateStreamController(actor);

        IActionResult result = await controller.GetStreamTimelineAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            from: null, to: null, count: 10, ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        PagedResult<TimelineEntry> paged = ok.Value.ShouldBeOfType<PagedResult<TimelineEntry>>();

        // AC #6: timeline must expose truncation through BOTH the count signal
        // (TotalCount = total filtered events, not just the returned page) AND
        // the cursor signal (ContinuationToken non-null when a next page exists).
        // Disjunctive checks let regressions drop one signal silently.
        paged.Items.Count.ShouldBe(10);
        paged.TotalCount.ShouldBe(200,
            "DW3 AC#6: TotalCount must reflect the full filtered count (200), not the returned page (10).");
        paged.ContinuationToken.ShouldNotBeNullOrEmpty(
            "DW3 AC#6: ContinuationToken must be non-null when a next page exists.");
        // Cursor is exclusive: next request uses `from = ContinuationToken`, which is last-returned + 1.
        paged.ContinuationToken.ShouldBe((paged.Items[^1].SequenceNumber + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            "DW3 AC#6: ContinuationToken must be the next sequence (last returned + 1) for exclusive cursor semantics.");
    }

    [Fact]
    public async Task Step_AtSequenceBeyondStream_Returns400WithStableMessage() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([
            Dw3TestUtilities.BuildEnvelope(1),
            Dw3TestUtilities.BuildEnvelope(2),
        ]);
        AdminStreamQueryController controller = Dw3TestUtilities.CreateStreamController(actor);

        IActionResult result = await controller.GetEventStepFrameAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            at: 9999, ct: CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ProblemDetails details = obj.Value.ShouldBeOfType<ProblemDetails>();
        // RED: existing Detail mentions sequence beyond stream — dev must adopt
        // a stable reason code so UI/CLI/MCP can parse it.
        (details.Detail ?? string.Empty).Contains("beyond", StringComparison.Ordinal).ShouldBeTrue(
            "DW3 AC#6: step beyond stream must surface a stable, parseable signal.");
    }

    [Fact]
    public async Task Bisect_BadSequenceBeyondStream_Returns400WithGuidance() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([
            Dw3TestUtilities.BuildEnvelope(1, """{"a":1}"""),
            Dw3TestUtilities.BuildEnvelope(2, """{"a":2}"""),
        ]);
        AdminStreamQueryController controller = Dw3TestUtilities.CreateStreamController(actor);

        IActionResult result = await controller.BisectAggregateStateAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            good: 1, bad: 9999, fields: "a", maxSteps: 30, maxFields: 1_000,
            ct: CancellationToken.None);

        // AC #6: bisect with bad>stream must reject with 400 + stable reason code.
        // The previous silent-clamp behavior is no longer accepted.
        ObjectResult obj = result.ShouldBeAssignableTo<ObjectResult>()!;
        obj.StatusCode.ShouldBe(StatusCodes.Status400BadRequest,
            "DW3 AC#6: bisect with bad>stream must reject with 400, not silently clamp.");
        ProblemDetails details = obj.Value.ShouldBeOfType<ProblemDetails>();
        (details.Detail ?? string.Empty).ShouldNotBeNullOrWhiteSpace(
            "DW3 AC#6: bisect with bad>stream must include guidance text.");
        details.Extensions.ShouldContainKey("reasonCode",
            "DW3 AC#10: 400 responses must carry a stable machine-readable reason code.");
        details.Extensions["reasonCode"].ShouldBe("bad_above_stream");
    }

    [Fact]
    public async Task Sandbox_AtSequenceBeyondStream_Returns400() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([
            Dw3TestUtilities.BuildEnvelope(1, """{"a":1}"""),
        ]);
        AdminStreamQueryController controller = Dw3TestUtilities.CreateStreamController(actor);

        SandboxCommandRequest request = new(
            CommandType: "Test.Command",
            PayloadJson: "{}",
            AtSequence: 9999,
            CorrelationId: null,
            UserId: null);

        IActionResult result = await controller.SandboxCommandAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            request, ct: CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Sandbox_AtSequenceZero_DoesNotInvokeActor() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        AdminStreamQueryController controller = Dw3TestUtilities.CreateStreamController(actor);

        SandboxCommandRequest request = new(
            CommandType: "Test.Command",
            PayloadJson: "{}",
            AtSequence: 0,
            CorrelationId: null,
            UserId: null);

        _ = await controller.SandboxCommandAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            request, ct: CancellationToken.None);

        // AtSequence=0 is the documented "empty initial state" path that
        // intentionally skips actor reads. AC #6 requires this to remain
        // honest and observable.
        _ = await actor.DidNotReceiveWithAnyArgs().GetEventsAsync(default);
    }
}
