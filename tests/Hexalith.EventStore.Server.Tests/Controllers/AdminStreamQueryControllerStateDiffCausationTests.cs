using System.Reflection;
using System.Text;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
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

public class AdminStreamQueryControllerStateDiffCausationTests {
    private const string _tenantId = "tenant-a";
    private const string _domain = "counter";
    private const string _aggregateId = "counter-1";

    private static ServerEventEnvelope BuildEnvelope(
        long seq,
        string? userId = "user-1",
        string corrId = "corr-1",
        string? causationId = "cause-1",
        string? messageId = null,
        string typeName = "CounterIncremented",
        byte[]? payload = null)
        => new(
            MessageId: messageId ?? $"msg-{seq}",
            AggregateId: _aggregateId,
            AggregateType: "Counter",
            TenantId: _tenantId,
            Domain: _domain,
            SequenceNumber: seq,
            GlobalPosition: seq,
            Timestamp: new DateTimeOffset(2026, 05, 05, 12, 0, 0, TimeSpan.Zero).AddSeconds(seq),
            CorrelationId: corrId,
            CausationId: causationId ?? string.Empty,
            UserId: userId ?? string.Empty,
            DomainServiceVersion: "1.0.0",
            EventTypeName: typeName,
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: payload ?? Encoding.UTF8.GetBytes("{}"),
            Extensions: null);

    private static AdminStreamQueryController CreateController(IAggregateActor actor) {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        _ = actorProxyFactory
            .CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor", Arg.Any<ActorProxyOptions?>())
            .Returns(actor);
        IDomainServiceInvoker invoker = Substitute.For<IDomainServiceInvoker>();
        return new AdminStreamQueryController(
            actorProxyFactory,
            invoker,
            NullLogger<AdminStreamQueryController>.Instance);
    }

    // -------- /state endpoint --------

    [Fact]
    public void GetAggregateState_RouteAttribute_UsesAtQuery() {
        MethodInfo method = typeof(AdminStreamQueryController)
            .GetMethod(nameof(AdminStreamQueryController.GetAggregateStateAsync))!;
        HttpGetAttribute? attr = method.GetCustomAttribute<HttpGetAttribute>();
        _ = attr.ShouldNotBeNull();
        attr.Template.ShouldBe("{tenantId}/{domain}/{aggregateId}/state");

        ParameterInfo? atParam = method.GetParameters().FirstOrDefault(p => p.Name == "at");
        _ = atParam.ShouldNotBeNull();
        atParam.ParameterType.ShouldBe(typeof(long));
    }

    [Fact]
    public async Task GetAggregateState_AtZero_ReturnsInitialEmptySnapshotWithDeterministicTimestamp() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetAggregateStateAsync(_tenantId, _domain, _aggregateId, 0L, CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        AggregateStateSnapshot snapshot = ok.Value.ShouldBeOfType<AggregateStateSnapshot>();
        snapshot.SequenceNumber.ShouldBe(0L);
        snapshot.StateJson.ShouldBe("{}");
        snapshot.Timestamp.ShouldBe(DateTimeOffset.UnixEpoch);
        _ = await actor.DidNotReceive().GetEventsAsync(Arg.Any<long>());
    }

    [Fact]
    public async Task GetAggregateState_NegativeAt_Returns400ProblemDetails() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetAggregateStateAsync(_tenantId, _domain, _aggregateId, -1L, CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ProblemDetails problem = obj.Value.ShouldBeOfType<ProblemDetails>();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("'at'");
        _ = await actor.DidNotReceive().GetEventsAsync(Arg.Any<long>());
    }

