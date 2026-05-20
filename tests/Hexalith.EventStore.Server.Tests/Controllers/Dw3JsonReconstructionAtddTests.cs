using System.Text;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Actors;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

/// <summary>
/// DW3 ATDD red-phase scaffolds for JSON reconstruction semantics (AC #2, #3, #4).
/// These tests now run as the DW3 regression guard for the selected behavior
/// matrix.
/// </summary>
public class Dw3JsonReconstructionAtddTests {
    // ---------------------------------------------------------------
    // AC #2 — Deletion / explicit-null / nested-removal semantics
    // ---------------------------------------------------------------

    [Fact]
    public async Task Step_OmittedPropertyAfterMerge_DoesNotEmitSyntheticDelete() {
        // Step #2: event 1 sets {"a":1,"b":2}. Event 2 omits "b" (sends {"a":3}).
        // Current DeepMerge does not remove "b". A synthetic delete in FieldChanges
        // would overclaim domain semantics. Test pins the supported behavior:
        // either no FieldChange for "b" (preserved-limitation), or a FieldChange
        // explicitly tagged via response metadata indicating disposition.
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([
            Dw3TestUtilities.BuildEnvelope(1, """{"a":1,"b":2}"""),
            Dw3TestUtilities.BuildEnvelope(2, """{"a":3}"""),
        ]);
        AdminStreamQueryController controller = Dw3TestUtilities.CreateStreamController(actor);

        IActionResult result = await controller.GetEventStepFrameAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            at: 2, ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        EventStepFrame frame = ok.Value.ShouldBeOfType<EventStepFrame>();

        // RED: when this Skip is removed, current DeepMerge keeps "b", so no
        // synthetic delete appears. After the dev implements either explicit
        // disposition metadata or fixes the helper, this assertion encodes the
        // chosen behavior.
        bool syntheticDeleteEmitted = frame.FieldChanges.Any(
            fc => fc.FieldPath == "b" && fc.NewValue == "null");
        syntheticDeleteEmitted.ShouldBeFalse(
            "DW3 AC#2: omitted properties must NOT be reported as synthetic deletes (NewValue=\"null\") "
            + "unless a recorded product/architecture decision approves delete semantics.");
    }

    [Fact]
    public async Task Step_ExplicitJsonNullValue_StateComesFromCanonicalReconstructor() {
        // Re-anchored under admin-ui-aggregate-state-replay-correctness: the controller no
        // longer deep-merges payloads. State JSON is whatever the canonical
        // IAggregateStateReconstructor returns. Apply-driven null semantics are owned by the
        // domain state type, not the controller — assert here only that the controller relays
        // the reconstructor's state and never injects payload-derived field changes the Apply
        // path did not produce.
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([
            Dw3TestUtilities.BuildEnvelope(1, """{"a":1}"""),
            Dw3TestUtilities.BuildEnvelope(2, """{"a":null}"""),
        ]);
        AdminStreamQueryController controller = Dw3TestUtilities.CreateStreamController(actor);

        IActionResult result = await controller.GetEventStepFrameAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            at: 2, ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        EventStepFrame frame = ok.Value.ShouldBeOfType<EventStepFrame>();
        // Test stub returns {} for every sequence — proves the controller did not synthesize
        // {a:null} via the legacy DeepMerge fallback that this story removed.
        frame.StateJson.ShouldBe("{}");
        frame.FieldChanges.ShouldBeEmpty();
    }

    [Fact]
    public async Task Step_NestedPropertyRemovedFromObject_StateComesFromCanonicalReconstructor() {
        // Re-anchored: nested removal semantics now live with the domain Apply path. The
        // controller no longer assembles state via DeepMerge so it cannot retain "y":2 from
        // a prior payload nor synthesize "obj.y → null". Assert the state and field changes
        // are exactly what the canonical IAggregateStateReconstructor returned.
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([
            Dw3TestUtilities.BuildEnvelope(1, """{"obj":{"x":1,"y":2}}"""),
            Dw3TestUtilities.BuildEnvelope(2, """{"obj":{"x":3}}"""),
        ]);
        AdminStreamQueryController controller = Dw3TestUtilities.CreateStreamController(actor);

        IActionResult result = await controller.GetEventStepFrameAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            at: 2, ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        EventStepFrame frame = ok.Value.ShouldBeOfType<EventStepFrame>();
        // Test stub returns {} — this guards that the legacy DeepMerge fallback is
        // unreachable from /step (admin-ui-aggregate-state-replay-correctness AC #1).
        frame.StateJson.ShouldBe("{}");
        frame.FieldChanges.ShouldBeEmpty();
    }

    // ---------------------------------------------------------------
    // AC #3 — Array treatment (opaque-leaf preservation)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Step_ArrayPayload_StateComesFromCanonicalReconstructor() {
        // Re-anchored: array semantics moved to the domain Apply path. The controller no
        // longer synthesizes "items" from raw payload bytes; whatever the reconstructor
        // returns is what the surface displays. The default test stub returns {} so no
        // payload-derived field changes leak through.
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([
            Dw3TestUtilities.BuildEnvelope(1, """{"items":[1,2,3]}"""),
        ]);
        AdminStreamQueryController controller = Dw3TestUtilities.CreateStreamController(actor);

