using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.DomainServices;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Controllers;

/// <summary>
/// Guard tests proving the canonical replay path under
/// admin-ui-aggregate-state-replay-correctness:
///   - <see cref="AdminStreamQueryController"/> no longer owns aggregate state reconstruction
///     (no DeepMerge / ReconstructState helpers exist).
///   - Every aggregate-state surface (state, diff, blame, bisect, step, sandbox) delegates to
///     <see cref="IAggregateStateReconstructor"/>.
///   - Failed/Partial replay results map to the documented RFC 7807 ProblemDetails matrix
///     (Failure and HTTP Semantics Matrix in the story Dev Notes).
/// </summary>
public class AdminStreamQueryControllerReplayDelegationTests
{
    private const string _tenantId = "tenant-a";
    private const string _domain = "counter";
    private const string _aggregateId = "counter-1";

    private static ServerEventEnvelope BuildEnvelope(long seq)
        => new(
            MessageId: $"msg-{seq}",
            AggregateId: _aggregateId,
            AggregateType: "Counter",
            TenantId: _tenantId,
            Domain: _domain,
            SequenceNumber: seq,
            GlobalPosition: seq,
            Timestamp: new DateTimeOffset(2026, 05, 07, 12, 0, 0, TimeSpan.Zero).AddSeconds(seq),
            CorrelationId: $"corr-{seq}",
            CausationId: string.Empty,
            UserId: "test-user",
            DomainServiceVersion: "1.0.0",
            EventTypeName: "CounterIncremented",
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: Encoding.UTF8.GetBytes("{}"),
            Extensions: null);

    private static AdminStreamQueryController CreateController(
        IAggregateActor actor,
        IAggregateStateReconstructor reconstructor,
        IDomainServiceInvoker? invoker = null)
    {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        _ = actorProxyFactory
            .CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor", Arg.Any<ActorProxyOptions?>())
            .Returns(actor);
        return new AdminStreamQueryController(
            actorProxyFactory,
            invoker ?? Substitute.For<IDomainServiceInvoker>(),
            reconstructor,
            NullLogger<AdminStreamQueryController>.Instance);
    }

    private sealed record SerializedTestEvent(string EventTypeName, byte[] PayloadBytes, string SerializationFormat)
        : ISerializedEventPayload;

    private static IAggregateStateReconstructor BuildReconstructor(AggregateReconstructionResult preset)
    {
        IAggregateStateReconstructor r = Substitute.For<IAggregateStateReconstructor>();
        _ = r.ReconstructAsync(
                Arg.Any<AggregateIdentity>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<ServerEventEnvelope>>(),
                Arg.Any<long>(),
                Arg.Any<bool>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(preset));
        return r;
    }

    // ---------------------------------------------------------------
    // Guard 1: deep-merge is unreachable
    // ---------------------------------------------------------------

    [Fact]
    public void Controller_HasNoDeepMergeOrReconstructStateMember_ProvingFallbackRemoved()
    {
        // The canonical replay path forbids any controller-side aggregate reconstruction.
        // If a future change reintroduces a DeepMerge or ReconstructState helper this guard
        // breaks and forces the reviewer to confirm the ADR allows it.
        Type controller = typeof(AdminStreamQueryController);
        IEnumerable<MethodInfo> methods = controller.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        methods
            .Where(m => m.Name.Contains("DeepMerge", StringComparison.Ordinal)
                || m.Name.Contains("ReconstructState", StringComparison.Ordinal))
            .ShouldBeEmpty(
                "admin-ui-aggregate-state-replay-correctness: AdminStreamQueryController must not "
                + "own DeepMerge/ReconstructState methods. Aggregate replay is owned by IAggregateStateReconstructor.");
    }

