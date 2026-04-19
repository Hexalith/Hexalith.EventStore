using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
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

public class AdminStreamQueryControllerTimelineTests {
    private const string _tenantId = "tenant-a";
    private const string _domain = "counter";
    private const string _aggregateId = "counter-1";

    private static ServerEventEnvelope BuildEnvelope(
        long seq,
        string? userId = "user-1",
        string corrId = "corr-1",
        string typeName = "CounterIncremented")
        => new(
            MessageId: $"msg-{seq}",
            AggregateId: _aggregateId,
            AggregateType: "Counter",
            TenantId: _tenantId,
            Domain: _domain,
            SequenceNumber: seq,
            GlobalPosition: seq,
            Timestamp: new DateTimeOffset(2026, 04, 19, 12, 0, 0, TimeSpan.Zero).AddSeconds(seq),
            CorrelationId: corrId,
            CausationId: $"cause-{seq}",
            UserId: userId ?? string.Empty,
            DomainServiceVersion: "1.0.0",
            EventTypeName: typeName,
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: [],
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

    [Fact]
    public async Task Timeline_HappyPath_ReturnsThreeEntriesProjectedFromEnvelopes() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([
            BuildEnvelope(1),
            BuildEnvelope(2),
            BuildEnvelope(3),
        ]);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetStreamTimelineAsync(
            _tenantId, _domain, _aggregateId, from: null, to: null, count: 100, ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        PagedResult<TimelineEntry> paged = ok.Value.ShouldBeOfType<PagedResult<TimelineEntry>>();
        paged.TotalCount.ShouldBe(3);
        paged.ContinuationToken.ShouldBeNull();
        paged.Items.Count.ShouldBe(3);

        TimelineEntry first = paged.Items[0];
        first.SequenceNumber.ShouldBe(1);
        first.EntryType.ShouldBe(TimelineEntryType.Event);
        first.TypeName.ShouldBe("CounterIncremented");
        first.CorrelationId.ShouldBe("corr-1");
        first.UserId.ShouldBe("user-1");
        first.Timestamp.ShouldBe(new DateTimeOffset(2026, 04, 19, 12, 0, 0, TimeSpan.Zero).AddSeconds(1));

        paged.Items.Select(e => e.SequenceNumber).ShouldBe([1, 2, 3]);
        paged.Items.All(e => e.EntryType == TimelineEntryType.Event).ShouldBeTrue();
    }

    [Fact]
    public async Task Timeline_EmptyStream_ReturnsOkWithEmptyItems() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([]);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetStreamTimelineAsync(
            _tenantId, _domain, _aggregateId, from: null, to: null, count: 100, ct: CancellationToken.None);

        result.ShouldNotBeOfType<NotFoundResult>();
        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        PagedResult<TimelineEntry> paged = ok.Value.ShouldBeOfType<PagedResult<TimelineEntry>>();
        paged.Items.Count.ShouldBe(0);
        paged.TotalCount.ShouldBe(0);
        paged.ContinuationToken.ShouldBeNull();
    }