        IActionResult result = await controller.GetEventStepFrameAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            at: 1, ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        EventStepFrame frame = ok.Value.ShouldBeOfType<EventStepFrame>();
        frame.StateJson.ShouldBe("{}");
        frame.FieldChanges.ShouldBeEmpty();
    }

    [Fact]
    public async Task Step_TwoEventsWithDifferentArrayContents_StateComesFromCanonicalReconstructor() {
        // Re-anchored: same rationale as Step_ArrayPayload_StateComesFromCanonicalReconstructor.
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([
            Dw3TestUtilities.BuildEnvelope(1, """{"items":[1,2]}"""),
            Dw3TestUtilities.BuildEnvelope(2, """{"items":[1,2,3]}"""),
        ]);
        AdminStreamQueryController controller = Dw3TestUtilities.CreateStreamController(actor);

        IActionResult result = await controller.GetEventStepFrameAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            at: 2, ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        EventStepFrame frame = ok.Value.ShouldBeOfType<EventStepFrame>();
        frame.StateJson.ShouldBe("{}");
        frame.FieldChanges.ShouldBeEmpty();
    }

    // ---------------------------------------------------------------
    // AC #4 — Recursion / malformed / non-object / empty-path
    // ---------------------------------------------------------------

    [Fact]
    public async Task Step_DeeplyNestedJsonPayload_DoesNotCauseStackOverflow() {
        // Build a deeply nested payload. Threshold is intentionally not pinned —
        // dev picks the limit during AC #4 implementation. Test asserts the
        // *shape*: the endpoint either returns 200 or a 400 with a stable
        // reason code. It MUST NOT propagate a StackOverflowException.
        const int depth = 1000;
        StringBuilder sb = new();
        for (int i = 0; i < depth; i++) {
            _ = sb.Append("{\"n\":");
        }

        _ = sb.Append("1");
        for (int i = 0; i < depth; i++) {
            _ = sb.Append('}');
        }

        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([
            Dw3TestUtilities.BuildEnvelope(1, sb.ToString()),
        ]);
        AdminStreamQueryController controller = Dw3TestUtilities.CreateStreamController(actor);

        // RED: must not throw StackOverflowException.
        IActionResult result = await controller.GetEventStepFrameAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            at: 1, ct: CancellationToken.None);

        // Either 200 with bounded result or 400 with stable reason — not 500.
        // Status code is what matters: 500 is forbidden; 200 (OkObjectResult) and 400 (ObjectResult
        // wrapping ProblemDetails via Problem(...)) are both acceptable.
        ObjectResult obj = result.ShouldBeAssignableTo<ObjectResult>()!;
        obj.StatusCode.ShouldNotBe(StatusCodes.Status500InternalServerError,
            "DW3 AC#4: deep recursion must not surface as a 500 InternalServerError with stack trace.");
        obj.StatusCode.ShouldBeOneOf(StatusCodes.Status200OK, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Step_EmptyPropertyNameInPayload_DoesNotCrash() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([
            Dw3TestUtilities.BuildEnvelope(1, """{"":42}"""),
        ]);
        AdminStreamQueryController controller = Dw3TestUtilities.CreateStreamController(actor);

        IActionResult result = await controller.GetEventStepFrameAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            at: 1, ct: CancellationToken.None);

        // RED: current FieldChange ctor throws ArgumentException on whitespace
        // FieldPath. The endpoint must skip such fields instead of returning 500.
        ObjectResult obj = result.ShouldBeAssignableTo<ObjectResult>()!;
        obj.StatusCode.ShouldNotBe(StatusCodes.Status500InternalServerError,
            "DW3 AC#4: empty property names must be skipped or surfaced as 400 — never 500.");
        obj.StatusCode.ShouldBeOneOf(StatusCodes.Status200OK, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Step_NonObjectPayload_SkippedSilently_NoPayloadLeakInProblemDetails() {
        // Numeric payload (123) is valid JSON but not an object.
        // ReconstructState already catches this case and skips. Test asserts
        // the value 123 does not appear in any ProblemDetails.Detail or response body.
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([
            Dw3TestUtilities.BuildEnvelope(1, "123"),
            Dw3TestUtilities.BuildEnvelope(2, """{"normal":true}"""),
        ]);
        AdminStreamQueryController controller = Dw3TestUtilities.CreateStreamController(actor);

        IActionResult result = await controller.GetEventStepFrameAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            at: 2, ct: CancellationToken.None);

        if (result is ObjectResult obj && obj.Value is ProblemDetails details) {
            (details.Detail ?? string.Empty).Contains("123", StringComparison.Ordinal).ShouldBeFalse(
                "DW3 AC#4: non-object payload values must not leak into ProblemDetails.");
        }
    }

    [Fact]
    public async Task Step_MalformedJsonPayloadBytes_SkippedAndContinues() {
        // Malformed payload — not valid JSON.
        byte[] malformed = Encoding.UTF8.GetBytes("{not-valid-json}");

        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([
            Dw3TestUtilities.BuildEnvelope(1, malformed),
            Dw3TestUtilities.BuildEnvelope(2, """{"normal":true}"""),
        ]);
        AdminStreamQueryController controller = Dw3TestUtilities.CreateStreamController(actor);

        IActionResult result = await controller.GetEventStepFrameAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            at: 2, ct: CancellationToken.None);

        if (result is ObjectResult obj && obj.Value is ProblemDetails details) {
            (details.Detail ?? string.Empty).Contains("not-valid-json", StringComparison.Ordinal).ShouldBeFalse(
                "DW3 AC#4: malformed payload bytes must not leak into ProblemDetails.");
        }
    }
}
