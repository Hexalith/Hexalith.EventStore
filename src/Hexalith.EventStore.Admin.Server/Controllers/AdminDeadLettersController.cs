using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.Server.Controllers;

/// <summary>
/// REST API controller for querying and managing dead-letter entries.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/admin/dead-letters")]
[Tags("Admin - Dead Letters")]
public class AdminDeadLettersController(
    IDeadLetterQueryService deadLetterQueryService,
    IDeadLetterCommandService deadLetterCommandService,
    ILogger<AdminDeadLettersController> logger) : ControllerBase {
    /// <summary>
    /// Gets the total count of dead-letter entries across all tenants.
    /// </summary>
    [HttpGet("count")]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetDeadLetterCount(CancellationToken ct = default) {
        try {
            int count = await deadLetterQueryService
                .GetDeadLetterCountAsync(ct)
                .ConfigureAwait(false);
            return Ok(count);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(GetDeadLetterCount), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(GetDeadLetterCount), ex);
        }
    }

    /// <summary>
    /// Lists dead-letter entries, optionally filtered by tenant.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(PagedResult<DeadLetterEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListDeadLetters(
        [FromQuery] string? tenantId,
        [FromQuery] int count = 100,
        [FromQuery] string? continuationToken = null,
        CancellationToken ct = default) {
        try {
            string? effectiveTenantId = ResolveTenantScope(tenantId);
            PagedResult<DeadLetterEntry> result = await deadLetterQueryService
                .ListDeadLettersAsync(effectiveTenantId, count, continuationToken, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(ListDeadLetters), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(ListDeadLetters), ex);
        }
    }

    /// <summary>
    /// Retries the specified dead-letter messages.
    /// </summary>
    [HttpPost("{tenantId}/retry")]
    [Authorize(Policy = AdminAuthorizationPolicies.Operator)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RetryDeadLetters(
        string tenantId,
        [FromBody] DeadLetterActionRequest request,
        CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(request);

        try {
            AdminOperationResult result = await deadLetterCommandService
                .RetryDeadLettersAsync(tenantId, request.MessageIds, ct)
                .ConfigureAwait(false);
            return MapOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(RetryDeadLetters), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(RetryDeadLetters), ex);
        }
    }

    /// <summary>
    /// Skips the specified dead-letter messages, removing them from the queue.
    /// </summary>
    [HttpPost("{tenantId}/skip")]
    [Authorize(Policy = AdminAuthorizationPolicies.Operator)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SkipDeadLetters(
        string tenantId,
        [FromBody] DeadLetterActionRequest request,
        CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(request);

        try {
            AdminOperationResult result = await deadLetterCommandService
                .SkipDeadLettersAsync(tenantId, request.MessageIds, ct)
                .ConfigureAwait(false);
            return MapOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(SkipDeadLetters), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(SkipDeadLetters), ex);
        }
    }

    /// <summary>
    /// Archives the specified dead-letter messages.
    /// </summary>
    [HttpPost("{tenantId}/archive")]
    [Authorize(Policy = AdminAuthorizationPolicies.Operator)]
    [ServiceFilter(typeof(AdminTenantAuthorizationFilter))]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ArchiveDeadLetters(
        string tenantId,
        [FromBody] DeadLetterActionRequest request,
        CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(request);

        try {
            AdminOperationResult result = await deadLetterCommandService
                .ArchiveDeadLettersAsync(tenantId, request.MessageIds, ct)
                .ConfigureAwait(false);
            return MapOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(ArchiveDeadLetters), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(ArchiveDeadLetters), ex);
        }
    }

    private string? ResolveTenantScope(string? requestedTenantId) {
        if (requestedTenantId is not null) {
            return requestedTenantId;
        }

        if (User.HasClaim(AdminClaimTypes.AdminRole, nameof(Abstractions.Models.Common.AdminRole.Admin))) {
            return null;
        }

        return User.FindFirst(AdminClaimTypes.Tenant)?.Value;
    }

    private IActionResult MapOperationResult(AdminOperationResult? result) {
        if (result is null) {
            return CreateProblemResult(StatusCodes.Status500InternalServerError, "Internal Server Error", "No result returned from the service.");
        }

        if (result.Success) {
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