    [Fact]
    public void Controller_RequiresIAggregateStateReconstructorInConstructor()
    {
        ConstructorInfo[] ctors = typeof(AdminStreamQueryController).GetConstructors();
        ctors.Length.ShouldBe(1);
        ctors[0].GetParameters()
            .Any(p => p.ParameterType == typeof(IAggregateStateReconstructor))
            .ShouldBeTrue(
                "Controller must depend on IAggregateStateReconstructor — the canonical replay path.");
    }

    // ---------------------------------------------------------------
    // Guard 2: per-surface delegation
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAggregateStateAsync_DelegatesToReconstructor()
    {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([BuildEnvelope(1)]);
        IAggregateStateReconstructor r = BuildReconstructor(
            AggregateReconstructionResult.Succeeded("{\"count\":1}", 1));
        AdminStreamQueryController controller = CreateController(actor, r);

        IActionResult result = await controller.GetAggregateStateAsync(_tenantId, _domain, _aggregateId, 1, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        await r.Received(1).ReconstructAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ServerEventEnvelope>>(),
            1,
            includeTimeline: false,
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DiffAggregateStateAsync_DelegatesToReconstructorWithTimeline()
    {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([BuildEnvelope(1), BuildEnvelope(2)]);
        IAggregateStateReconstructor r = BuildReconstructor(
            AggregateReconstructionResult.Succeeded("{\"count\":2}", 2,
                timeline: [
                    new AggregateReconstructionTimelineEntry(1, "CounterIncremented", "{\"count\":1}"),
                    new AggregateReconstructionTimelineEntry(2, "CounterIncremented", "{\"count\":2}"),
                ]));
        AdminStreamQueryController controller = CreateController(actor, r);

        IActionResult result = await controller.DiffAggregateStateAsync(_tenantId, _domain, _aggregateId, 1, 2, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        await r.Received(1).ReconstructAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ServerEventEnvelope>>(),
            2,
            includeTimeline: true,
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAggregateBlameAsync_DelegatesToReconstructorWithTimeline()
    {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([BuildEnvelope(1)]);
        IAggregateStateReconstructor r = BuildReconstructor(
            AggregateReconstructionResult.Succeeded("{\"count\":1}", 1,
                timeline: [new AggregateReconstructionTimelineEntry(1, "CounterIncremented", "{\"count\":1}")]));
        AdminStreamQueryController controller = CreateController(actor, r);

        IActionResult result = await controller.GetAggregateBlameAsync(_tenantId, _domain, _aggregateId, at: null, ct: CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        await r.Received(1).ReconstructAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ServerEventEnvelope>>(),
            Arg.Any<long>(),
            includeTimeline: true,
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BisectAggregateStateAsync_DelegatesToReconstructorWithTimeline()
    {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([BuildEnvelope(1), BuildEnvelope(2), BuildEnvelope(3)]);
        IAggregateStateReconstructor r = BuildReconstructor(
            AggregateReconstructionResult.Succeeded("{\"count\":3}", 3,
                timeline: [
                    new AggregateReconstructionTimelineEntry(1, "CounterIncremented", "{\"count\":1}"),
                    new AggregateReconstructionTimelineEntry(2, "CounterIncremented", "{\"count\":2}"),
                    new AggregateReconstructionTimelineEntry(3, "CounterIncremented", "{\"count\":3}"),
                ]));
        AdminStreamQueryController controller = CreateController(actor, r);

        IActionResult result = await controller.BisectAggregateStateAsync(
            _tenantId, _domain, _aggregateId, good: 1, bad: 3, fields: "count", ct: CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        await r.Received(1).ReconstructAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ServerEventEnvelope>>(),
            3,
            includeTimeline: true,
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetEventStepFrameAsync_DelegatesToReconstructorWithTimeline()
    {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([BuildEnvelope(1), BuildEnvelope(2)]);
        IAggregateStateReconstructor r = BuildReconstructor(
            AggregateReconstructionResult.Succeeded("{\"count\":2}", 2,
                timeline: [
                    new AggregateReconstructionTimelineEntry(1, "CounterIncremented", "{\"count\":1}"),
                    new AggregateReconstructionTimelineEntry(2, "CounterIncremented", "{\"count\":2}"),
                ]));
        AdminStreamQueryController controller = CreateController(actor, r);

        IActionResult result = await controller.GetEventStepFrameAsync(_tenantId, _domain, _aggregateId, at: 2, ct: CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        await r.Received(1).ReconstructAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ServerEventEnvelope>>(),
            2,
            includeTimeline: true,
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SandboxCommandAsync_HistoricalAtSequence_ReplaysOnlyEventsUpToSandboxPointPlusSyntheticEvents()
    {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([BuildEnvelope(1), BuildEnvelope(2)]);

        var replayCalls = new List<IReadOnlyList<ServerEventEnvelope>>();
        IAggregateStateReconstructor r = Substitute.For<IAggregateStateReconstructor>();
        _ = r.ReconstructAsync(
                Arg.Any<AggregateIdentity>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<ServerEventEnvelope>>(),
                Arg.Any<long>(),
                Arg.Any<bool>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var envelopes = (IReadOnlyList<ServerEventEnvelope>)call[2]!;
                replayCalls.Add(envelopes);
                long target = (long)call[3]!;
                return Task.FromResult(AggregateReconstructionResult.Succeeded($"{{\"count\":{target}}}", target));
            });

        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(DomainResult.Success([
                new SerializedTestEvent("CounterIncremented", Encoding.UTF8.GetBytes("{}"), "json"),
            ])));
        AdminStreamQueryController controller = CreateController(actor, r, invoker);
        var request = new SandboxCommandRequest(
            CommandType: "IncrementCounter",
            PayloadJson: "{}",
            AtSequence: 1,
            CorrelationId: "corr-sandbox",
            UserId: "user-1");

        IActionResult result = await controller.SandboxCommandAsync(_tenantId, _domain, _aggregateId, request, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        replayCalls.Count.ShouldBe(2);
        replayCalls[1].Count.ShouldBe(2);
        replayCalls[1].Select(e => e.MessageId).ShouldBe(["msg-1", "sandbox-2"]);
    }

    [Fact]
    public async Task SandboxCommandAsync_DomainInvocationFailure_ReturnsSafeOperatorMessage()
    {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        IAggregateStateReconstructor r = BuildReconstructor(AggregateReconstructionResult.Succeeded("{}", 0));
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        _ = invoker.InvokeAsync(Arg.Any<CommandEnvelope>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns<DomainResult>(_ => throw new InvalidOperationException(
                "Response status code does not indicate success: 500 (Internal Server Error). No Handle method found."));
        AdminStreamQueryController controller = CreateController(actor, r, invoker);
        var request = new SandboxCommandRequest(
            CommandType: "Hexalith.EventStore.Sample.Counter.Events.CounterIncremented",
            PayloadJson: "{}",
            AtSequence: 0,
            CorrelationId: null,
            UserId: null);

        IActionResult result = await controller.SandboxCommandAsync(_tenantId, _domain, _aggregateId, request, CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        SandboxResult sandbox = ok.Value.ShouldBeOfType<SandboxResult>();
        sandbox.Outcome.ShouldBe("error");
        sandbox.ErrorMessage.ShouldNotBeNull();
        sandbox.ErrorMessage!.ShouldContain("Verify the command type");
        sandbox.ErrorMessage.ShouldContain("looks like an event type");
        sandbox.ErrorMessage.ShouldNotContain("500");
        sandbox.ErrorMessage.ShouldNotContain("No Handle method");
    }

    [Fact]
    public async Task DiffAggregateStateAsync_SucceededReplayMissingTimeline_ReturnsProblem()
    {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([BuildEnvelope(1), BuildEnvelope(2)]);
        IAggregateStateReconstructor r = BuildReconstructor(
            AggregateReconstructionResult.Succeeded("{\"count\":2}", 2, timeline: null));
        AdminStreamQueryController controller = CreateController(actor, r);

        IActionResult result = await controller.DiffAggregateStateAsync(_tenantId, _domain, _aggregateId, 1, 2, CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        ProblemDetails problem = obj.Value.ShouldBeOfType<ProblemDetails>();
        problem.Extensions["errorCategory"]!.ToString().ShouldBe(AggregateReconstructionErrorCategory.Unexpected.ToString());
    }

    [Fact]
    public async Task GetAggregateBlameAsync_TruncatedWindow_ReplaysFullHistoryUpToTarget()
    {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([BuildEnvelope(1), BuildEnvelope(2), BuildEnvelope(3)]);
        IAggregateStateReconstructor r = BuildReconstructor(
            AggregateReconstructionResult.Succeeded("{\"count\":3}", 3,
                timeline: [
                    new AggregateReconstructionTimelineEntry(1, "CounterIncremented", "{\"count\":1}"),
                    new AggregateReconstructionTimelineEntry(2, "CounterIncremented", "{\"count\":2}"),
                    new AggregateReconstructionTimelineEntry(3, "CounterIncremented", "{\"count\":3}"),
                ]));
        AdminStreamQueryController controller = CreateController(actor, r);

        IActionResult result = await controller.GetAggregateBlameAsync(
            _tenantId, _domain, _aggregateId, at: 3, maxEvents: 1, maxFields: 10, ct: CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        await r.Received(1).ReconstructAsync(
            Arg.Any<AggregateIdentity>(),
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<ServerEventEnvelope>>(events => events.Count == 3),
            3,
            includeTimeline: true,
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    // ---------------------------------------------------------------
    // Guard 3: ProblemDetails matrix
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(AggregateReconstructionErrorCategory.UnknownAggregateType, StatusCodes.Status404NotFound, "unknown-aggregate-type")]
    [InlineData(AggregateReconstructionErrorCategory.UnknownEventType, StatusCodes.Status422UnprocessableEntity, "unknown-event-type")]
    [InlineData(AggregateReconstructionErrorCategory.DeserializationFailed, StatusCodes.Status422UnprocessableEntity, "deserialization-failed")]
    [InlineData(AggregateReconstructionErrorCategory.ApplyHandlerMissing, StatusCodes.Status422UnprocessableEntity, "apply-handler-missing")]
    [InlineData(AggregateReconstructionErrorCategory.UnsupportedVersion, StatusCodes.Status422UnprocessableEntity, "unsupported-version")]
    [InlineData(AggregateReconstructionErrorCategory.Unexpected, StatusCodes.Status500InternalServerError, "unexpected")]
    public async Task GetAggregateStateAsync_FailedReplay_ReturnsExpectedRfc7807ProblemDetails(
        AggregateReconstructionErrorCategory category,
        int expectedStatus,
        string expectedTypeSlug)
    {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([BuildEnvelope(1)]);
        IAggregateStateReconstructor r = BuildReconstructor(
            AggregateReconstructionResult.Failed(category, "boom", failedSequenceNumber: 1, failedEventType: "BogusEvent"));
        AdminStreamQueryController controller = CreateController(actor, r);

        IActionResult result = await controller.GetAggregateStateAsync(_tenantId, _domain, _aggregateId, 1, CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(expectedStatus);
        ProblemDetails problem = obj.Value.ShouldBeOfType<ProblemDetails>();
        problem.Type.ShouldBe($"urn:hexalith:eventstore:replay:{expectedTypeSlug}");
        problem.Extensions["status"]!.ToString().ShouldBe("Failed");
        problem.Extensions["errorCategory"]!.ToString().ShouldBe(category.ToString());
        problem.Extensions["failedSequenceNumber"].ShouldBe(1L);
        problem.Extensions["failedEventType"]!.ToString().ShouldBe("BogusEvent");
    }

    [Fact]
    public async Task GetAggregateStateAsync_PartialReplay_Returns409ConflictWithApplyFailedType()
    {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([BuildEnvelope(1)]);
        IAggregateStateReconstructor r = BuildReconstructor(
            AggregateReconstructionResult.Partial(
                stateJson: "{\"count\":3}",
                lastAppliedSequenceNumber: 3,
                failedSequenceNumber: 4,
                failedEventType: "BogusEvent",
                errorCategory: AggregateReconstructionErrorCategory.ApplyFailed,
                message: "Apply threw."));
        AdminStreamQueryController controller = CreateController(actor, r);

        IActionResult result = await controller.GetAggregateStateAsync(_tenantId, _domain, _aggregateId, 1, CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        ProblemDetails problem = obj.Value.ShouldBeOfType<ProblemDetails>();
        problem.Type.ShouldBe("urn:hexalith:eventstore:replay:apply-failed");
        problem.Extensions["status"]!.ToString().ShouldBe("Partial");
        problem.Extensions["lastAppliedSequenceNumber"].ShouldBe(3L);
    }

    [Fact]
    public async Task FailedReplay_NeverReturns200OkWithEmptyState()
    {
        // Negative-evidence guard (AC #4): a Failed replay must not surface as a 200 OK with {}.
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([BuildEnvelope(1)]);
        IAggregateStateReconstructor r = BuildReconstructor(
            AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.ApplyHandlerMissing,
                "Missing handler",
                failedSequenceNumber: 1,
                failedEventType: "BogusEvent"));
        AdminStreamQueryController controller = CreateController(actor, r);

        IActionResult result = await controller.GetAggregateStateAsync(_tenantId, _domain, _aggregateId, 1, CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldNotBe(StatusCodes.Status200OK);
        obj.Value.ShouldBeOfType<ProblemDetails>();
    }

    [Fact]
    public async Task FailedReplay_ProblemDetailsAlwaysContainsReplayExtensionKeys()
    {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([BuildEnvelope(1)]);
        IAggregateStateReconstructor r = BuildReconstructor(
            AggregateReconstructionResult.Failed(AggregateReconstructionErrorCategory.Unexpected, "raw detail"));
        AdminStreamQueryController controller = CreateController(actor, r);

        IActionResult result = await controller.GetAggregateStateAsync(_tenantId, _domain, _aggregateId, 1, CancellationToken.None);

        ProblemDetails problem = result.ShouldBeOfType<ObjectResult>().Value.ShouldBeOfType<ProblemDetails>();
        problem.Extensions.Keys.ShouldContain("status");
        problem.Extensions.Keys.ShouldContain("failedSequenceNumber");
        problem.Extensions.Keys.ShouldContain("failedEventType");
        problem.Extensions.Keys.ShouldContain("errorCategory");
        problem.Extensions.Keys.ShouldContain("message");
        problem.Extensions.Keys.ShouldContain("lastAppliedSequenceNumber");
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldNotContain("raw detail");
        string extensionMessage = problem.Extensions["message"]!.ToString().ShouldNotBeNull();
        extensionMessage.ShouldNotContain("raw detail");
    }

    // ---------------------------------------------------------------
    // CausationChainView: confirms it intentionally does NOT depend on replay.
    // (No state reconstruction in this surface; only event linkage.)
    // ---------------------------------------------------------------

    [Fact]
    public async Task TraceCausationChainAsync_DoesNotInvokeReconstructor()
    {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([BuildEnvelope(1)]);
        IAggregateStateReconstructor r = Substitute.For<IAggregateStateReconstructor>();
        AdminStreamQueryController controller = CreateController(actor, r);

        IActionResult result = await controller.TraceCausationChainAsync(_tenantId, _domain, _aggregateId, at: 1, ct: CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        await r.DidNotReceiveWithAnyArgs().ReconstructAsync(
            default!, default!, default!, default, default, default, default);
    }
}
