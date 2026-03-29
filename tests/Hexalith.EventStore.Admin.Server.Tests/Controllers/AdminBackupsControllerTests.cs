using System.Security.Claims;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Controllers;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Hexalith.EventStore.Admin.Server.Tests.Controllers;

public class AdminBackupsControllerTests
{
    private readonly IBackupQueryService _queryService = Substitute.For<IBackupQueryService>();
    private readonly IBackupCommandService _commandService = Substitute.For<IBackupCommandService>();
    private readonly AdminBackupsController _sut;

    public AdminBackupsControllerTests()
    {
        _sut = new AdminBackupsController(_queryService, _commandService, NullLogger<AdminBackupsController>.Instance);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = CreatePrincipal("Admin"),
            },
        };
    }

    // === GetBackupJobs ===

    [Fact]
    public async Task GetBackupJobs_ReturnsOk_WithBackupJobs()
    {
        IReadOnlyList<BackupJob> expected = [];
        _queryService.GetBackupJobsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetBackupJobs("tenant-a");

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetBackupJobs_AdminWithNullTenantId_PassesNullToService()
    {
        IReadOnlyList<BackupJob> expected = [];
        _queryService.GetBackupJobsAsync(null, Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetBackupJobs(null);

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        await _queryService.Received(1).GetBackupJobsAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBackupJobs_NonAdminWithNullTenantId_ResolvesToUserTenant()
    {
        _sut.ControllerContext.HttpContext.User = CreatePrincipal("ReadOnly", "tenant-x");
        IReadOnlyList<BackupJob> expected = [];
        _queryService.GetBackupJobsAsync("tenant-x", Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.GetBackupJobs(null);

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        await _queryService.Received(1).GetBackupJobsAsync("tenant-x", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetBackupJobs_ServiceThrowsHttpRequestException_Returns503()
    {
        _queryService.GetBackupJobsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        IActionResult result = await _sut.GetBackupJobs("tenant-a");

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task GetBackupJobs_ServiceThrowsRpcException_Returns503()
    {
        _queryService.GetBackupJobsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "test")));

        IActionResult result = await _sut.GetBackupJobs("tenant-a");

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public async Task GetBackupJobs_ServiceThrowsUnexpectedException_Returns500()
    {
        _queryService.GetBackupJobsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Unexpected"));

        IActionResult result = await _sut.GetBackupJobs("tenant-a");

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    // === TriggerBackup ===

    [Fact]
    public async Task TriggerBackup_ReturnsAccepted_WhenServiceSucceeds()
    {
        var expected = new AdminOperationResult(true, "op-1", "Backup started", null);
        _commandService.TriggerBackupAsync("tenant-a", "nightly", true, Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.TriggerBackup("tenant-a", "nightly", true);

        var acceptedResult = result.ShouldBeOfType<AcceptedResult>();
        acceptedResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task TriggerBackup_ReturnsErrorCode_WhenServiceFails()
    {
        var failed = new AdminOperationResult(false, "op-1", "Not found", "NotFound");
        _commandService.TriggerBackupAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(failed);

        IActionResult result = await _sut.TriggerBackup("tenant-a", null, true);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task TriggerBackup_ReturnsUnprocessable_WhenInvalidOperation()
    {
        var failed = new AdminOperationResult(false, "op-1", "Already running", "InvalidOperation");
        _commandService.TriggerBackupAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(failed);

        IActionResult result = await _sut.TriggerBackup("tenant-a", null, true);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task TriggerBackup_Returns503_WhenServiceUnavailable()
    {
        _commandService.TriggerBackupAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("Timed out"));

        IActionResult result = await _sut.TriggerBackup("tenant-a", null, true);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status503ServiceUnavailable);
    }

    // === ValidateBackup ===

    [Fact]
    public async Task ValidateBackup_ReturnsAccepted_WhenServiceSucceeds()
    {
        var expected = new AdminOperationResult(true, "op-2", "Valid", null);
        _commandService.ValidateBackupAsync("backup-123", Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ValidateBackup("backup-123");

        var acceptedResult = result.ShouldBeOfType<AcceptedResult>();
        acceptedResult.Value.ShouldBe(expected);
    }

    // === TriggerRestore ===

    [Fact]
    public async Task TriggerRestore_ReturnsAccepted_WhenServiceSucceeds()
    {
        var expected = new AdminOperationResult(true, "op-3", "Restore started", null);
        _commandService.TriggerRestoreAsync("backup-123", Arg.Any<DateTimeOffset?>(), false, Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.TriggerRestore("backup-123", null, false);

        var acceptedResult = result.ShouldBeOfType<AcceptedResult>();
        acceptedResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task TriggerRestore_Returns500_WhenNullResult()
    {
        _commandService.TriggerRestoreAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((AdminOperationResult)null!);

        IActionResult result = await _sut.TriggerRestore("backup-123", null, false);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    // === ExportStream (SEC-2: manual tenant validation) ===

    [Fact]
    public async Task ExportStream_AdminRole_AllowsAnyTenant()
    {
        var expected = new StreamExportResult(true, "tenant-b", "Counter", "c-1", 10, "{}", "export.json", null);
        _commandService.ExportStreamAsync(Arg.Any<StreamExportRequest>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ExportStream(
            new StreamExportRequest("tenant-b", "Counter", "c-1"));

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task ExportStream_NonAdminWithMatchingTenant_Succeeds()
    {
        _sut.ControllerContext.HttpContext.User = CreatePrincipal("ReadOnly", "tenant-a");
        var expected = new StreamExportResult(true, "tenant-a", "Counter", "c-1", 10, "{}", "export.json", null);
        _commandService.ExportStreamAsync(Arg.Any<StreamExportRequest>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ExportStream(
            new StreamExportRequest("tenant-a", "Counter", "c-1"));

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task ExportStream_NonAdminWithMismatchedTenant_Returns403()
    {
        _sut.ControllerContext.HttpContext.User = CreatePrincipal("ReadOnly", "tenant-a");

        IActionResult result = await _sut.ExportStream(
            new StreamExportRequest("tenant-b", "Counter", "c-1"));

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    // === ImportStream (SEC-2: manual tenant validation) ===

    [Fact]
    public async Task ImportStream_AdminRole_AllowsAnyTenant()
    {
        var expected = new AdminOperationResult(true, "op-5", "Import done", null);
        _commandService.ImportStreamAsync("tenant-b", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ImportStream("tenant-b", "{}");

        var acceptedResult = result.ShouldBeOfType<AcceptedResult>();
        acceptedResult.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task ImportStream_NonAdminWithMatchingTenant_Succeeds()
    {
        _sut.ControllerContext.HttpContext.User = CreatePrincipal("ReadOnly", "tenant-a");
        var expected = new AdminOperationResult(true, "op-5", "Import done", null);
        _commandService.ImportStreamAsync("tenant-a", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        IActionResult result = await _sut.ImportStream("tenant-a", "{}");

        var acceptedResult = result.ShouldBeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task ImportStream_NonAdminWithMismatchedTenant_Returns403()
    {
        _sut.ControllerContext.HttpContext.User = CreatePrincipal("ReadOnly", "tenant-a");

        IActionResult result = await _sut.ImportStream("tenant-b", "{}");

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    // === Error code mapping ===

    [Fact]
    public async Task MapOperationResult_Unauthorized_Returns403()
    {
        var failed = new AdminOperationResult(false, "op-1", "Denied", "Unauthorized");
        _commandService.TriggerBackupAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(failed);

        IActionResult result = await _sut.TriggerBackup("tenant-a", null, true);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task MapOperationResult_UnknownErrorCode_Returns500()
    {
        var failed = new AdminOperationResult(false, "op-1", "Unknown", "SomethingElse");
        _commandService.TriggerBackupAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(failed);

        IActionResult result = await _sut.TriggerBackup("tenant-a", null, true);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    // === ProblemDetails format ===

    [Fact]
    public async Task ErrorResponse_ContainsProblemDetails_WithCorrelationId()
    {
        _queryService.GetBackupJobsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Unexpected"));

        IActionResult result = await _sut.GetBackupJobs("tenant-a");

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        var problemDetails = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problemDetails.Extensions.ShouldContainKey("correlationId");
    }

    private static ClaimsPrincipal CreatePrincipal(string adminRole, params string[] tenants)
    {
        var claims = new List<Claim> { new(AdminClaimTypes.AdminRole, adminRole) };
        foreach (string tenant in tenants)
        {
            claims.Add(new Claim(AdminClaimTypes.Tenant, tenant));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }
}
