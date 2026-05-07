using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.DomainServices;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Controllers;

/// <summary>
/// DW3 ATDD red-phase scaffolds for direct CommandApi parameter upper bounds (AC #5).
/// Each test asserts a 400 ProblemDetails response with a stable reason code AND
/// (when the test seam permits) that the actor's <c>GetEventsAsync</c> was never
/// invoked — proving rejection happens before any expensive full-stream read.
/// </summary>
public class Dw3DirectMaxParameterBoundsAtddTests {
    /// <summary>
    /// Builds a controller wired to an actor substitute that returns an empty
    /// stream. Tests assert the actor was never invoked when the input is
    /// over-limit, so the empty Returns is only a safety net.
    /// </summary>
    private static (AdminStreamQueryController controller, IAggregateActor actor, IActorProxyFactory factory) CreateController() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(Arg.Any<long>()).Returns([]);
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory
            .CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor", Arg.Any<ActorProxyOptions?>())
            .Returns(actor);
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        AdminStreamQueryController controller = new(
            factory,
            invoker,
            Dw3TestUtilities.CreateEmptyStateReconstructor(),
            NullLogger<AdminStreamQueryController>.Instance);
        return (controller, actor, factory);
    }

    private static ProblemDetails ShouldBeBadRequestProblem(IActionResult result) {
        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        return obj.Value.ShouldBeOfType<ProblemDetails>();
    }

    private static void ShouldHaveStableReasonCode(ProblemDetails details, string expectedReasonCode) {
        // Reason code may live in the Detail message or as an Extensions["reasonCode"]
        // entry. Tests accept either presentation but require the code to be one of
        // the bounded vocabulary in Dw3TestUtilities.Dw3DirectBoundReasonCodes.
        string? reason = null;
        if (details.Extensions.TryGetValue("reasonCode", out object? value)) {
            reason = value?.ToString();
        }

        if (reason is null && (details.Detail ?? string.Empty).Contains(expectedReasonCode, StringComparison.Ordinal)) {
            reason = expectedReasonCode;
        }

        reason.ShouldBe(expectedReasonCode,
            $"DW3 AC#5: ProblemDetails must surface stable reason code '{expectedReasonCode}'. "
            + "Place it in Extensions[\"reasonCode\"] or include it verbatim in Detail.");
        Dw3TestUtilities.Dw3DirectBoundReasonCodes.ShouldContain(reason!,
            "DW3 AC#5: reason code must come from the bounded vocabulary.");
    }

    // ---------------------------------------------------------------
    // Timeline — count parameter
    // ---------------------------------------------------------------

    [Fact]
    public async Task Timeline_CountAboveLimit_RejectedWithStableReasonCode_BeforeActorRead() {
        (AdminStreamQueryController controller, IAggregateActor actor, _) = CreateController();

        IActionResult result = await controller.GetStreamTimelineAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            from: null, to: null, count: int.MaxValue, ct: CancellationToken.None);

        ProblemDetails details = ShouldBeBadRequestProblem(result);
        ShouldHaveStableReasonCode(details, "count_above_limit");

        await actor.DidNotReceiveWithAnyArgs().GetEventsAsync(default);
    }

    [Fact]
    public async Task Timeline_DefaultCount_Ok_NoBoundRejection() {
        (AdminStreamQueryController controller, _, _) = CreateController();

        IActionResult result = await controller.GetStreamTimelineAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            from: null, to: null, count: 100, ct: CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>(
            "DW3 AC#5: default value (100) must remain compatible with current behavior.");
    }

    // ---------------------------------------------------------------
    // Blame — maxEvents and maxFields
    // ---------------------------------------------------------------

    [Fact]
    public async Task Blame_MaxEventsAboveLimit_RejectedBeforeActorRead() {
        (AdminStreamQueryController controller, IAggregateActor actor, _) = CreateController();

        IActionResult result = await controller.GetAggregateBlameAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            at: null, maxEvents: int.MaxValue, maxFields: 5_000, ct: CancellationToken.None);

        ProblemDetails details = ShouldBeBadRequestProblem(result);
        ShouldHaveStableReasonCode(details, "max_events_above_limit");

        await actor.DidNotReceiveWithAnyArgs().GetEventsAsync(default);
    }

    [Fact]
    public async Task Blame_MaxFieldsAboveLimit_RejectedBeforeActorRead() {
        (AdminStreamQueryController controller, IAggregateActor actor, _) = CreateController();

        IActionResult result = await controller.GetAggregateBlameAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            at: null, maxEvents: 10_000, maxFields: int.MaxValue, ct: CancellationToken.None);

        ProblemDetails details = ShouldBeBadRequestProblem(result);
        ShouldHaveStableReasonCode(details, "max_fields_above_limit");

        await actor.DidNotReceiveWithAnyArgs().GetEventsAsync(default);
    }

    // ---------------------------------------------------------------
    // Bisect — maxSteps and maxFields
    // ---------------------------------------------------------------

    [Fact]
    public async Task Bisect_MaxStepsAboveLimit_RejectedBeforeActorRead() {
        (AdminStreamQueryController controller, IAggregateActor actor, _) = CreateController();

        IActionResult result = await controller.BisectAggregateStateAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            good: 1, bad: 100, fields: null,
            maxSteps: int.MaxValue, maxFields: 1_000, ct: CancellationToken.None);

        ProblemDetails details = ShouldBeBadRequestProblem(result);
        ShouldHaveStableReasonCode(details, "max_steps_above_limit");

        await actor.DidNotReceiveWithAnyArgs().GetEventsAsync(default);
    }

    [Fact]
    public async Task Bisect_MaxFieldsAboveLimit_RejectedBeforeActorRead() {
        (AdminStreamQueryController controller, IAggregateActor actor, _) = CreateController();

        IActionResult result = await controller.BisectAggregateStateAsync(
            Dw3TestUtilities.TenantId, Dw3TestUtilities.Domain, Dw3TestUtilities.AggregateId,
            good: 1, bad: 100, fields: null,
            maxSteps: 30, maxFields: int.MaxValue, ct: CancellationToken.None);

        ProblemDetails details = ShouldBeBadRequestProblem(result);
        ShouldHaveStableReasonCode(details, "max_fields_above_limit");

        await actor.DidNotReceiveWithAnyArgs().GetEventsAsync(default);
    }

    // ---------------------------------------------------------------
    // Reason-code vocabulary contract
    // ---------------------------------------------------------------

    [Fact]
    public void DirectBoundReasonCodes_AllConformToStableNamingContract() {
        // RED: vocabulary must satisfy regex ^[a-z][a-z0-9_]*$ AND length < 64.
        // Test fails if a future code is added that breaks the contract.
        foreach (string code in Dw3TestUtilities.Dw3DirectBoundReasonCodes) {
            code.Length.ShouldBeLessThan(64,
                $"DW3 AC#10: reason code '{code}' must be < 64 chars (party-mode handoff: machine-readable diagnostics).");
            System.Text.RegularExpressions.Regex.IsMatch(code, "^[a-z][a-z0-9_]*$")
                .ShouldBeTrue($"DW3 AC#10: reason code '{code}' must match ^[a-z][a-z0-9_]*$.");
        }
    }
}