    [Fact]
    public async Task GetAggregateState_EmptyStream_Returns404() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(Arg.Any<long>()).Returns([]);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetAggregateStateAsync(_tenantId, _domain, _aggregateId, 3L, CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetAggregateState_HappyPath_ReturnsReconstructedStateWithEventTimestamp() {
        ServerEventEnvelope[] events = [
            BuildEnvelope(1, payload: Encoding.UTF8.GetBytes("{\"value\":1}")),
            BuildEnvelope(2, payload: Encoding.UTF8.GetBytes("{\"label\":\"two\"}")),
            BuildEnvelope(3, payload: Encoding.UTF8.GetBytes("{\"value\":3}")),
        ];
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns(events);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetAggregateStateAsync(_tenantId, _domain, _aggregateId, 2L, CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        AggregateStateSnapshot snapshot = ok.Value.ShouldBeOfType<AggregateStateSnapshot>();
        snapshot.SequenceNumber.ShouldBe(2L);
        snapshot.Timestamp.ShouldBe(events[1].Timestamp);

        using JsonDocument doc = JsonDocument.Parse(snapshot.StateJson);
        doc.RootElement.GetProperty("value").GetInt32().ShouldBe(1);
        doc.RootElement.GetProperty("label").GetString().ShouldBe("two");
    }

    [Fact]
    public async Task GetAggregateState_OperationCanceled_Rethrows() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(Arg.Any<long>()).ThrowsAsync(new OperationCanceledException());
        AdminStreamQueryController controller = CreateController(actor);

        _ = await Should.ThrowAsync<OperationCanceledException>(() =>
            controller.GetAggregateStateAsync(_tenantId, _domain, _aggregateId, 1L, CancellationToken.None));
    }

    // -------- /diff endpoint --------

    [Fact]
    public void DiffAggregateState_RouteAttribute_UsesFromToQuery() {
        MethodInfo method = typeof(AdminStreamQueryController)
            .GetMethod(nameof(AdminStreamQueryController.DiffAggregateStateAsync))!;
        HttpGetAttribute? attr = method.GetCustomAttribute<HttpGetAttribute>();
        _ = attr.ShouldNotBeNull();
        attr.Template.ShouldBe("{tenantId}/{domain}/{aggregateId}/diff");

        method.GetParameters().Any(p => p.Name == "from" && p.ParameterType == typeof(long)).ShouldBeTrue();
        method.GetParameters().Any(p => p.Name == "to" && p.ParameterType == typeof(long)).ShouldBeTrue();
    }

    [Fact]
    public async Task DiffAggregateState_NegativeFrom_Returns400() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.DiffAggregateStateAsync(_tenantId, _domain, _aggregateId, -1L, 5L, CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        _ = await actor.DidNotReceive().GetEventsAsync(Arg.Any<long>());
    }

    [Fact]
    public async Task DiffAggregateState_ToEqualsFrom_Returns400() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.DiffAggregateStateAsync(_tenantId, _domain, _aggregateId, 3L, 3L, CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        _ = await actor.DidNotReceive().GetEventsAsync(Arg.Any<long>());
    }

    [Fact]
    public async Task DiffAggregateState_EmptyStream_Returns404() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(Arg.Any<long>()).Returns([]);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.DiffAggregateStateAsync(_tenantId, _domain, _aggregateId, 0L, 1L, CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task DiffAggregateState_HappyPath_ReturnsChangedFields() {
        ServerEventEnvelope[] events = [
            BuildEnvelope(1, payload: Encoding.UTF8.GetBytes("{\"value\":1}")),
            BuildEnvelope(2, payload: Encoding.UTF8.GetBytes("{\"value\":2}")),
        ];
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns(events);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.DiffAggregateStateAsync(_tenantId, _domain, _aggregateId, 1L, 2L, CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        AggregateStateDiff diff = ok.Value.ShouldBeOfType<AggregateStateDiff>();
        diff.FromSequence.ShouldBe(1L);
        diff.ToSequence.ShouldBe(2L);
        diff.ChangedFields.Count.ShouldBe(1);
        diff.ChangedFields[0].FieldPath.ShouldBe("value");
        diff.ChangedFields[0].OldValue.ShouldBe("1");
        diff.ChangedFields[0].NewValue.ShouldBe("2");
    }

    [Fact]
    public async Task DiffAggregateState_UnchangedRange_Returns200WithEmptyChangedFields() {
        ServerEventEnvelope[] events = [
            BuildEnvelope(1, payload: Encoding.UTF8.GetBytes("{\"value\":1}")),
            BuildEnvelope(2, payload: Encoding.UTF8.GetBytes("{}")),
        ];
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns(events);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.DiffAggregateStateAsync(_tenantId, _domain, _aggregateId, 1L, 2L, CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        AggregateStateDiff diff = ok.Value.ShouldBeOfType<AggregateStateDiff>();
        diff.ChangedFields.ShouldBeEmpty();
    }

    [Fact]
    public async Task DiffAggregateState_FromZero_TreatsBaselineAsEmptyState() {
        ServerEventEnvelope[] events = [
            BuildEnvelope(1, payload: Encoding.UTF8.GetBytes("{\"value\":7}")),
        ];
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns(events);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.DiffAggregateStateAsync(_tenantId, _domain, _aggregateId, 0L, 1L, CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        AggregateStateDiff diff = ok.Value.ShouldBeOfType<AggregateStateDiff>();
        diff.ChangedFields.Count.ShouldBe(1);
        diff.ChangedFields[0].FieldPath.ShouldBe("value");
        diff.ChangedFields[0].NewValue.ShouldBe("7");
    }

    // -------- /causation endpoint --------

    [Fact]
    public void TraceCausationChain_RouteAttribute_UsesAtQuery() {
        MethodInfo method = typeof(AdminStreamQueryController)
            .GetMethod(nameof(AdminStreamQueryController.TraceCausationChainAsync))!;
        HttpGetAttribute? attr = method.GetCustomAttribute<HttpGetAttribute>();
        _ = attr.ShouldNotBeNull();
        attr.Template.ShouldBe("{tenantId}/{domain}/{aggregateId}/causation");

        method.GetParameters().Any(p => p.Name == "at" && p.ParameterType == typeof(long)).ShouldBeTrue();
    }

    [Fact]
    public async Task TraceCausationChain_AtZero_Returns400() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.TraceCausationChainAsync(_tenantId, _domain, _aggregateId, 0L, CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        _ = await actor.DidNotReceive().GetEventsAsync(Arg.Any<long>());
    }

    [Fact]
    public async Task TraceCausationChain_EmptyStream_Returns404() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(Arg.Any<long>()).Returns([]);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.TraceCausationChainAsync(_tenantId, _domain, _aggregateId, 1L, CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task TraceCausationChain_MissingTargetEvent_Returns404() {
        ServerEventEnvelope[] events = [
            BuildEnvelope(1, payload: Encoding.UTF8.GetBytes("{}")),
            BuildEnvelope(2, payload: Encoding.UTF8.GetBytes("{}")),
        ];
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(Arg.Any<long>()).Returns(events);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.TraceCausationChainAsync(_tenantId, _domain, _aggregateId, 99L, CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task TraceCausationChain_TargetOnly_ReturnsTargetMetadata() {
        // Target has a CausationId but no event in the stream matches it (typical: the
        // CausationId references a command MessageId, not an event MessageId).
        ServerEventEnvelope target = BuildEnvelope(1, causationId: "external-cmd-1", messageId: "evt-1");
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(Arg.Any<long>()).Returns([target]);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.TraceCausationChainAsync(_tenantId, _domain, _aggregateId, 1L, CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        CausationChain chain = ok.Value.ShouldBeOfType<CausationChain>();
        chain.OriginatingCommandId.ShouldBe("external-cmd-1");
        chain.OriginatingCommandType.ShouldBe("CounterIncremented");
        chain.CorrelationId.ShouldBe("corr-1");
        chain.UserId.ShouldBe("user-1");
        chain.Events.Count.ShouldBe(1);
        chain.Events[0].SequenceNumber.ShouldBe(1L);
        chain.AffectedProjections.ShouldBeEmpty();
    }

    [Fact]
    public async Task TraceCausationChain_BlankCausationId_DoesNotFabricateUpstream() {
        // Two events share a correlation id but neither has a CausationId — we must NOT
        // promote them to direct causation.
        ServerEventEnvelope[] events = [
            BuildEnvelope(1, causationId: string.Empty, messageId: "evt-1", corrId: "corr-shared"),
            BuildEnvelope(2, causationId: string.Empty, messageId: "evt-2", corrId: "corr-shared"),
        ];
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(Arg.Any<long>()).Returns(events);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.TraceCausationChainAsync(_tenantId, _domain, _aggregateId, 2L, CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        CausationChain chain = ok.Value.ShouldBeOfType<CausationChain>();
        chain.Events.Count.ShouldBe(1, "blank CausationId must not fabricate links from same-correlation events");
        chain.Events[0].SequenceNumber.ShouldBe(2L);
    }

    [Fact]
    public async Task TraceCausationChain_DirectDownstreamLinkage_IncludesDownstream() {
        // Event 2 is causally linked to event 1 via CausationId == event 1's MessageId.
        ServerEventEnvelope[] events = [
            BuildEnvelope(1, causationId: "external-cmd", messageId: "evt-1"),
            BuildEnvelope(2, causationId: "evt-1", messageId: "evt-2"),
        ];
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(Arg.Any<long>()).Returns(events);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.TraceCausationChainAsync(_tenantId, _domain, _aggregateId, 1L, CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        CausationChain chain = ok.Value.ShouldBeOfType<CausationChain>();
        chain.Events.Count.ShouldBe(2);
        chain.Events[0].SequenceNumber.ShouldBe(1L);
        chain.Events[1].SequenceNumber.ShouldBe(2L);
    }

    [Fact]
    public async Task TraceCausationChain_OperationCanceled_Rethrows() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(Arg.Any<long>()).ThrowsAsync(new OperationCanceledException());
        AdminStreamQueryController controller = CreateController(actor);

        _ = await Should.ThrowAsync<OperationCanceledException>(() =>
            controller.TraceCausationChainAsync(_tenantId, _domain, _aggregateId, 1L, CancellationToken.None));
    }
}