    [Fact]
    public async Task Timeline_RangeFilter_ReturnsOnlyEventsInRange() {
        ServerEventEnvelope[] tenEvents = [.. Enumerable.Range(1, 10).Select(i => BuildEnvelope(i))];
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        // from=3 means the controller passes fromSequence=2 to GetEventsAsync (exclusive lower bound).
        // Actor returns events with SequenceNumber > 2 → [3..10].
        _ = actor.GetEventsAsync(2).Returns([.. tenEvents.Where(e => e.SequenceNumber > 2)]);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetStreamTimelineAsync(
            _tenantId, _domain, _aggregateId, from: 3, to: 7, count: 100, ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        PagedResult<TimelineEntry> paged = ok.Value.ShouldBeOfType<PagedResult<TimelineEntry>>();
        paged.Items.Select(e => e.SequenceNumber).ShouldBe([3, 4, 5, 6, 7]);
        _ = await actor.Received(1).GetEventsAsync(2);
    }

    [Fact]
    public async Task Timeline_CountCap_TakesFirstNAfterOrdering() {
        ServerEventEnvelope[] manyEvents = [.. Enumerable.Range(1, 500).Select(i => BuildEnvelope(i))];
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns(manyEvents);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetStreamTimelineAsync(
            _tenantId, _domain, _aggregateId, from: null, to: null, count: 25, ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        PagedResult<TimelineEntry> paged = ok.Value.ShouldBeOfType<PagedResult<TimelineEntry>>();
        paged.Items.Count.ShouldBe(25);
        paged.Items.Select(e => e.SequenceNumber).ShouldBe(Enumerable.Range(1, 25).Select(i => (long)i));
    }

    [Fact]
    public async Task Timeline_CountZero_NormalizesTo100() {
        ServerEventEnvelope[] events = [.. Enumerable.Range(1, 200).Select(i => BuildEnvelope(i))];
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns(events);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetStreamTimelineAsync(
            _tenantId, _domain, _aggregateId, from: null, to: null, count: 0, ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        PagedResult<TimelineEntry> paged = ok.Value.ShouldBeOfType<PagedResult<TimelineEntry>>();
        paged.Items.Count.ShouldBe(100);
    }

    [Fact]
    public async Task Timeline_CountNegative_NormalizesTo100() {
        ServerEventEnvelope[] events = [.. Enumerable.Range(1, 200).Select(i => BuildEnvelope(i))];
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns(events);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetStreamTimelineAsync(
            _tenantId, _domain, _aggregateId, from: null, to: null, count: -5, ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        PagedResult<TimelineEntry> paged = ok.Value.ShouldBeOfType<PagedResult<TimelineEntry>>();
        paged.Items.Count.ShouldBe(100);
    }

    [Fact]
    public async Task Timeline_BadRequest_FromNegative() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetStreamTimelineAsync(
            _tenantId, _domain, _aggregateId, from: -1, to: null, count: 100, ct: CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ProblemDetails problem = obj.Value.ShouldBeOfType<ProblemDetails>();
        problem.Title.ShouldBe("Bad Request");
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("'from' must be >= 0");
        _ = await actor.DidNotReceive().GetEventsAsync(Arg.Any<long>());
    }

    [Fact]
    public async Task Timeline_BadRequest_ToZero() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetStreamTimelineAsync(
            _tenantId, _domain, _aggregateId, from: null, to: 0, count: 100, ct: CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ProblemDetails problem = obj.Value.ShouldBeOfType<ProblemDetails>();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("'to' must be >= 1");
    }

    [Fact]
    public async Task Timeline_BadRequest_ToLessThanFrom() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetStreamTimelineAsync(
            _tenantId, _domain, _aggregateId, from: 5, to: 3, count: 100, ct: CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        ProblemDetails problem = obj.Value.ShouldBeOfType<ProblemDetails>();
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldContain("'to' must be >= 'from'");
    }

    [Fact]
    public async Task Timeline_UserIdEmptyOrWhitespace_ProjectsToNull() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(0).Returns([
            BuildEnvelope(1, userId: string.Empty),
            BuildEnvelope(2, userId: "   "),
            BuildEnvelope(3, userId: "alice"),
        ]);
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetStreamTimelineAsync(
            _tenantId, _domain, _aggregateId, from: null, to: null, count: 100, ct: CancellationToken.None);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        PagedResult<TimelineEntry> paged = ok.Value.ShouldBeOfType<PagedResult<TimelineEntry>>();
        paged.Items[0].UserId.ShouldBeNull();
        paged.Items[1].UserId.ShouldBeNull();
        paged.Items[2].UserId.ShouldBe("alice");
    }

    [Fact]
    public async Task Timeline_OperationCanceled_Rethrows() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(Arg.Any<long>()).ThrowsAsync(new OperationCanceledException());
        AdminStreamQueryController controller = CreateController(actor);

        _ = await Should.ThrowAsync<OperationCanceledException>(() => controller.GetStreamTimelineAsync(
            _tenantId, _domain, _aggregateId, from: null, to: null, count: 100, ct: CancellationToken.None));
    }

    [Fact]
    public async Task Timeline_ActorThrows_Returns500WithProblemDetails() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(Arg.Any<long>()).ThrowsAsync(new InvalidOperationException("kaboom"));
        AdminStreamQueryController controller = CreateController(actor);

        IActionResult result = await controller.GetStreamTimelineAsync(
            _tenantId, _domain, _aggregateId, from: null, to: null, count: 100, ct: CancellationToken.None);

        ObjectResult obj = result.ShouldBeOfType<ObjectResult>();
        obj.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        ProblemDetails problem = obj.Value.ShouldBeOfType<ProblemDetails>();
        problem.Detail.ShouldBe("Failed to fetch stream timeline.");
        (problem.Detail ?? string.Empty).ShouldNotContain("kaboom");
    }
}
