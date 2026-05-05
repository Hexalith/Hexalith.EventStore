using System.Text;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Actors;

using Microsoft.AspNetCore.Mvc;

using NSubstitute;

using Shouldly;

using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Controllers;

/// <summary>
/// DW3 ATDD red-phase scaffolds for JSON reconstruction semantics (AC #2, #3, #4).
/// All tests are <c>[Fact(Skip = ...)]</c> until the corresponding production
/// behavior is implemented; the dev removes the Skip marker per AC, watches the
/// test go red, then implements the production change to make it green.
/// </summary>
public class Dw3JsonReconstructionAtddTests {
    private const string _baseSkip = "ATDD red phase — DW3 ";

    // ---------------------------------------------------------------
    // AC #2 — Deletion / explicit-null / nested-removal semantics
    // ---------------------------------------------------------------

    [Fact(Skip = _baseSkip + "AC#2 (omitted property). Remove Skip when implementing.")]
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

    [Fact(Skip = _baseSkip + "AC#2 (explicit JSON null). Remove Skip when implementing.")]
    public async Task Step_ExplicitJsonNullValue_HasDocumentedRepresentation() {
        // Event 1: {"a":1}. Event 2: {"a":null}. The disposition the dev picks
        // (supported, preserved-limitation, accepted-debt, future-actor-api) must
        // be encoded as test data in Dw3JsonBehaviorDispositions and reflected
        // here. Current code: explicit null overwrites because of DeepMerge.
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

        FieldChange? change = frame.FieldChanges.SingleOrDefault(fc => fc.FieldPath == "a");
        change.ShouldNotBeNull("DW3 AC#2: explicit JSON null on a previously-set property must be visible in FieldChanges.");
        change!.OldValue.ShouldBe("1");
        change.NewValue.ShouldBe("null");
    }

    [Fact(Skip = _baseSkip + "AC#2 (nested removal). Remove Skip when implementing.")]
    public async Task Step_NestedPropertyRemovedFromObject_BehaviorMatchesMatrix() {
        // Event 1: {"obj":{"x":1,"y":2}}. Event 2: {"obj":{"x":3}} (no "y").
        // Current JsonDiff DOES emit "y → null" because the recursive nested
        // diff scan picks it up via the "before-not-in-after" pass. This is
        // the preserved-limitation case: dev must either (a) document this as
        // accepted-debt or (b) tighten DeepMerge/JsonDiff to be consistent.
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

        // RED: dev must explicitly choose. Test asserts the nested-removal
        // behavior is internally consistent: if the diff records "obj.y → null",
        // it must align with the documented disposition; if it does not, the
        // FieldChange list must reflect the supported semantics only.
        bool nestedRemovalReported = frame.FieldChanges.Any(
            fc => fc.FieldPath == "obj.y" && fc.NewValue == "null");

        if (nestedRemovalReported) {
            // Currently true under existing code — mark as preserved-limitation
            // explicitly via response metadata when the dev implements AC #1.
            frame.StateJson.Contains("\"y\":2", StringComparison.Ordinal).ShouldBeTrue(
                "DW3 AC#2: if nested removal is reported in FieldChanges, the reconstructed state must still contain the previous value because DeepMerge cannot infer deletions from omission.");
        }
    }

    // ---------------------------------------------------------------
    // AC #3 — Array treatment (opaque-leaf preservation)
    // ---------------------------------------------------------------

    [Fact(Skip = _baseSkip + "AC#3 (array as leaf). Remove Skip when implementing.")]
    public async Task Step_ArrayPayload_TreatedAsOpaqueLeaf_NoElementPaths() {
        // Event with array property: {"items":[1,2,3]}. FlattenJson currently
        // treats arrays as leaves. AC #3 requires this opaque-leaf behavior to
        // be preserved with tests until a recorded decision changes it.
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

        frame.FieldChanges
            .Where(fc => fc.FieldPath.StartsWith("items[", StringComparison.Ordinal))
            .ShouldBeEmpty(
                "DW3 AC#3: arrays must remain opaque leaves — no element-level "
                + "field paths (e.g. items[0]) until a recorded architecture decision approves it.");
        frame.FieldChanges.ShouldContain(fc => fc.FieldPath == "items");
    }

    [Fact(Skip = _baseSkip + "AC#3 (array element-change). Remove Skip when implementing.")]
    public async Task Step_TwoEventsWithDifferentArrayContents_DiffRecordsWholeArray() {
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

        FieldChange? change = frame.FieldChanges.SingleOrDefault(fc => fc.FieldPath == "items");
        change.ShouldNotBeNull("DW3 AC#3: array changes must be reported as a single FieldChange on the array path.");
        change!.OldValue.ShouldBe("[1,2]");
        change.NewValue.ShouldBe("[1,2,3]");
    }

    // ---------------------------------------------------------------
    // AC #4 — Recursion / malformed / non-object / empty-path
    // ---------------------------------------------------------------

    [Fact(Skip = _baseSkip + "AC#4 (deep recursion). Remove Skip when implementing.")]
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
        result.ShouldSatisfyAllConditions(
            () => result.ShouldNotBeOfType<ObjectResult>(
                "DW3 AC#4: deep recursion must not surface as a 500 InternalServerError with stack trace.")
        );
    }

    [Fact(Skip = _baseSkip + "AC#4 (empty property name). Remove Skip when implementing.")]
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
        result.ShouldNotBeOfType<ObjectResult>(
            "DW3 AC#4: empty property names must be skipped or surfaced as 400 — never 500.");
    }

    [Fact(Skip = _baseSkip + "AC#4 (non-object payload). Remove Skip when implementing.")]
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

    [Fact(Skip = _baseSkip + "AC#4 (malformed JSON). Remove Skip when implementing.")]
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
