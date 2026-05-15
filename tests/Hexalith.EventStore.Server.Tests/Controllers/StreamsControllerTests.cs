using System.Security.Claims;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Controllers;
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
        _ = actor.GetEventsAsync(1).Returns([
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
        page.Metadata.NextContinuationToken.ShouldNotBeNull();
        page.Metadata.NextContinuationToken.Value.ShouldNotContain($"{Tenant}:{Domain}:{AggregateId}");
        page.Metadata.NextContinuationToken.Value.ShouldNotContain(":events:");
        _ = await actor.Received(1).GetEventsAsync(1);
    }

    [Fact]
    public async Task ReadStreamAsyncMapsMissingEventToSafeProblem() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetEventsAsync(Arg.Any<long>()).ThrowsAsync(new MissingEventException(2, Tenant, Domain, AggregateId));
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status404NotFound);
        problem.Extensions["reasonCode"].ShouldBe(StreamReplayReasonCodes.MissingEvent);
        problem.Detail.ShouldNotBeNull();
        problem.Detail.ShouldNotContain("state store");
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
