using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;

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
    ILogger<AdminBackupsController> logger) : ControllerBase
{
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
        CancellationToken ct = default)
    {
        try
        {
            string? effectiveTenantId = ResolveTenantScope(tenantId);
            IReadOnlyList<BackupJob> result = await backupQueryService
                .GetBackupJobsAsync(effectiveTenantId, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(GetBackupJobs), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(GetBackupJobs), ex);
        }
    }

    /// <summary>
    /// Triggers a full tenant backup.
    /// </summary>
    [HttpPost("{tenantId:regex(^(?!export-stream$|import-stream$).+$)}")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> TriggerBackup(
        string tenantId,
        [FromQuery] string? description,
        [FromQuery] bool includeSnapshots = true,
        CancellationToken ct = default)
    {
        try
        {
            AdminOperationResult result = await backupCommandService
                .TriggerBackupAsync(tenantId, description, includeSnapshots, ct)
                .ConfigureAwait(false);
            return MapAsyncOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(TriggerBackup), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(TriggerBackup), ex);
        }
    }

    /// <summary>
    /// Validates integrity of a completed backup.
    /// </summary>
    [HttpPost("{backupId}/validate")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ValidateBackup(
        string backupId,
        CancellationToken ct = default)
    {
        try
        {
            AdminOperationResult result = await backupCommandService
                .ValidateBackupAsync(backupId, ct)
                .ConfigureAwait(false);
            return MapAsyncOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(ValidateBackup), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(ValidateBackup), ex);
        }
    }

    /// <summary>
    /// Initiates a restore from a backup.
    /// </summary>
    [HttpPost("{backupId}/restore")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> TriggerRestore(
        string backupId,
        [FromQuery] DateTimeOffset? pointInTime,
        [FromQuery] bool dryRun = false,
        CancellationToken ct = default)
    {
        try
        {
            AdminOperationResult result = await backupCommandService
                .TriggerRestoreAsync(backupId, pointInTime, dryRun, ct)
                .ConfigureAwait(false);
            return MapAsyncOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(TriggerRestore), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
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
        CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request);

            // SEC-2: Validate tenant access from request body (filter only checks route/query params)
            if (!User.HasClaim(AdminClaimTypes.AdminRole, nameof(AdminRole.Admin))
                && !User.HasClaim(AdminClaimTypes.Tenant, request.TenantId))
            {
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
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(ExportStream), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(ExportStream), ex);
        }
    }

    /// <summary>
    /// Imports events into a stream from exported content.
    /// </summary>
    [HttpPost("import-stream")]
    [Authorize(Policy = AdminAuthorizationPolicies.Admin)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ImportStream(
        [FromQuery] string tenantId,
        [FromBody] string content,
        CancellationToken ct = default)
    {
        try
        {
            // SEC-2: Validate tenant access from query param (filter only checks route params)
            if (!User.HasClaim(AdminClaimTypes.AdminRole, nameof(AdminRole.Admin))
                && !User.HasClaim(AdminClaimTypes.Tenant, tenantId))
            {
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
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(ImportStream), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(ImportStream), ex);
        }
    }

    private string? ResolveTenantScope(string? requestedTenantId)
    {
        if (requestedTenantId is not null)
        {
            return requestedTenantId;
        }

        if (User.HasClaim(AdminClaimTypes.AdminRole, nameof(AdminRole.Admin)))
        {
            return null;
        }

        return User.FindFirst(AdminClaimTypes.Tenant)?.Value;
    }

    private IActionResult MapAsyncOperationResult(AdminOperationResult? result)
    {
        if (result is null)
        {
            return CreateProblemResult(StatusCodes.Status500InternalServerError, "Internal Server Error", "No result returned from the service.");
        }

        if (result.Success)
        {
            return Accepted(result);
        }

        return result.ErrorCode switch
        {
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

    private ObjectResult ServiceUnavailable(string method, Exception ex)
    {
        logger.LogError(ex, "Admin service unavailable: {Method}", method);
        return CreateProblemResult(
            StatusCodes.Status503ServiceUnavailable,
            "Service Unavailable",
            "The admin backend service is temporarily unavailable. Retry shortly.");
    }

    private ObjectResult UnexpectedError(string method, Exception ex)
    {
        logger.LogError(ex, "Unexpected error in {Method}", method);
        return CreateProblemResult(
            StatusCodes.Status500InternalServerError,
            "Internal Server Error",
            "An unexpected error occurred.");
    }

    private ObjectResult CreateProblemResult(int statusCode, string title, string? detail = null)
    {
        string correlationId = HttpContext.Items["CorrelationId"]?.ToString()
            ?? Guid.NewGuid().ToString();
        return new ObjectResult(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = HttpContext.Request.Path,
            Extensions = { ["correlationId"] = correlationId },
        })
        { StatusCode = statusCode };
    }
}
