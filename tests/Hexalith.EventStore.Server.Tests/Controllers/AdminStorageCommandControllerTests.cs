using System.Security.Claims;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Authorization;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using EventStoreTenantValidator = Hexalith.EventStore.Authorization.ITenantValidator;

namespace Hexalith.EventStore.Server.Tests.Controllers;

public class AdminStorageCommandControllerTests {
    [Fact]
    public async Task CreateSnapshot_CommandSubmitOnly_DoesNotCreateActorProxy() {
        (AdminStorageCommandController controller, IActorProxyFactory factory, _, _, _) = CreateController(
            CreatePrincipal(
                new Claim("eventstore:tenant", "tenant-a"),
                new Claim("eventstore:permission", "command:submit")));

        ActionResult<AdminOperationResult> result = await controller.CreateSnapshot(new ManualSnapshotRequest("tenant-a", "orders", "order-1"));

        AdminOperationResult operation = ExtractOperation(result);
        operation.Success.ShouldBeFalse();
        operation.ErrorCode.ShouldBe("RejectedUnauthorized");
        _ = factory.DidNotReceive().CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CreateSnapshot_NullRequest_ReturnsRejectedValidationWithoutActorAccess() {
        (AdminStorageCommandController controller, IActorProxyFactory factory, EventStoreTenantValidator tenantValidator, _, _) = CreateController(
            CreatePrincipal(new Claim("eventstore:admin-role", "Operator")));

        ActionResult<AdminOperationResult> result = await controller.CreateSnapshot(null);

        AdminOperationResult operation = ExtractOperation(result);
        operation.Success.ShouldBeFalse();
        operation.ErrorCode.ShouldBe("RejectedValidation");
        _ = factory.DidNotReceive().CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>());
        _ = await tenantValidator.DidNotReceive().ValidateAsync(
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string?>());
    }

    [Fact]
    public async Task CreateSnapshot_NotFound_WritesFailedJobEvidence() {
        (AdminStorageCommandController controller, _, _, _, DaprClient daprClient) = CreateController(
            CreatePrincipal(
                new Claim("eventstore:tenant", "tenant-a"),
                new Claim("eventstore:admin-role", "Operator")));

        ActionResult<AdminOperationResult> result = await controller.CreateSnapshot(new ManualSnapshotRequest("tenant-a", "orders", "order-1"));

        AdminOperationResult operation = ExtractOperation(result);
        operation.Success.ShouldBeFalse();
        operation.ErrorCode.ShouldBe("NotFound");
        _ = await daprClient.Received(2).TrySaveStateAsync(
            "statestore",
            Arg.Is<string>(key => key == "admin:storage-snapshot-jobs:tenant-a" || key == "admin:storage-snapshot-jobs:all"),
            Arg.Is<List<SnapshotJob>>(jobs =>
                jobs.Count == 1
                && jobs[0].Status == SnapshotJobStatus.Failed
                && jobs[0].SequenceNumber == 0
                && jobs[0].ErrorCode == "NotFound"),
            Arg.Any<string>(),
            stateOptions: Arg.Any<StateOptions?>(),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateSnapshot_EvidenceWriteFailure_ReturnsTypedFailure() {
        IAggregateActor actor = Substitute.For<IAggregateActor>();
        _ = actor.CreateManualSnapshotAsync(Arg.Any<string?>())
            .Returns(new ManualSnapshotResult(ManualSnapshotOutcome.Created, 5, "tenant-a:orders:order-1:snapshot", null, null));

        (AdminStorageCommandController controller, _, _, _, DaprClient daprClient) = CreateController(
            CreatePrincipal(
                new Claim("eventstore:tenant", "tenant-a"),
                new Claim("eventstore:admin-role", "Operator")),
            actor);
        SetupJobIndex(daprClient, saveResult: false);

        ActionResult<AdminOperationResult> result = await controller.CreateSnapshot(new ManualSnapshotRequest("tenant-a", "orders", "order-1"));

        AdminOperationResult operation = ExtractOperation(result);
        operation.Success.ShouldBeFalse();
        operation.ErrorCode.ShouldBe("JobEvidenceWriteFailed");
        operation.OperationId.ShouldStartWith("manual-snapshot-");
    }

    private static (AdminStorageCommandController Controller, IActorProxyFactory Factory, EventStoreTenantValidator TenantValidator, IRbacValidator RbacValidator, DaprClient DaprClient) CreateController(
        ClaimsPrincipal user,
        IAggregateActor? actor = null) {
        actor ??= Substitute.For<IAggregateActor>();
        _ = actor.CreateManualSnapshotAsync(Arg.Any<string?>())
            .Returns(new ManualSnapshotResult(ManualSnapshotOutcome.NotFound, 0, null, "NotFound", "Aggregate stream was not found."));

        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        _ = factory.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor")
            .Returns(actor);

        DaprClient daprClient = Substitute.For<DaprClient>();
        SetupJobIndex(daprClient, saveResult: true);

        EventStoreTenantValidator tenantValidator = Substitute.For<EventStoreTenantValidator>();
        _ = tenantValidator.ValidateAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(TenantValidationResult.Allowed);

        IRbacValidator rbacValidator = Substitute.For<IRbacValidator>();
        _ = rbacValidator.ValidateAsync(
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>())
            .Returns(RbacValidationResult.Allowed);

        var controller = new AdminStorageCommandController(
            factory,
            daprClient,
            tenantValidator,
            rbacValidator,
            Options.Create(new CommandStatusOptions { StateStoreName = "statestore" }),
            NullLogger<AdminStorageCommandController>.Instance) {
            ControllerContext = new ControllerContext {
                HttpContext = new DefaultHttpContext { User = user },
            },
        };

        return (controller, factory, tenantValidator, rbacValidator, daprClient);
    }

    private static void SetupJobIndex(DaprClient daprClient, bool saveResult) {
        _ = daprClient.GetStateAndETagAsync<List<SnapshotJob>>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => (new List<SnapshotJob>(), "etag"));

        _ = daprClient.TrySaveStateAsync(
                "statestore",
                Arg.Any<string>(),
                Arg.Any<List<SnapshotJob>>(),
                Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(saveResult);
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "Test"));

    private static AdminOperationResult ExtractOperation(ActionResult<AdminOperationResult> result)
        => result.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<AdminOperationResult>();
}
