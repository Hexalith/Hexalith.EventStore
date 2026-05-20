using System.Reflection;
using System.Text;

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

public class AdminStreamQueryControllerEventDetailTests {
    private const string _tenantId = "tenant-a";
    private const string _domain = "counter";
    private const string _aggregateId = "counter-1";

    private static ServerEventEnvelope BuildEnvelope(
        long seq,
        string? userId = "user-1",
        string corrId = "corr-1",
        string? causationId = "cause-1",
        string typeName = "CounterIncremented",
        byte[]? payload = null)
        => new(
            MessageId: $"msg-{seq}",
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
            Payload: payload ?? [],
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
            Dw3TestUtilities.CreateEmptyStateReconstructor(),
            NullLogger<AdminStreamQueryController>.Instance);
    }

    [Fact]
    public async Task EventDetail_HappyPath_ReturnsProjectedDetail() {
        byte[] payload = Encoding.UTF8.GetBytes("{\"value\":42}");
        ServerEventEnvelope envelope = BuildEnvelope(3, payload: payload);
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(2).Returns([envelope]);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetEventDetailAsync(
            _tenantId, _domain, _aggregateId, sequenceNumber: 3, ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        EventDetail detail = ok.Value.ShouldBeOfType<EventDetail>();
        detail.TenantId.ShouldBe(_tenantId);
        detail.Domain.ShouldBe(_domain);
        detail.AggregateId.ShouldBe(_aggregateId);
        detail.SequenceNumber.ShouldBe(3);
        detail.EventTypeName.ShouldBe("CounterIncremented");
        detail.Timestamp.ShouldBe(envelope.Timestamp);
        detail.CorrelationId.ShouldBe("corr-1");
        detail.CausationId.ShouldBe("cause-1");
        detail.UserId.ShouldBe("user-1");
        detail.PayloadJson.ShouldBe("{\"value\":42}");
    }

    [Fact]
    public async Task EventDetail_SequenceOne_UsesZeroLowerBound() {
        ServerEventEnvelope envelope = BuildEnvelope(1, payload: Encoding.UTF8.GetBytes("{\"value\":1}"));
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([envelope]);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetEventDetailAsync(
            _tenantId, _domain, _aggregateId, sequenceNumber: 1, ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        EventDetail detail = ok.Value.ShouldBeOfType<EventDetail>();
        detail.SequenceNumber.ShouldBe(1);
        _ = await actor.Received(1).GetEventsAsync(0);
    }

    [Fact]
    public async Task EventDetail_UsesExclusiveLowerBound() {
        ServerEventEnvelope envelope = BuildEnvelope(5, payload: Encoding.UTF8.GetBytes("{\"value\":5}"));
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(4).Returns([envelope]);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetEventDetailAsync(
            _tenantId, _domain, _aggregateId, sequenceNumber: 5, ct: CancellationToken.None);

        _ = result.ShouldBeOfType<OkObjectResult>();
        _ = await actor.Received(1).GetEventsAsync(4);
        _ = await actor.DidNotReceive().GetEventsAsync(0);
        _ = await actor.DidNotReceive().GetEventsAsync(5);
    }

    [Fact]
    public async Task EventDetail_SelectsExactSequenceFromMultipleReturnedEvents() {
        ServerEventEnvelope[] events = [
            BuildEnvelope(5, payload: Encoding.UTF8.GetBytes("{\"value\":5}")),
            BuildEnvelope(6, payload: Encoding.UTF8.GetBytes("{\"value\":6}")),
            BuildEnvelope(7, payload: Encoding.UTF8.GetBytes("{\"value\":7}")),
        ];
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(5).Returns(events);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetEventDetailAsync(
            _tenantId, _domain, _aggregateId, sequenceNumber: 6, ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        EventDetail detail = ok.Value.ShouldBeOfType<EventDetail>();
        detail.SequenceNumber.ShouldBe(6);
        detail.PayloadJson.ShouldBe("{\"value\":6}");
    }

    [Fact]
    public async Task EventDetail_MissingExactSequence_Returns404ProblemDetails() {
        // Actor returns nearby events (sequence 6, 7) but not the requested sequence 5.
        ServerEventEnvelope[] events = [
            BuildEnvelope(6, payload: Encoding.UTF8.GetBytes("{\"value\":6}")),
            BuildEnvelope(7, payload: Encoding.UTF8.GetBytes("{\"value\":7}")),
        ];
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(4).Returns(events);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetEventDetailAsync(
            _tenantId, _domain, _aggregateId, sequenceNumber: 5, ct: CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        ProblemDetails problem = obj.Value.ShouldBeOfType<ProblemDetails>();
        problem.Title.ShouldBe("Not Found");
        problem.Detail.ShouldBe("Event not found.");
    }

    [Fact]
    public async Task EventDetail_EmptyStream_Returns404ProblemDetails() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(Arg.Any<long>()).Returns([]);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetEventDetailAsync(
            _tenantId, _domain, _aggregateId, sequenceNumber: 1, ct: CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        ProblemDetails problem = obj.Value.ShouldBeOfType<ProblemDetails>();
        problem.Detail.ShouldBe("Event not found.");
    }

    [Fact]
    public async Task EventDetail_InvalidSequence_Returns400AndDoesNotInvokeActor() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetEventDetailAsync(
            _tenantId, _domain, _aggregateId, sequenceNumber: 0, ct: CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ProblemDetails problem = obj.Value.ShouldBeOfType<ProblemDetails>();
        problem.Title.ShouldBe("Bad Request");
        _ = problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("'sequenceNumber' must be >= 1");
        _ = await actor.DidNotReceive().GetEventsAsync(Arg.Any<long>());
    }

    [Fact]
    public async Task EventDetail_NegativeSequence_Returns400AndDoesNotInvokeActor() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetEventDetailAsync(
            _tenantId, _domain, _aggregateId, sequenceNumber: -3, ct: CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        _ = await actor.DidNotReceive().GetEventsAsync(Arg.Any<long>());
    }

    [Fact]
    public async Task EventDetail_CausationAndUserWhitespace_ProjectToNull() {
        // Null/empty/whitespace causation and user IDs project to null on EventDetail.
        ServerEventEnvelope[] envelopes = [
            BuildEnvelope(1, userId: string.Empty, causationId: string.Empty, payload: Encoding.UTF8.GetBytes("{\"value\":1}")),
            BuildEnvelope(2, userId: "   ", causationId: "   ", payload: Encoding.UTF8.GetBytes("{\"value\":2}")),
            BuildEnvelope(3, userId: "alice", causationId: "cause-3", payload: Encoding.UTF8.GetBytes("{\"value\":3}")),
        ];
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns(envelopes);
        _ = actor.GetEventsAsync(1).Returns([envelopes[1], envelopes[2]]);
        _ = actor.GetEventsAsync(2).Returns([envelopes[2]]);
        AdminStreamQueryController controller = CreateController(actor);

        EventDetail empty = ((OkObjectResult)await controller.GetEventDetailAsync(_tenantId, _domain, _aggregateId, 1, CancellationToken.None))
            .Value.ShouldBeOfType<EventDetail>();
        empty.CausationId.ShouldBeNull();
        empty.UserId.ShouldBeNull();

        EventDetail whitespace = ((OkObjectResult)await controller.GetEventDetailAsync(_tenantId, _domain, _aggregateId, 2, CancellationToken.None))
            .Value.ShouldBeOfType<EventDetail>();
        whitespace.CausationId.ShouldBeNull();
        whitespace.UserId.ShouldBeNull();

        EventDetail normal = ((OkObjectResult)await controller.GetEventDetailAsync(_tenantId, _domain, _aggregateId, 3, CancellationToken.None))
            .Value.ShouldBeOfType<EventDetail>();
        normal.CausationId.ShouldBe("cause-3");
        normal.UserId.ShouldBe("alice");
    }

    [Fact]
    public async Task EventDetail_EmptyPayload_NormalizesToEmptyJsonObject() {
        ServerEventEnvelope envelope = BuildEnvelope(1, payload: []);
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([envelope]);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetEventDetailAsync(
            _tenantId, _domain, _aggregateId, sequenceNumber: 1, ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        EventDetail detail = ok.Value.ShouldBeOfType<EventDetail>();
        detail.PayloadJson.ShouldBe("{}");
    }

    [Fact]
    public async Task EventDetail_WhitespacePayload_NormalizesToEmptyJsonObject() {
        ServerEventEnvelope envelope = BuildEnvelope(1, payload: Encoding.UTF8.GetBytes("   "));
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([envelope]);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetEventDetailAsync(
            _tenantId, _domain, _aggregateId, sequenceNumber: 1, ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        EventDetail detail = ok.Value.ShouldBeOfType<EventDetail>();
        detail.PayloadJson.ShouldBe("{}");
    }

    [Fact]
    public async Task EventDetail_NonJsonUtf8Payload_ReturnsDecodedRawText() {
        ServerEventEnvelope envelope = BuildEnvelope(1, payload: Encoding.UTF8.GetBytes("plain text"));
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([envelope]);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetEventDetailAsync(
            _tenantId, _domain, _aggregateId, sequenceNumber: 1, ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        EventDetail detail = ok.Value.ShouldBeOfType<EventDetail>();
        detail.PayloadJson.ShouldBe("plain text");
    }

    [Fact]
    public void EventDetail_RouteAttribute_UsesExpectedTemplate() {
        MethodInfo method = typeof(AdminStreamQueryController)
            .GetMethod(nameof(AdminStreamQueryController.GetEventDetailAsync))!;
        _ = method.ShouldNotBeNull();

        HttpGetAttribute? httpGet = method.GetCustomAttribute<HttpGetAttribute>();
        _ = httpGet.ShouldNotBeNull();
        httpGet!.Template.ShouldBe("{tenantId}/{domain}/{aggregateId}/events/{sequenceNumber:long}");
    }

    [Fact]
    public async Task EventDetail_OperationCanceled_Rethrows() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(Arg.Any<long>()).ThrowsAsync(new OperationCanceledException());
        AdminStreamQueryController controller = CreateController(actor);

        _ = await Should.ThrowAsync<OperationCanceledException>(() => controller.GetEventDetailAsync(
            _tenantId, _domain, _aggregateId, sequenceNumber: 3, ct: CancellationToken.None));
    }

    [Fact]
    public async Task EventDetail_PreCanceledToken_RethrowsAndDoesNotInvokeActor() {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        AdminStreamQueryController controller = CreateController(actor);

        _ = await Should.ThrowAsync<OperationCanceledException>(() => controller.GetEventDetailAsync(
            _tenantId, _domain, _aggregateId, sequenceNumber: 3, ct: cts.Token));
        _ = await actor.DidNotReceive().GetEventsAsync(Arg.Any<long>());
    }

    [Fact]
    public async Task EventDetail_ActorThrows_Returns500WithoutLeakingExceptionMessage() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(Arg.Any<long>()).ThrowsAsync(new InvalidOperationException("kaboom-secret-detail"));
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetEventDetailAsync(
            _tenantId, _domain, _aggregateId, sequenceNumber: 3, ct: CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        ProblemDetails problem = obj.Value.ShouldBeOfType<ProblemDetails>();
        problem.Detail.ShouldBe("Failed to fetch event detail.");
        (problem.Detail ?? string.Empty).ShouldNotContain("kaboom");
    }
}
