using System.Security.Claims;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Tests.Fakes;
using Hexalith.EventStore.Testing.Fakes;
using Hexalith.EventStore.Testing.Security;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;

/// <summary>
/// Story 22.7d-4 — replay/read regression coverage that injects
/// <see cref="ProtectedDataLeakSentinel"/> values into protection metadata, captured
/// invocation arguments, and serialized ProblemDetails output, and proves no sentinel
/// reaches the public StreamsController response surface.
/// </summary>
public class StreamsControllerProtectedDataLeakRegressionTests {
    private const string Tenant = "tenant-a";
    private const string Domain = "party";
    private const string AggregateId = "party-1";

    [Fact]
    public async Task ReadStreamAsync_UnreadableProtected_WithSentinelKeyAliasInMetadata_DoesNotLeakIntoProblemDetails() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 1));
        _ = actor.ReadEventsRangeAsync(1, null, 2).Returns([
            BuildEnvelope(
                1,
                [9, 9, 9],
                new EventStorePayloadProtectionMetadata(
                    PayloadProtectionState.Protected,
                    MetadataVersion: 1,
                    Scheme: "test-aead",
                    // Intentionally embed a sentinel value into a field that callers would commonly
                    // (and incorrectly) bubble back through diagnostics — proves the controller does
                    // not surface protection metadata fields into ProblemDetails.
                    KeyAlias: ProtectedDataLeakSentinel.ProtectedKeyAlias,
                    ContentHint: null,
                    CompatibilityFlags: null)),
        ]);
        var protectionService = new FakeUnreadableProtectionService();
        protectionService.ConfigureEventUnreadable(UnreadableProtectedDataReason.MissingKey);
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor, protectionService);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId, FromSequence: 1, PageSize: 1));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status422UnprocessableEntity);
        string json = JsonSerializer.Serialize(problem, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        ProtectedDataLeakSentinel.AssertNoLeak([
            problem.Title,
            problem.Detail,
            problem.Type,
            json,
            .. problem.Extensions.Values.Select(static v => v?.ToString()),
            .. problem.Extensions.Keys,
        ]);
        problem.Extensions["reasonCode"].ShouldBe(UnreadableProtectedDataReasonCodes.MissingKey);
    }

    [Fact]
    public async Task ReadStreamAsync_UnreadableProtected_SafeRecoveryMetadataPresent() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: true, CurrentSequence: 2));
        _ = actor.ReadEventsRangeAsync(1, null, 2).Returns([
            BuildEnvelope(
                1,
                [1, 2, 3],
                new EventStorePayloadProtectionMetadata(
                    PayloadProtectionState.Protected,
                    MetadataVersion: 1,
                    Scheme: "test-aead",
                    KeyAlias: "safe-alias",
                    ContentHint: null,
                    CompatibilityFlags: null)),
        ]);
        var protectionService = new FakeUnreadableProtectionService();
        protectionService.ConfigureEventUnreadable(UnreadableProtectedDataReason.ConsistencyMismatch);
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor, protectionService);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId, FromSequence: 1, PageSize: 1));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status422UnprocessableEntity);

        // AC #1: safe recovery metadata MUST remain present so the no-leak guard is not satisfied
        // by blanking diagnostic context. Validate the four safe envelope fields.
        problem.Extensions["reasonCode"].ShouldBe(UnreadableProtectedDataReasonCodes.ConsistencyMismatch);
        problem.Extensions[UnreadableProtectedDataProblem.ExtensionMetadataVersion].ShouldBe(1);
        problem.Extensions[UnreadableProtectedDataProblem.ExtensionSequenceNumber].ShouldBe(1L);
        problem.Extensions[UnreadableProtectedDataProblem.ExtensionDomain].ShouldBe(Domain);
    }

    [Fact]
    public async Task ReadStreamAsync_MissingStream_ProblemDetailsContainsNoSentinel() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(Exists: false, CurrentSequence: 0));
        (StreamsController controller, _, FakeTenantValidator tenantValidator, FakeRbacValidator rbacValidator) = CreateController(actor);
        tenantValidator.ConfiguredResult = TenantValidationResult.Allowed;
        rbacValidator.ConfiguredResult = RbacValidationResult.Allowed;

        IActionResult result = await controller.ReadStreamAsync(new StreamReadRequest(Tenant, Domain, AggregateId));

        ProblemDetails problem = AssertProblem(result, StatusCodes.Status404NotFound);
        string json = JsonSerializer.Serialize(problem, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        ProtectedDataLeakSentinel.AssertNoLeak([
            problem.Title,
            problem.Detail,
            problem.Type,
            json,
        ]);
    }

    private static (StreamsController Controller, IActorProxyFactory ActorProxyFactory, FakeTenantValidator TenantValidator, FakeRbacValidator RbacValidator) CreateController(
        IAggregateActor? actor = null,
        IEventPayloadProtectionService? payloadProtectionService = null) {
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
            NullLogger<StreamsController>.Instance,
            payloadProtectionService) {
            ControllerContext = new ControllerContext {
                HttpContext = new DefaultHttpContext {
                    User = new ClaimsPrincipal(new ClaimsIdentity([
                        new Claim("sub", "user-1"),
                    ], "test")),
                },
            },
        };
        controller.HttpContext.Items["CorrelationId"] = "corr-stream-test";

        return (controller, actorProxyFactory, tenantValidator, rbacValidator);
    }

    private static ProblemDetails AssertProblem(IActionResult result, int statusCode) {
        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(statusCode);
        return objectResult.Value.ShouldBeOfType<ProblemDetails>();
    }

    private static EventEnvelope BuildEnvelope(
        long seq,
        byte[] payload,
        EventStorePayloadProtectionMetadata? metadata)
        => new(
            MessageId: $"msg-{seq}",
            AggregateId: AggregateId,
            AggregateType: "Party",
            TenantId: Tenant,
            Domain: Domain,
            SequenceNumber: seq,
            GlobalPosition: seq,
            Timestamp: System.DateTimeOffset.UnixEpoch.AddSeconds(seq),
            CorrelationId: "corr-stream-test",
            CausationId: "cause-1",
            UserId: "user-1",
            DomainServiceVersion: "1.0.0",
            EventTypeName: "PartyJoined",
            MetadataVersion: metadata?.MetadataVersion ?? 1,
            SerializationFormat: "json",
            Payload: payload,
            Extensions: metadata is null
                ? null
                : EventStorePayloadProtectionMetadataCarrier.Write((System.Collections.Generic.IDictionary<string, string>?)null, metadata));
}
