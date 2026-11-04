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
using Hexalith.EventStore.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using EventStoreTenantValidator = Hexalith.EventStore.Authorization.ITenantValidator;

namespace Hexalith.EventStore.Server.Tests.Controllers;

public class AdminStorageCommandControllerTests {
    /// <summary>Provides invalid tenant-scope scenarios for each storage mutation.</summary>
    /// <returns>Each storage operation paired with one invalid scope scenario.</returns>
    public static IEnumerable<object[]> InvalidScopes()
    {
        foreach (string operation in new[] { "snapshot", "set", "delete" })
        {
            foreach (string violation in new[] { "reserved", "malformed", "tenant-header", "platform-header", "invalid-grant", "duplicate-grant" })
            {
                yield return [operation, violation];
            }
        }
    }

    /// <summary>Verifies that invalid storage scopes are rejected before authorization or state access.</summary>
    /// <param name="operation">The storage mutation to invoke.</param>
    /// <param name="violation">The invalid scope or grant scenario.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [MemberData(nameof(InvalidScopes))]
    public async Task StorageMutation_InvalidOrMixedScope_RejectsBeforeAuthorizationOrState(string operation, string violation)
    {
        List<Claim> claims = [new("eventstore:tenant", "tenant-a"), new("eventstore:admin-role", "Operator")];
        if (violation == "invalid-grant")
        {
            claims.Add(new Claim("eventstore:tenant", "system"));
        }
        if (violation == "duplicate-grant")
        {
            claims.Add(new Claim("eventstore:tenant", "TENANT-A"));
        }
        (AdminStorageCommandController controller, IActorProxyFactory factory, EventStoreTenantValidator tenantValidator,
            IRbacValidator rbacValidator, DaprClient dapr) = CreateController(CreatePrincipal([.. claims]));
        string tenant = violation switch
        {
            "reserved" => "SYSTEM",
            "malformed" => " tenant-a",
            _ => "tenant-a",
        };
        if (violation == "tenant-header")
        {
            controller.Request.Headers["X-Hexalith-Tenant"] = tenant;
        }
        if (violation == "platform-header")
        {
            controller.Request.Headers["X-Hexalith-Scope"] = "platform:tenants";
        }

        AdminOperationResult result = ExtractOperation(await InvokeStorageMutation(controller, operation, tenant));

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe(violation is "reserved" or "malformed" ? "RejectedValidation" : "RejectedUnauthorized");
        tenantValidator.ReceivedCalls().ShouldBeEmpty();
        rbacValidator.ReceivedCalls().ShouldBeEmpty();
        factory.ReceivedCalls().ShouldBeEmpty();
        dapr.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>Verifies that storage authorization and persisted identities use the canonical tenant.</summary>
    /// <param name="operation">The storage mutation to invoke.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Theory]
    [InlineData("snapshot")]
    [InlineData("set")]
    [InlineData("delete")]
    public async Task StorageMutation_MixedCaseTenant_CanonicalizesBeforeAuthorizationAndState(string operation)
    {
        (AdminStorageCommandController controller, IActorProxyFactory factory, EventStoreTenantValidator tenantValidator,
            IRbacValidator rbacValidator, DaprClient dapr) = CreateController(CreatePrincipal(
                new Claim("eventstore:tenant", "TENANT-A"), new Claim("eventstore:admin-role", "Operator")));
        SetupPolicyIndex(dapr, operation == "delete"
            ? [new SnapshotPolicy("tenant-a", "orders", "OrderAggregate", 100, DateTimeOffset.UtcNow)] : []);
        _ = dapr.GetStateAndETagAsync<List<SnapshotPolicy>>("statestore", Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(), metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>()).Returns(_ => (new List<SnapshotPolicy>(), "etag"));
        _ = dapr.TrySaveStateAsync("statestore", Arg.Any<string>(), Arg.Any<List<SnapshotPolicy>>(), Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(), metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>()).Returns(true);

        _ = await InvokeStorageMutation(controller, operation, "Tenant-A");

        _ = await tenantValidator.Received(1).ValidateAsync(Arg.Any<ClaimsPrincipal>(), "tenant-a", Arg.Any<CancellationToken>(),
            operation == "snapshot" ? "order-1" : "OrderAggregate");
        _ = await rbacValidator.Received(1).ValidateAsync(Arg.Any<ClaimsPrincipal>(), "tenant-a", "orders",
            operation == "snapshot" ? "command:replay" : "snapshot:policy", operation == "snapshot" ? "command" : "admin",
            Arg.Any<CancellationToken>(), operation == "snapshot" ? "order-1" : "OrderAggregate");
        if (operation == "snapshot")
        {
            _ = factory.Received(1).CreateActorProxy<IAggregateActor>(new ActorId("tenant-a:orders:order-1"), "AggregateActor");
            _ = await dapr.Received(1).TrySaveStateAsync("statestore", "admin:storage-snapshot-jobs:tenant-a",
                Arg.Is<List<SnapshotJob>>(jobs => jobs.Count == 1 && jobs[0].TenantId == "tenant-a"), Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(), metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>());
        }
        else
        {
            _ = await dapr.Received(1).TrySaveStateAsync("statestore", "admin:storage-snapshot-policies:tenant-a",
                Arg.Is<List<SnapshotPolicy>>(policies => operation == "delete" ? policies.Count == 0
                    : policies.Count == 1 && policies[0].TenantId == "tenant-a"), Arg.Any<string>(),
                stateOptions: Arg.Any<StateOptions?>(), metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>());
        }
    }

    private static Task<ActionResult<AdminOperationResult>> InvokeStorageMutation(AdminStorageCommandController controller, string operation, string tenant)
        => operation switch
        {
            "snapshot" => controller.CreateSnapshot(new ManualSnapshotRequest(tenant, "orders", "order-1")),
            "set" => controller.SetSnapshotPolicy(new SnapshotPolicySetRequest(tenant, "orders", "OrderAggregate", 100)),
            _ => controller.DeleteSnapshotPolicy(new SnapshotPolicyDeleteRequest(tenant, "orders", "OrderAggregate")),
        };

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

    [Fact]
    public async Task SetSnapshotPolicy_TenantDenied_DoesNotReadPolicyState() {
        (AdminStorageCommandController controller, _, EventStoreTenantValidator tenantValidator, _, DaprClient daprClient) = CreateController(
            CreatePrincipal(new Claim("eventstore:admin-role", "Operator")));
        _ = tenantValidator.ValidateAsync(
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>())
            .Returns(TenantValidationResult.Denied("Tenant access denied."));

        ActionResult<AdminOperationResult> result = await controller.SetSnapshotPolicy(
            new SnapshotPolicySetRequest("tenant-a", "orders", "OrderAggregate", 100));

        AdminOperationResult operation = ExtractOperation(result);
        operation.Success.ShouldBeFalse();
        operation.ErrorCode.ShouldBe("RejectedUnauthorized");
        _ = await daprClient.DidNotReceiveWithAnyArgs().GetStateAsync<List<SnapshotPolicy>>(
            default!,
            default!,
            consistencyMode: default,
            metadata: default!,
            cancellationToken: default);
        _ = await daprClient.DidNotReceiveWithAnyArgs().GetStateAndETagAsync<List<SnapshotPolicy>>(
            default!,
            default!,
            consistencyMode: default,
            metadata: default!,
            cancellationToken: default);
    }

    [Fact]
    public async Task SetSnapshotPolicy_InvalidRequest_ReturnsRejectedValidationBeforeTenantAuth() {
        (AdminStorageCommandController controller, _, EventStoreTenantValidator tenantValidator, _, DaprClient daprClient) = CreateController(
            CreatePrincipal(new Claim("eventstore:admin-role", "Operator")));

        ActionResult<AdminOperationResult> result = await controller.SetSnapshotPolicy(
            new SnapshotPolicySetRequest("", "orders", "OrderAggregate", 100));

        AdminOperationResult operation = ExtractOperation(result);
        operation.Success.ShouldBeFalse();
        operation.ErrorCode.ShouldBe("RejectedValidation");
        _ = await tenantValidator.DidNotReceive().ValidateAsync(
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string?>());
        _ = await daprClient.DidNotReceiveWithAnyArgs().GetStateAsync<List<SnapshotPolicy>>(
            default!,
            default!,
            consistencyMode: default,
            metadata: default!,
            cancellationToken: default);
    }

    [Fact]
    public async Task DeleteSnapshotPolicy_InvalidRequest_ReturnsRejectedValidationBeforeTenantAuth() {
        (AdminStorageCommandController controller, _, EventStoreTenantValidator tenantValidator, _, DaprClient daprClient) = CreateController(
            CreatePrincipal(new Claim("eventstore:admin-role", "Operator")));

        ActionResult<AdminOperationResult> result = await controller.DeleteSnapshotPolicy(
            new SnapshotPolicyDeleteRequest("tenant-a", "", "OrderAggregate"));

        AdminOperationResult operation = ExtractOperation(result);
        operation.Success.ShouldBeFalse();
        operation.ErrorCode.ShouldBe("RejectedValidation");
        _ = await tenantValidator.DidNotReceive().ValidateAsync(
            Arg.Any<ClaimsPrincipal>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<string?>());
        _ = await daprClient.DidNotReceiveWithAnyArgs().GetStateAsync<List<SnapshotPolicy>>(
            default!,
            default!,
            consistencyMode: default,
            metadata: default!,
            cancellationToken: default);
    }

    [Fact]
    public async Task DeleteSnapshotPolicy_MissingPolicy_ReturnsTypedNotFound() {
        (AdminStorageCommandController controller, _, _, _, DaprClient daprClient) = CreateController(
            CreatePrincipal(new Claim("eventstore:admin-role", "Operator")));
        SetupPolicyIndex(daprClient, []);

        ActionResult<AdminOperationResult> result = await controller.DeleteSnapshotPolicy(
            new SnapshotPolicyDeleteRequest("tenant-a", "orders", "OrderAggregate"));

        AdminOperationResult operation = ExtractOperation(result);
        operation.Success.ShouldBeFalse();
        operation.ErrorCode.ShouldBe("NotFound");
        _ = await daprClient.DidNotReceiveWithAnyArgs().TrySaveStateAsync<List<SnapshotPolicy>>(
            default!,
            default!,
            default!,
            default!,
            stateOptions: default,
            metadata: default!,
            cancellationToken: default);
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
            new DaprSnapshotPolicyRepository(
                daprClient,
                Options.Create(new CommandStatusOptions { StateStoreName = "statestore" }),
                NullLogger<DaprSnapshotPolicyRepository>.Instance),
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

    private static void SetupPolicyIndex(DaprClient daprClient, List<SnapshotPolicy> policies) {
        _ = daprClient.GetStateAsync<List<SnapshotPolicy>>(
                "statestore",
                Arg.Any<string>(),
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(_ => policies);
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "Test"));

    private static AdminOperationResult ExtractOperation(ActionResult<AdminOperationResult> result)
        => result.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<AdminOperationResult>();
}
