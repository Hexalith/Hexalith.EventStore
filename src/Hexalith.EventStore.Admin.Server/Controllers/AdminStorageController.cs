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
/// REST API controller for querying and managing storage, snapshots, and compaction.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/admin/storage")]
[Tags("Admin - Storage")]
public class AdminStorageController(
    IStorageQueryService storageQueryService,
    IStorageCommandService storageCommandService,
    ILogger<AdminStorageController> logger) : ControllerBase
{
    /// <summary>
    /// Gets the storage overview, optionally filtered by tenant.
    /// </summary>
    [HttpGet("overview")]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(StorageOverview), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetStorageOverview(
        [FromQuery] string? tenantId,
        CancellationToken ct = default)
    {
        try
        {
            string? effectiveTenantId = ResolveTenantScope(tenantId);
            StorageOverview result = await storageQueryService
                .GetStorageOverviewAsync(effectiveTenantId, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(GetStorageOverview), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(GetStorageOverview), ex);
        }
    }

    /// <summary>
    /// Gets the streams with the highest storage usage.
    /// </summary>
    [HttpGet("hot-streams")]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(IReadOnlyList<StreamStorageInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHotStreams(
        [FromQuery] string? tenantId,
        [FromQuery] int count = 100,
        CancellationToken ct = default)
    {
        try
        {
            string? effectiveTenantId = ResolveTenantScope(tenantId);
            IReadOnlyList<StreamStorageInfo> result = await storageQueryService
                .GetHotStreamsAsync(effectiveTenantId, count, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(GetHotStreams), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(GetHotStreams), ex);
        }
    }

    /// <summary>
    /// Gets the snapshot policies, optionally filtered by tenant.
    /// </summary>
    [HttpGet("snapshot-policies")]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(IReadOnlyList<SnapshotPolicy>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetSnapshotPolicies(
        [FromQuery] string? tenantId,
        CancellationToken ct = default)
    {
        try
        {
            string? effectiveTenantId = ResolveTenantScope(tenantId);
            IReadOnlyList<SnapshotPolicy> result = await storageQueryService
                .GetSnapshotPoliciesAsync(effectiveTenantId, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(GetSnapshotPolicies), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(GetSnapshotPolicies), ex);
        }
    }

    /// <summary>
    /// Triggers compaction for a tenant, optionally scoped to a specific domain.
    /// </summary>
    [HttpPost("{tenantId}/compact")]
    [Authorize(Policy = AdminAuthorizationPolicies.Operator)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> TriggerCompaction(
        string tenantId,
        [FromQuery] string? domain,
        CancellationToken ct = default)
    {
        try
        {
            AdminOperationResult result = await storageCommandService
                .TriggerCompactionAsync(tenantId, domain, ct)
                .ConfigureAwait(false);
            return MapAsyncOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(TriggerCompaction), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(TriggerCompaction), ex);
        }
    }

    /// <summary>
    /// Creates a snapshot for a specific aggregate.
    /// </summary>
    [HttpPost("{tenantId}/{domain}/{aggregateId}/snapshot")]
    [Authorize(Policy = AdminAuthorizationPolicies.Operator)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateSnapshot(
        string tenantId,
        string domain,
        string aggregateId,
        CancellationToken ct = default)
    {
        try
        {
            AdminOperationResult result = await storageCommandService
                .CreateSnapshotAsync(tenantId, domain, aggregateId, ct)
                .ConfigureAwait(false);
            return MapOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(CreateSnapshot), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(CreateSnapshot), ex);
        }
    }

    /// <summary>
    /// Sets the automatic snapshot policy for an aggregate type.
    /// </summary>
    [HttpPut("{tenantId}/{domain}/{aggregateType}/snapshot-policy")]
    [Authorize(Policy = AdminAuthorizationPolicies.Operator)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetSnapshotPolicy(
        string tenantId,
        string domain,
        string aggregateType,
        [FromQuery] int intervalEvents,
        CancellationToken ct = default)
    {
        try
        {
            AdminOperationResult result = await storageCommandService
                .SetSnapshotPolicyAsync(tenantId, domain, aggregateType, intervalEvents, ct)
                .ConfigureAwait(false);
            return MapOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(SetSnapshotPolicy), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(SetSnapshotPolicy), ex);
        }
    }

    /// <summary>
    /// Deletes the automatic snapshot policy for an aggregate type.
    /// </summary>
    [HttpDelete("{tenantId}/{domain}/{aggregateType}/snapshot-policy")]
    [Authorize(Policy = AdminAuthorizationPolicies.Operator)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteSnapshotPolicy(
        string tenantId,
        string domain,
        string aggregateType,
        CancellationToken ct = default)
    {
        try
        {
            AdminOperationResult result = await storageCommandService
                .DeleteSnapshotPolicyAsync(tenantId, domain, aggregateType, ct)
                .ConfigureAwait(false);
            return MapOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex))
        {
            return ServiceUnavailable(nameof(DeleteSnapshotPolicy), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UnexpectedError(nameof(DeleteSnapshotPolicy), ex);
        }
    }

    private string? ResolveTenantScope(string? requestedTenantId)
    {
        if (requestedTenantId is not null)
        {
            return requestedTenantId;
        }

        if (User.HasClaim(AdminClaimTypes.AdminRole, nameof(Abstractions.Models.Common.AdminRole.Admin)))
        {
            return null;
        }

        return User.FindFirst(AdminClaimTypes.Tenant)?.Value;
    }

    private IActionResult MapOperationResult(AdminOperationResult? result)
    {
        if (result is null)
        {
            return CreateProblemResult(StatusCodes.Status500InternalServerError, "Internal Server Error", "No result returned from the service.");
        }

        if (result.Success)
        {
            return Ok(result);
        }

        return result.ErrorCode switch
        {
            "NotFound" => CreateProblemResult(StatusCodes.Status404NotFound, "Not Found", result.Message),
            "Unauthorized" => CreateProblemResult(StatusCodes.Status403Forbidden, "Forbidden", result.Message),
            "InvalidOperation" => CreateProblemResult(StatusCodes.Status422UnprocessableEntity, "Invalid Operation", result.Message),
            _ => CreateProblemResult(StatusCodes.Status500InternalServerError, "Internal Server Error", result.Message),
        };
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
