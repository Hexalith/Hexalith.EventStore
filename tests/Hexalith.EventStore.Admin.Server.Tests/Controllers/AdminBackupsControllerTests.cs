using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Testing.Security;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

public class AdminBackupsControllerTests {
    private readonly IBackupQueryService _queryService = Substitute.For<IBackupQueryService>();
    private readonly IBackupCommandService _commandService = Substitute.For<IBackupCommandService>();
    private readonly AdminBackupsController _sut;

    public AdminBackupsControllerTests() => _sut = new AdminBackupsController(_queryService, _commandService, NullLogger<AdminBackupsController>.Instance) {
        ControllerContext = new ControllerContext {
            HttpContext = new DefaultHttpContext {
                User = CreatePrincipal("Admin"),
            },
        }
    };

    // === GetBackupJobs ===

    [Fact]
    public async Task GetBackupJobs_ReturnsOk_WithBackupJobs() {
        IReadOnlyList<BackupJob> expected = [];
        _ = _queryService.GetBackupJobsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetBackupJobs("tenant-a");

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetBackupJobs_AdminWithNullTenantId_PassesNullToService() {
        IReadOnlyList<BackupJob> expected = [];
        _ = _queryService.GetBackupJobsAsync(null, Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetBackupJobs(null);
        _ = result.ShouldBeOfType<OkObjectResult>();
        _ = await _queryService.Received(1).GetBackupJobsAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBackupJobs_NonAdminWithNullTenantId_ResolvesToUserTenant() {
        _sut.ControllerContext.HttpContext.User = CreatePrincipal("ReadOnly", "tenant-x");
        IReadOnlyList<BackupJob> expected = [];
        _ = _queryService.GetBackupJobsAsync("tenant-x", Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetBackupJobs(null);
        _ = result.ShouldBeOfType<OkObjectResult>();
        _ = await _queryService.Received(1).GetBackupJobsAsync("tenant-x", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBackupJobs_ServiceThrowsHttpRequestException_Returns503() {
        _ = _queryService.GetBackupJobsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        IActionResult result = await _sut.GetBackupJobs("tenant-a");

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task GetBackupJobs_ServiceThrowsRpcException_Returns503() {
        _ = _queryService.GetBackupJobsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "test")));

        IActionResult result = await _sut.GetBackupJobs("tenant-a");

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task GetBackupJobs_ServiceThrowsUnexpectedException_Returns500() {
        _ = _queryService.GetBackupJobsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Unexpected"));

        IActionResult result = await _sut.GetBackupJobs("tenant-a");

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    // === TriggerBackup ===

    [Fact]
    public async Task TriggerBackup_ReturnsAccepted_WhenServiceSucceeds() {
        var expected = new AdminOperationResult(true, "op-1", "Backup started", null);
        _ = _commandService.TriggerBackupAsync("tenant-a", "nightly", true, Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.TriggerBackup("tenant-a", "nightly", true);

        AcceptedResult acceptedResult = result.ShouldBeOfType<AcceptedResult>();
        acceptedResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task TriggerBackup_ReturnsErrorCode_WhenServiceFails() {
        var failed = new AdminOperationResult(false, "op-1", "Not found", "NotFound");
        _ = _commandService.TriggerBackupAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(failed);

        IActionResult result = await _sut.TriggerBackup("tenant-a", null, true);

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(failed);
    }

    [Fact]
    public async Task TriggerBackup_ReturnsOkWithTypedResult_WhenOperationDeferred() {
        var expected = new AdminOperationResult(false, "deferred-backup-trigger", "Backup creation is deferred.", "Deferred");
        _ = _commandService.TriggerBackupAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.TriggerBackup("tenant-a", null, true);

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task TriggerBackup_ReturnsUnprocessable_WhenInvalidOperation() {
        var failed = new AdminOperationResult(false, "op-1", "Already running", "InvalidOperation");
        _ = _commandService.TriggerBackupAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(failed);

        IActionResult result = await _sut.TriggerBackup("tenant-a", null, true);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task TriggerBackup_Returns503_WhenServiceUnavailable() {
        _ = _commandService.TriggerBackupAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("Timed out"));

        IActionResult result = await _sut.TriggerBackup("tenant-a", null, true);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
    }

    // === ValidateBackup ===

    [Fact]
    public async Task ValidateBackup_ReturnsAccepted_WhenServiceSucceeds() {
        var expected = new AdminOperationResult(true, "op-2", "Valid", null);
        _ = _commandService.ValidateBackupAsync("backup-123", Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ValidateBackup("backup-123");

        AcceptedResult acceptedResult = result.ShouldBeOfType<AcceptedResult>();
        acceptedResult.Value.ShouldBe(expected);
    }

    // === TriggerRestore ===

    [Fact]
    public async Task TriggerRestore_ReturnsAccepted_WhenServiceSucceeds() {
        var expected = new AdminOperationResult(true, "op-3", "Restore started", null);
        _ = _commandService.TriggerRestoreAsync("backup-123", Arg.Any<DateTimeOffset?>(), false, Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.TriggerRestore("backup-123", null, false);

        AcceptedResult acceptedResult = result.ShouldBeOfType<AcceptedResult>();
        acceptedResult.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("Deferred")]
    [InlineData("RejectedValidation")]
    [InlineData("RejectedUnauthorized")]
    [InlineData("NotFound")]
    [InlineData("UpstreamUnavailable")]
    [InlineData("UnsupportedBackend")]
    [InlineData("UnexpectedError")]
    public async Task ValidateBackup_ReturnsOkWithTypedOperationOutcome(string errorCode) {
        var expected = new AdminOperationResult(false, $"op-{errorCode}", $"{errorCode} outcome", errorCode);
        _ = _commandService.ValidateBackupAsync("backup-123", Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ValidateBackup("backup-123");

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task TriggerRestore_ReturnsOkWithTypedResult_WhenOperationDeferred() {
        var expected = new AdminOperationResult(false, "deferred-backup-restore", "Restore is deferred.", "Deferred");
        _ = _commandService.TriggerRestoreAsync("backup-123", Arg.Any<DateTimeOffset?>(), false, Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.TriggerRestore("backup-123", null, false);

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task TriggerRestore_Returns500_WhenNullResult() {
        _ = _commandService.TriggerRestoreAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((AdminOperationResult)null!);

        IActionResult result = await _sut.TriggerRestore("backup-123", null, false);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    // === ExportStream (SEC-2: manual tenant validation) ===

    [Fact]
    public async Task ExportStream_AdminRole_AllowsAnyTenant() {
        var expected = new StreamExportResult(true, "tenant-b", "Counter", "c-1", 10, "{}", "export.json", null);
        _ = _commandService.ExportStreamAsync(Arg.Any<StreamExportRequest>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ExportStream(
            new StreamExportRequest("tenant-b", "Counter", "c-1"));

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task ExportStream_NonAdminWithMatchingTenant_Succeeds() {
        _sut.ControllerContext.HttpContext.User = CreatePrincipal("ReadOnly", "tenant-a");
        var expected = new StreamExportResult(true, "tenant-a", "Counter", "c-1", 10, "{}", "export.json", null);
        _ = _commandService.ExportStreamAsync(Arg.Any<StreamExportRequest>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ExportStream(
            new StreamExportRequest("tenant-a", "Counter", "c-1"));

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task ExportStream_NonAdminWithMismatchedTenant_Returns403() {
        _sut.ControllerContext.HttpContext.User = CreatePrincipal("ReadOnly", "tenant-a");

        IActionResult result = await _sut.ExportStream(
            new StreamExportRequest("tenant-b", "Counter", "c-1"));

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    // === ImportStream (SEC-2: manual tenant validation) ===

    [Fact]
    public async Task ImportStream_AdminRole_AllowsAnyTenant() {
        var expected = new AdminOperationResult(true, "op-5", "Import done", null);
        _ = _commandService.ImportStreamAsync("tenant-b", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ImportStream("tenant-b", "{}");

        AcceptedResult acceptedResult = result.ShouldBeOfType<AcceptedResult>();
        acceptedResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task ImportStream_NonAdminWithMatchingTenant_Succeeds() {
        _sut.ControllerContext.HttpContext.User = CreatePrincipal("ReadOnly", "tenant-a");
        var expected = new AdminOperationResult(true, "op-5", "Import done", null);
        _ = _commandService.ImportStreamAsync("tenant-a", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ImportStream("tenant-a", "{}");
        _ = result.ShouldBeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task ImportStream_ReturnsOkWithTypedResult_WhenOperationDeferred() {
        var expected = new AdminOperationResult(false, "deferred-backup-import-stream", "Stream import is deferred.", "Deferred");
        _ = _commandService.ImportStreamAsync("tenant-a", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ImportStream("tenant-a", "{}");

        OkObjectResult okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task ImportStream_NonAdminWithMismatchedTenant_Returns403() {
        _sut.ControllerContext.HttpContext.User = CreatePrincipal("ReadOnly", "tenant-a");

        IActionResult result = await _sut.ImportStream("tenant-b", "{}");

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    // === Story 22.7c admission/workflow endpoints ===

    [Fact]
    public async Task SubmitAdmission_InvalidRequest_Returns400() {
        RestoredBackupAdmissionRequest request = ValidAdmissionRequest() with { ToSequence = -1 };

        IActionResult result = await _sut.SubmitAdmission(request);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task SubmitAdmission_DeferredValidation_ReturnsSafeProblemDetails() {
        RestoredBackupAdmissionRequest request = ValidAdmissionRequest();
        var admission = new RestoredBackupAdmissionResult(
            request.AdmissionId,
            RestoredBackupAdmissionState.DeferredValidation,
            request.TenantId,
            request.Domain,
            request.AggregateId,
            request.FromSequence,
            request.ToSequence,
            request.BackupManifestId,
            request.ProtectionMetadataVersion,
            request.KeyReferencePolicy,
            request.KeyAliasFingerprint,
            "backup-engine-deferred",
            RestoredBackupAdmissionResult.DeferredValidationCode,
            CryptoShreddingNextAction.ProvideRestoreEvidence,
            request.CorrelationId,
            null,
            request.OperatorActorId,
            DateTimeOffset.UtcNow,
            false);
        _ = _commandService.AdmitRestoredBackupAsync(request, Arg.Any<CancellationToken>())
            .Returns(admission);

        IActionResult result = await _sut.SubmitAdmission(request);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
        ProblemDetails problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Extensions["admissionId"].ShouldBe(request.AdmissionId);
        ProtectedDataLeakSentinel.AssertNoLeak(problem.Extensions.Values.Select(static v => v?.ToString()));
    }

    [Fact]
    public async Task GetAdmission_NonAdminMismatchedTenant_Returns403WithoutLookup() {
        _sut.ControllerContext.HttpContext.User = CreatePrincipal("ReadOnly", "tenant-a");

        IActionResult result = await _sut.GetAdmission("tenant-b", "admission-1");

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        _ = await _queryService.DidNotReceiveWithAnyArgs()
            .GetRestoreAdmissionAsync(default!, default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitAdmissionDecision_PassesTenantAndDecisionToService() {
        var admission = new RestoredBackupAdmissionResult(
            "admission-1",
            RestoredBackupAdmissionState.Accepted,
            "tenant-a",
            "orders",
            "agg-1",
            0,
            10,
            "manifest-1",
            1,
            KeyReferencePolicy.NoKeyReference,
            null,
            null,
            RestoredBackupAdmissionResult.AcceptedCode,
            CryptoShreddingNextAction.None,
            null,
            "audit-1",
            "operator",
            DateTimeOffset.UtcNow,
            false);
        _ = _commandService.SubmitRestoreAdmissionDecisionAsync(
                "tenant-a",
                "admission-1",
                RestoredBackupAdmissionState.Accepted,
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(admission);

        IActionResult result = await _sut.SubmitAdmissionDecision("tenant-a", "admission-1", RestoredBackupAdmissionState.Accepted);

        OkObjectResult ok = result.ShouldBeOfType<OkObjectResult>();
        ok.Value.ShouldBe(admission);
    }

    [Fact]
    public async Task SubmitCryptoShreddingWorkflow_ReturnsAcceptedDecision() {
        CryptoShreddingWorkflowRequest request = ValidWorkflowRequest();
        var decision = new CryptoShreddingWorkflowDecision(
            request.Identity,
            CryptoShreddingWorkflowState.Requested,
            CryptoShreddingWorkflowDecision.ReasonCodeFor(CryptoShreddingWorkflowState.Requested),
            CryptoShreddingWorkflowDecision.NextActionFor(CryptoShreddingWorkflowState.Requested),
            request.CorrelationId,
            "audit-1",
            request.OperatorActorId,
            DateTimeOffset.UtcNow,
            false,
            false);
        _ = _commandService.SubmitCryptoShreddingWorkflowAsync(request, Arg.Any<CancellationToken>())
            .Returns(decision);

        IActionResult result = await _sut.SubmitCryptoShreddingWorkflow(request);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        objectResult.Value.ShouldBe(decision);
    }

    [Fact]
    public async Task SubmitCryptoShreddingWorkflow_NonAdminMismatchedTenant_Returns403() {
        _sut.ControllerContext.HttpContext.User = CreatePrincipal("ReadOnly", "tenant-a");
        CryptoShreddingWorkflowRequest request = ValidWorkflowRequest() with {
            Identity = ValidWorkflowRequest().Identity with { TenantId = "tenant-b" },
        };

        IActionResult result = await _sut.SubmitCryptoShreddingWorkflow(request);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    // === Error code mapping ===

    [Fact]
    public async Task MapOperationResult_Unauthorized_Returns403() {
        var failed = new AdminOperationResult(false, "op-1", "Denied", "Unauthorized");
        _ = _commandService.TriggerBackupAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(failed);

        IActionResult result = await _sut.TriggerBackup("tenant-a", null, true);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task MapOperationResult_UnknownErrorCode_Returns500() {
        var failed = new AdminOperationResult(false, "op-1", "Unknown", "SomethingElse");
        _ = _commandService.TriggerBackupAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(failed);

        IActionResult result = await _sut.TriggerBackup("tenant-a", null, true);

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    // === ProblemDetails format ===

    [Fact]
    public async Task ErrorResponse_ContainsProblemDetails_WithCorrelationId() {
        _ = _queryService.GetBackupJobsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Unexpected"));

        IActionResult result = await _sut.GetBackupJobs("tenant-a");

        ObjectResult objectResult = result.ShouldBeOfType<ObjectResult>();
        ProblemDetails problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Extensions.ShouldContainKey("correlationId");
    }

    private static RestoredBackupAdmissionRequest ValidAdmissionRequest()
        => new(
            AdmissionId: "01HKADADADADADADADADADADAD",
            TenantId: "tenant-a",
            Domain: "orders",
            AggregateId: "agg-1",
            FromSequence: 0,
            ToSequence: 10,
            BackupManifestId: "manifest-1",
            BackupCreatedAtUtc: DateTimeOffset.UtcNow,
            RestoreRequestedAtUtc: DateTimeOffset.UtcNow,
            ProtectionMetadataVersion: 1,
            KeyReferencePolicy: KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint: null,
            DeletionWatermarkUtc: null,
            CorrelationId: "01HKCORR",
            OperatorActorId: "operator");

    private static CryptoShreddingWorkflowRequest ValidWorkflowRequest() {
        var identity = new CryptoShreddingWorkflowIdentity(
            WorkflowId: "01HKAAAAAAAAAAAAAAAAAAAAAA",
            TenantId: "tenant-a",
            Domain: "orders",
            Scope: CryptoShreddingWorkflowScope.Aggregate,
            AggregateId: "agg-1",
            FromSequence: null,
            ToSequence: null,
            KeyReferencePolicy: KeyReferencePolicy.NoKeyReference,
            KeyAliasFingerprint: null);
        return new CryptoShreddingWorkflowRequest(
            identity,
            CryptoShreddingWorkflowState.Deleted,
            "operator",
            "01HKCORR",
            DateTimeOffset.UtcNow);
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole, params string[] tenants) {
        var claims = new List<Claim> { new(AdminClaimTypes.AdminRole, adminRole) };
        foreach (string tenant in tenants) {
            claims.Add(new Claim(AdminClaimTypes.Tenant, tenant));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
