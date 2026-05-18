using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Contracts.Problems;
using Hexalith.EventStore.Contracts.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.Server.Controllers;

/// <summary>
/// REST API controller for backup and restore operations.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/admin/backups")]
[Tags("Admin - Backups")]
public class AdminBackupsController(
    IBackupQueryService backupQueryService,
    IBackupCommandService backupCommandService,
    ILogger<AdminBackupsController> logger) : ControllerBase {
    /// <summary>
    /// Gets backup jobs, optionally filtered by tenant.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(IReadOnlyList<BackupJob>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetBackupJobs(
        [FromQuery] string? tenantId,
        CancellationToken ct = default) {
        try {
            string? effectiveTenantId = ResolveTenantScope(tenantId);
            IReadOnlyList<BackupJob> result = await backupQueryService
                .GetBackupJobsAsync(effectiveTenantId, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(GetBackupJobs), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(GetBackupJobs), ex);
        }
    }

    /// <summary>
    /// Triggers a full tenant backup.
    /// </summary>
    [HttpPost("{tenantId:regex(^(?!export-stream$|import-stream$).+$)}")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> TriggerBackup(
        string tenantId,
        [FromQuery] string? description,
        [FromQuery] bool includeSnapshots = true,
        CancellationToken ct = default) {
        try {
            AdminOperationResult result = await backupCommandService
                .TriggerBackupAsync(tenantId, description, includeSnapshots, ct)
                .ConfigureAwait(false);
            return MapAsyncOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(TriggerBackup), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(TriggerBackup), ex);
        }
    }

    /// <summary>
    /// Validates integrity of a completed backup.
    /// </summary>
    [HttpPost("{backupId}/validate")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ValidateBackup(
        string backupId,
        CancellationToken ct = default) {
        try {
            AdminOperationResult result = await backupCommandService
                .ValidateBackupAsync(backupId, ct)
                .ConfigureAwait(false);
            return MapAsyncOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(ValidateBackup), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(ValidateBackup), ex);
        }
    }

    /// <summary>
    /// Initiates a restore from a backup.
    /// </summary>
    [HttpPost("{backupId}/restore")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> TriggerRestore(
        string backupId,
        [FromQuery] DateTimeOffset? pointInTime,
        [FromQuery] bool dryRun = false,
        CancellationToken ct = default) {
        try {
            AdminOperationResult result = await backupCommandService
                .TriggerRestoreAsync(backupId, pointInTime, dryRun, ct)
                .ConfigureAwait(false);
            return MapAsyncOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(TriggerRestore), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(TriggerRestore), ex);
        }
    }

    /// <summary>
    /// Exports a single stream as downloadable content.
    /// </summary>
    [HttpPost("export-stream")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(StreamExportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ExportStream(
        [FromBody] StreamExportRequest request,
        CancellationToken ct = default) {
        try {
            ArgumentNullException.ThrowIfNull(request);

            // SEC-2: Validate tenant access from request body (filter only checks route/query params)
            if (!User.HasClaim(AdminClaimTypes.AdminRole, nameof(AdminRole.Admin))
                && !User.HasClaim(AdminClaimTypes.Tenant, request.TenantId)) {
                return CreateProblemResult(
                    StatusCodes.Status403Forbidden,
                    "Tenant Access Denied",
                    "Not authorized to export streams for the requested tenant.");
            }

            StreamExportResult result = await backupCommandService
                .ExportStreamAsync(request, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(ExportStream), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(ExportStream), ex);
        }
    }

    /// <summary>
    /// Imports events into a stream from exported content.
    /// </summary>
    [HttpPost("import-stream")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ImportStream(
        [FromQuery] string tenantId,
        [FromBody] string content,
        CancellationToken ct = default) {
        try {
            // SEC-2: Validate tenant access from query param (filter only checks route params)
            if (!User.HasClaim(AdminClaimTypes.AdminRole, nameof(AdminRole.Admin))
                && !User.HasClaim(AdminClaimTypes.Tenant, tenantId)) {
                return CreateProblemResult(
                    StatusCodes.Status403Forbidden,
                    "Tenant Access Denied",
                    "Not authorized to import streams for the requested tenant.");
            }

            AdminOperationResult result = await backupCommandService
                .ImportStreamAsync(tenantId, content, ct)
                .ConfigureAwait(false);
            return MapAsyncOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(ImportStream), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(ImportStream), ex);
        }
    }

    /// <summary>
    /// Story 22.7c — submits a restored-backup admission request. The response carries a safe
    /// admission status with reason code, watermark conflict description, and operator next action.
    /// Until the backup engine lands, the implementation returns <c>DeferredValidation</c>: callers
    /// must NOT serve protected content based on the response.
    /// </summary>
    /// <param name="request">The admission request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The admission result or a ProblemDetails describing the conflict.</returns>
    [HttpPost("admissions")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(RestoredBackupAdmissionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SubmitAdmission(
        [FromBody] RestoredBackupAdmissionRequest request,
        CancellationToken ct = default) {
        try {
            ArgumentNullException.ThrowIfNull(request);
            if (!request.TryValidate(out string? rejectionReason)) {
                return CreateProblemResult(
                    StatusCodes.Status400BadRequest,
                    "Invalid Admission Request",
                    rejectionReason);
            }

            // SEC-2: validate tenant access from request body
            if (!User.HasClaim(AdminClaimTypes.AdminRole, nameof(AdminRole.Admin))
                && !User.HasClaim(AdminClaimTypes.Tenant, request.TenantId)) {
                return CreateProblemResult(
                    StatusCodes.Status403Forbidden,
                    "Tenant Access Denied",
                    "Not authorized to admit restored backups for the requested tenant.");
            }

            RestoredBackupAdmissionResult result = await backupCommandService
                .AdmitRestoredBackupAsync(request, ct)
                .ConfigureAwait(false);
            return MapAdmissionResult(result);
        }
        catch (ArgumentException ex) {
            return CreateProblemResult(StatusCodes.Status400BadRequest, "Invalid Admission Request", ex.Message);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(SubmitAdmission), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(SubmitAdmission), ex);
        }
    }

    /// <summary>
    /// Story 22.7c — records an operator decision for an existing admission record.
    /// </summary>
    /// <param name="tenantId">The tenant scope of the admission.</param>
    /// <param name="admissionId">The admission identifier.</param>
    /// <param name="decision">The operator decision.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated admission result.</returns>
    [HttpPost("admissions/{tenantId}/{admissionId}/decision")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(RestoredBackupAdmissionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SubmitAdmissionDecision(
        string tenantId,
        string admissionId,
        [FromQuery] RestoredBackupAdmissionState decision,
        CancellationToken ct = default) {
        try {
            if (!CanAccessTenant(tenantId)) {
                return TenantDenied("submit restored-backup admission decisions for");
            }

            string operatorActorId = User.FindFirst("sub")?.Value ?? "anonymous";
            RestoredBackupAdmissionResult result = await backupCommandService
                .SubmitRestoreAdmissionDecisionAsync(tenantId, admissionId, decision, operatorActorId, ct)
                .ConfigureAwait(false);
            return MapAdmissionResult(result);
        }
        catch (ArgumentException ex) {
            return CreateProblemResult(StatusCodes.Status400BadRequest, "Invalid Admission Decision", ex.Message);
        }
        catch (KeyNotFoundException ex) {
            return CreateProblemResult(StatusCodes.Status404NotFound, "Admission Not Found", ex.Message);
        }
        catch (InvalidOperationException ex) {
            return CreateProblemResult(StatusCodes.Status409Conflict, "Admission Conflict", ex.Message);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(SubmitAdmissionDecision), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(SubmitAdmissionDecision), ex);
        }
    }

    /// <summary>
    /// Story 22.7c — gets the current status of a restored-backup admission record.
    /// </summary>
    /// <param name="tenantId">The tenant scope of the admission.</param>
    /// <param name="admissionId">The admission identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The admission status, or 404 when no record exists.</returns>
    [HttpGet("admissions/{tenantId}/{admissionId}")]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ProducesResponseType(typeof(RestoredBackupAdmissionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAdmission(
        string tenantId,
        string admissionId,
        CancellationToken ct = default) {
        try {
            if (!CanAccessTenant(tenantId)) {
                return TenantDenied("read restored-backup admissions for");
            }

            RestoredBackupAdmissionResult? result = await backupQueryService
                .GetRestoreAdmissionAsync(tenantId, admissionId, ct)
                .ConfigureAwait(false);
            if (result is null) {
                return CreateProblemResult(
                    StatusCodes.Status404NotFound,
                    "Admission Not Found",
                    "No restored-backup admission record exists with the supplied identifier.");
            }

            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(GetAdmission), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(GetAdmission), ex);
        }
    }

    /// <summary>
    /// Story 22.7c — records an operator-initiated crypto-shredding workflow request.
    /// </summary>
    [HttpPost("crypto-shredding/workflows")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ProducesResponseType(typeof(CryptoShreddingWorkflowDecision), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitCryptoShreddingWorkflow(
        [FromBody] CryptoShreddingWorkflowRequest request,
        CancellationToken ct = default) {
        try {
            ArgumentNullException.ThrowIfNull(request);
            if (!request.TryValidate(out string? rejectionReason)) {
                return CreateProblemResult(
                    StatusCodes.Status400BadRequest,
                    "Invalid Crypto-Shredding Workflow Request",
                    rejectionReason);
            }

            if (!CanAccessTenant(request.Identity.TenantId)) {
                return TenantDenied("submit crypto-shredding workflows for");
            }

            CryptoShreddingWorkflowDecision result = await backupCommandService
                .SubmitCryptoShreddingWorkflowAsync(request, ct)
                .ConfigureAwait(false);
            return MapWorkflowResult(result);
        }
        catch (ArgumentException ex) {
            return CreateProblemResult(StatusCodes.Status400BadRequest, "Invalid Crypto-Shredding Workflow Request", ex.Message);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(SubmitCryptoShreddingWorkflow), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(SubmitCryptoShreddingWorkflow), ex);
        }
    }

    /// <summary>
    /// Story 22.7c — gets the current status of a crypto-shredding workflow record.
    /// </summary>
    [HttpGet("crypto-shredding/workflows/{tenantId}/{workflowId}")]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ProducesResponseType(typeof(CryptoShreddingWorkflowDecision), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCryptoShreddingWorkflow(
        string tenantId,
        string workflowId,
        CancellationToken ct = default) {
        try {
            if (!CanAccessTenant(tenantId)) {
                return TenantDenied("read crypto-shredding workflows for");
            }

            CryptoShreddingWorkflowDecision? result = await backupQueryService
                .GetCryptoShreddingWorkflowAsync(tenantId, workflowId, ct)
                .ConfigureAwait(false);
            if (result is null) {
                return CreateProblemResult(
                    StatusCodes.Status404NotFound,
                    "Workflow Not Found",
                    "No crypto-shredding workflow record exists with the supplied identifier.");
            }

            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(GetCryptoShreddingWorkflow), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(GetCryptoShreddingWorkflow), ex);
        }
    }

    private IActionResult MapAdmissionResult(RestoredBackupAdmissionResult result) {
        ArgumentNullException.ThrowIfNull(result);
        int statusCode = RestoredBackupAdmissionProblem.GetStatusCode(result.State);
        if (result.State == RestoredBackupAdmissionState.Accepted) {
            return Ok(result);
        }

        if (result.State == RestoredBackupAdmissionState.Pending) {
            return StatusCode(statusCode, result);
        }

        // Conflict states return a ProblemDetails surface carrying the safe admission metadata.
        var problem = new ProblemDetails {
            Status = statusCode,
            Type = RestoredBackupAdmissionProblem.TypeUri,
            Title = RestoredBackupAdmissionProblem.DefaultTitle,
            Detail = RestoredBackupAdmissionProblem.GetSafeOperatorGuidance(result.State),
            Instance = HttpContext.Request.Path,
        };
        problem.Extensions[RestoredBackupAdmissionProblem.ExtensionAdmissionId] = result.AdmissionId;
        problem.Extensions[RestoredBackupAdmissionProblem.ExtensionAdmissionState] = result.State.ToString();
        problem.Extensions[RestoredBackupAdmissionProblem.ExtensionReasonCode] = result.ReasonCode;
        problem.Extensions[RestoredBackupAdmissionProblem.ExtensionNextAction] = result.NextAction.ToString();
        problem.Extensions[RestoredBackupAdmissionProblem.ExtensionTenantId] = result.TenantId;
        problem.Extensions[RestoredBackupAdmissionProblem.ExtensionDomain] = result.Domain;
        problem.Extensions[RestoredBackupAdmissionProblem.ExtensionBackupManifestId] = result.BackupManifestId;
        problem.Extensions[RestoredBackupAdmissionProblem.ExtensionMetadataVersion] = result.ProtectionMetadataVersion;
        if (!string.IsNullOrWhiteSpace(result.WatermarkConflict)) {
            problem.Extensions[RestoredBackupAdmissionProblem.ExtensionWatermarkConflict] = result.WatermarkConflict;
        }

        if (!string.IsNullOrWhiteSpace(result.CorrelationId)) {
            problem.Extensions[RestoredBackupAdmissionProblem.ExtensionCorrelationId] = result.CorrelationId;
        }

        if (!string.IsNullOrWhiteSpace(result.AuditId)) {
            problem.Extensions[RestoredBackupAdmissionProblem.ExtensionAuditId] = result.AuditId;
        }

        if (statusCode == StatusCodes.Status503ServiceUnavailable) {
            Response.Headers.RetryAfter = "5";
        }

        return StatusCode(statusCode, problem);
    }

    private IActionResult MapWorkflowResult(CryptoShreddingWorkflowDecision result) {
        ArgumentNullException.ThrowIfNull(result);
        int statusCode = CryptoShreddingWorkflowProblem.GetStatusCode(result.State);
        if (statusCode == StatusCodes.Status202Accepted) {
            return StatusCode(statusCode, result);
        }

        var problem = new ProblemDetails {
            Status = statusCode,
            Type = CryptoShreddingWorkflowProblem.TypeUri,
            Title = CryptoShreddingWorkflowProblem.DefaultTitle,
            Detail = CryptoShreddingWorkflowProblem.GetSafeOperatorGuidance(result.State),
            Instance = HttpContext.Request.Path,
        };
        problem.Extensions[CryptoShreddingWorkflowProblem.ExtensionWorkflowId] = result.Identity.WorkflowId;
        problem.Extensions[CryptoShreddingWorkflowProblem.ExtensionWorkflowState] = result.State.ToString();
        problem.Extensions[CryptoShreddingWorkflowProblem.ExtensionReasonCode] = result.ReasonCode;
        problem.Extensions[CryptoShreddingWorkflowProblem.ExtensionNextAction] = result.NextAction.ToString();
        problem.Extensions[CryptoShreddingWorkflowProblem.ExtensionTenantId] = result.Identity.TenantId;
        problem.Extensions[CryptoShreddingWorkflowProblem.ExtensionDomain] = result.Identity.Domain;
        problem.Extensions[CryptoShreddingWorkflowProblem.ExtensionIrreversibleDecisionRecorded] = result.IrreversibleDecisionRecorded;
        if (!string.IsNullOrWhiteSpace(result.Identity.AggregateId)) {
            problem.Extensions[CryptoShreddingWorkflowProblem.ExtensionAggregateId] = result.Identity.AggregateId;
        }

        if (result.Identity.FromSequence.HasValue) {
            problem.Extensions[CryptoShreddingWorkflowProblem.ExtensionFromSequence] = result.Identity.FromSequence.Value;
        }

        if (result.Identity.ToSequence.HasValue) {
            problem.Extensions[CryptoShreddingWorkflowProblem.ExtensionToSequence] = result.Identity.ToSequence.Value;
        }

        if (!string.IsNullOrWhiteSpace(result.CorrelationId)) {
            problem.Extensions[CryptoShreddingWorkflowProblem.ExtensionCorrelationId] = result.CorrelationId;
        }

        if (!string.IsNullOrWhiteSpace(result.AuditId)) {
            problem.Extensions[CryptoShreddingWorkflowProblem.ExtensionAuditId] = result.AuditId;
        }

        return StatusCode(statusCode, problem);
    }

    private bool CanAccessTenant(string tenantId)
        => User.HasClaim(AdminClaimTypes.AdminRole, nameof(AdminRole.Admin))
            || User.HasClaim(AdminClaimTypes.Tenant, tenantId);

    private ObjectResult TenantDenied(string action)
        => CreateProblemResult(
            StatusCodes.Status403Forbidden,
            "Tenant Access Denied",
            $"Not authorized to {action} the requested tenant.");

    private string? ResolveTenantScope(string? requestedTenantId) {
        if (requestedTenantId is not null) {
            return requestedTenantId;
        }

        if (User.HasClaim(AdminClaimTypes.AdminRole, nameof(AdminRole.Admin))) {
            return null;
        }

        return User.FindFirst(AdminClaimTypes.Tenant)?.Value;
    }

    private IActionResult MapAsyncOperationResult(AdminOperationResult? result) {
        if (result is null) {
            return CreateProblemResult(StatusCodes.Status500InternalServerError, "Internal Server Error", "No result returned from the service.");
        }

        if (result.Success) {
            return Accepted(result);
        }

        if (IsTypedBusinessOutcome(result.ErrorCode)) {
            return Ok(result);
        }

        return result.ErrorCode switch {
            "NotFound" => CreateProblemResult(StatusCodes.Status404NotFound, "Not Found", result.Message),
            "Unauthorized" => CreateProblemResult(StatusCodes.Status403Forbidden, "Forbidden", result.Message),
            "InvalidOperation" => CreateProblemResult(StatusCodes.Status422UnprocessableEntity, "Invalid Operation", result.Message),
            _ => CreateProblemResult(StatusCodes.Status500InternalServerError, "Internal Server Error", result.Message),
        };
    }

    private static bool IsServiceUnavailable(Exception ex)
        => ex is HttpRequestException or TimeoutException
            || (ex is Grpc.Core.RpcException rpc && rpc.StatusCode is
                Grpc.Core.StatusCode.Unavailable or
                Grpc.Core.StatusCode.DeadlineExceeded or
                Grpc.Core.StatusCode.Aborted or
                Grpc.Core.StatusCode.ResourceExhausted);

    private static bool IsTypedBusinessOutcome(string? errorCode)
        => errorCode is "Deferred"
            or "Blocked"
            or "RejectedValidation"
            or "RejectedUnauthorized"
            or "NotFound"
            or "UpstreamUnavailable"
            or "UnsupportedBackend"
            or "UnexpectedError";

    private ObjectResult ServiceUnavailable(string method, Exception ex) {
        logger.LogError(ex, "Admin service unavailable: {Method}", method);
        return CreateProblemResult(
            StatusCodes.Status503ServiceUnavailable,
            "Service Unavailable",
            "The admin backend service is temporarily unavailable. Retry shortly.");
    }

    private ObjectResult UnexpectedError(string method, Exception ex) {
        logger.LogError(ex, "Unexpected error in {Method}", method);
        return CreateProblemResult(
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            "An unexpected error occurred.");
    }

    private ObjectResult CreateProblemResult(int statusCode, string title, string? detail = null) {
        string correlationId = HttpContext.Items["CorrelationId"]?.ToString()
            ?? Guid.NewGuid().ToString();
        return new ObjectResult(new ProblemDetails {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Extensions = { ["correlationId"] = correlationId },
        }) { StatusCode = statusCode };
    }
}
