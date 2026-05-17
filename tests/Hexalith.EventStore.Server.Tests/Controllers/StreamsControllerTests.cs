using System.Security.Claims;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.Fakes;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using ServerEventEnvelope = Hexalith.EventStore.Server.Events.EventEnvelope;

namespace Hexalith.EventStore.Server.Tests.Controllers;

public class StreamsControllerTests {
    private const string Tenant = "tenant-a";
    private const string Domain = "party";
    private const string AggregateId = "party-1";

    [Fact]
    public async Task ReadStreamAsyncWithInvalidRangeRejectsBeforeActorProxy() {
        (StreamsController controller, IActorProxyFactory actorProxyFactory, _, _) = CreateController();

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(
            Tenant,
            Domain,
            AggregateId,
            FromSequence: 10,
            ToSequence: 5));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.InvalidRange);
        actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!, default);
    }

    [Fact]
    public async Task ReadStreamAsyncWithContinuationRejectsBeforeActorProxyUntilTokenSupportExists() {
        (StreamsController controller, IActorProxyFactory actorProxyFactory, _, _) = CreateController();

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(
            Tenant,
            Domain,
            AggregateId,
            ContinuationToken: new ReplayContinuationToken("opaque")));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.InvalidContinuation);
        actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!, default);
    }

    [Fact]
    public async Task ReadStreamAsyncWithDeniedTenantRejectsBeforeRbacAndActorProxy() {
        (StreamsController controller, IActorProxyFactory actorProxyFactory, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController();
        tenantValidator.ConfiguredResult = TenantValidationResult.Denied("No tenant access.");

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status403Forbidden);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.UnauthorizedTenant);
        rbacValidator.ReceivedRequests.ShouldBeEmpty();
        actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!, default);
    }

    [Fact]
    public async Task ReadStreamAsyncWithDeniedReplayScopeRejectsBeforeActorProxy() {
        (StreamsController controller, IActorProxyFactory actorProxyFactory, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController();
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Denied("Missing replay permission.");

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status403Forbidden);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.ForbiddenReplayScope);
        actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!, default);
    }

    [Fact]
    public async Task ReadStreamAsyncReturnsOrderedBoundedAggregatePage() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 4));
        _ = actor.ReadEventsRangeAsync(1, null, 3).Returns([
            BuildEnvelope(2),
            BuildEnvelope(3),
            BuildEnvelope(4),
        ]);
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(
            Tenant,
            Domain,
            AggregateId,
            FromSequence: 1,
            PageSize: 2));

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        StreamReadPage page = ok.Value.ShouldBeOfType<StreamReadPage>();
        page.Events.Select(e => e.SequenceNumber).ShouldBe([2, 3]);
        page.Metadata.LastSequenceReturned.ShouldBe(3);
        page.Metadata.LatestSequence.ShouldBe(4);
        page.Metadata.IsTruncated.ShouldBeTrue();
        // P-D3: continuation tokens are deferred until request-binding is implemented.
        // Server returns null and callers paginate via FromSequence = lastSequenceReturned + 1.
        page.Metadata.NextContinuationToken.ShouldBeNull();
        _ = await actor.Received(1).ReadEventsRangeAsync(1, null, 3);
    }

    [Fact]
    public async Task ReadStreamAsyncMapsMissingEventToSafeProblem() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 3));
        _ = actor.ReadEventsRangeAsync(Arg.Any<long>(), Arg.Any<long?>(), Arg.Any<int>())
            .ThrowsAsync(new MissingEventException(2, Tenant, Domain, AggregateId));
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status404NotFound);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.MissingEvent);
        problem.Detail.ShouldNotBeNull();
        AssertNoForbiddenLeakage(problem);
    }

    [Fact]
    public async Task ReadStreamAsyncWithMissingAggregateReturnsBadRequestBeforeActorProxy() {
        (StreamsController controller, IActorProxyFactory actorProxyFactory, _, _) = CreateController();

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.MissingRequiredField);
        string json = System.Text.Json.JsonSerializer.Serialize(problem, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        json.ShouldContain("\"reasonCode\"");
        json.ShouldNotContain("\"ReasonCode\"", Case.Sensitive);
        actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!, default);
    }

    [Theory]
    [InlineData(Tenant, Domain, ":actor-key")]
    public async Task ReadStreamAsyncWithInvalidIdentityShapeReturnsDedicatedReasonBeforeActorProxy(
        string tenant,
        string domain,
        string aggregateId) {
        (StreamsController controller, IActorProxyFactory actorProxyFactory, _, _) = CreateController();

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(tenant, domain, aggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.InvalidAggregateIdentity);
        problem.Detail.ShouldBe("Stream identity is invalid.");
        actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!, default);
    }

    [Fact]
    public async Task ReadStreamAsyncWithTooLargeFromSequenceRejectsBeforeActorProxy() {
        (StreamsController controller, IActorProxyFactory actorProxyFactory, _, _) = CreateController();

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(
            Tenant,
            Domain,
            AggregateId,
            FromSequence: int.MaxValue));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.InvalidRange);
        actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!, default);
    }

    [Fact]
    public async Task ReadStreamAsyncWithTooLargeToSequenceRejectsBeforeActorProxy() {
        (StreamsController controller, IActorProxyFactory actorProxyFactory, _, _) = CreateController();

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(
            Tenant,
            Domain,
            AggregateId,
            ToSequence: (long)int.MaxValue + 1));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.InvalidRange);
        actorProxyFactory.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!, default);
    }

    [Fact]
    public async Task ReadStreamAsyncReturnsNullLastSequenceForEmptyPage() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 10));
        _ = actor.ReadEventsRangeAsync(10, null, 101).Returns([]);
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId, FromSequence: 10));

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        StreamReadPage page = ok.Value.ShouldBeOfType<StreamReadPage>();
        page.Metadata.LastSequenceReturned.ShouldBeNull();
        page.Metadata.LatestSequence.ShouldBe(10);
        page.Metadata.EventCount.ShouldBe(0);
    }

    [Fact]
    public async Task ReadStreamAsyncMissingStreamReturnsSafeNotFound() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: false, CurrentSequence: 0));
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status404NotFound);
        problem.Type.ShouldBe(ProblemTypeUris.NotFound);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.MissingStream);
        _ = await actor.DidNotReceiveWithAnyArgs().ReadEventsRangeAsync(default, default, default);
        AssertNoForbiddenLeakage(problem);
    }

    [Fact]
    public async Task ReadStreamAsyncExistingZeroEventStreamReturnsEmptyPage() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 0));
        _ = actor.ReadEventsRangeAsync(0, null, 101).Returns([]);
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        StreamReadPage page = ok.Value.ShouldBeOfType<StreamReadPage>();
        page.Events.ShouldBeEmpty();
        page.Metadata.LatestSequence.ShouldBe(0);
        page.Metadata.LastSequenceReturned.ShouldBeNull();
    }

    [Fact]
    public async Task ReadStreamAsyncLatestSequenceDoesNotRegressBelowReturnedEvents() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 2));
        _ = actor.ReadEventsRangeAsync(1, null, 3).Returns([BuildEnvelope(2), BuildEnvelope(3)]);
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(
            Tenant,
            Domain,
            AggregateId,
            FromSequence: 1,
            PageSize: 2));

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        StreamReadPage page = ok.Value.ShouldBeOfType<StreamReadPage>();
        page.Metadata.LatestSequence.ShouldBe(3);
    }

    [Fact]
    public async Task ReadStreamAsyncWithUnavailableActorReturnsServiceUnavailable() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 3));
        _ = actor.ReadEventsRangeAsync(Arg.Any<long>(), Arg.Any<long?>(), Arg.Any<int>())
            .ThrowsAsync(new HttpRequestException("dapr unavailable"));
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status503ServiceUnavailable);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.ServiceUnavailable);
        controller.Response.Headers.RetryAfter.ToString().ShouldBe("5");
        AssertNoForbiddenLeakage(problem);
    }

    [Fact]
    public async Task ReadStreamAsyncWithActorApplicationFailureReturnsInternalError() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().ThrowsAsync(new ActorMethodInvocationException("actor failed", new InvalidOperationException("serializer defect"), false));
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status500InternalServerError);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.InternalError);
        AssertNoForbiddenLeakage(problem);
    }

    [Fact]
    public async Task ReadStreamAsyncWithNestedApplicationFailureDoesNotBecomeServiceUnavailable() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().ThrowsAsync(new ActorMethodInvocationException(
            "actor failed",
            new InvalidOperationException("application wrapper", new HttpRequestException("transport-looking inner")),
            false));
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status500InternalServerError);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.InternalError);
        AssertNoForbiddenLeakage(problem);
    }

    private static (StreamsController Controller, IActorProxyFactory ActorProxyFactory, FakeTenantValidator TenantValidator, FakeRbacValidator RbacValidator) CreateController(
        IAggregateActor? actor = null) {
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        if (actor is not null) {
            _ = actorProxyFactory
                .CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor", Arg.Any<ActorProxyOptions?>())
                .Returns(actor);
        }

        var tenantValidator = new FakeTenantValidator {
            ConfiguredResult = TenantValidationResult.Allowed,
        };
        var rbacValidator = new FakeRbacValidator {
            ConfiguredResult = RbacValidationResult.Allowed,
        };
        var controller = new StreamsController(
            actorProxyFactory,
            tenantValidator,
            rbacValidator,
            NullLogger<StreamsController>.Instance) {
            ControllerContext = new ControllerContext {
                HttpContext = new DefaultHttpContext {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim("sub", "user-1"),
                    ], "test")),
                },
            },
        };

        return (controller, actorProxyFactory, tenantValidator, rbacValidator);
    }

    private static ProblemDetails AssertProblem(IActionResult result, int statusCode) {
        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(statusCode);
        return objectResult.Value.ShouldBeOfType<ProblemDetails>();
    }

    private static void AssertNoForbiddenLeakage(ProblemDetails problem) {
        string serialized = System.Text.Json.JsonSerializer.Serialize(problem);
        foreach (string forbidden in new[] {
            "state store",
            "statestore",
            "dapr://",
            "projection-rebuild-checkpoints:",
            "redis://",
            "localhost:",
            "127.0.0.1",
            "Bearer ",
            "stack trace",
            "at Hexalith.",
            "payload",
            "protected",
            "display name",
        }) {
            serialized.ShouldNotContain(forbidden, Case.Insensitive);
        }

        foreach (string forbiddenPattern in new[] { "\\bAggregateActor\\b", "\\bETag\\b" }) {
            System.Text.RegularExpressions.Regex.IsMatch(
                serialized,
                forbiddenPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).ShouldBeFalse();
        }
    }

    private static ServerEventEnvelope BuildEnvelope(long seq)
        => new(
            MessageId: $"msg-{seq}",
            AggregateId: AggregateId,
            AggregateType: "Party",
            TenantId: Tenant,
            Domain: Domain,
            SequenceNumber: seq,
            GlobalPosition: seq,
            Timestamp: DateTimeOffset.UnixEpoch.AddSeconds(seq),
            CorrelationId: $"corr-{seq}",
            CausationId: $"cause-{seq}",
            UserId: "user-1",
            DomainServiceVersion: "v1",
            EventTypeName: "PartyRenamed",
            MetadataVersion: 1,
            SerializationFormat: "json",
            Payload: [],
            Extensions: null);
}
