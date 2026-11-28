using System.Net;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.DeadLetters;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Authorization;
using Hexalith.EventStore.Admin.Server.Configuration;
using Hexalith.EventStore.Admin.Server.Models;
using Hexalith.EventStore.Contracts.Identity;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Admin.Server.Controllers;

/// <summary>
/// REST API controller for querying and managing dead-letter entries.
/// </summary>
[Hexalith.EventStore.Contracts.Routing.EventStoreTechnicalHttpController]
[ApiController]
[Authorize(Policy = "EventStoreHuman")]
[Route("api/v1/admin/dead-letters")]
[Tags("Admin - Dead Letters")]
public class AdminDeadLettersController(
    IDeadLetterQueryService deadLetterQueryService,
    IDeadLetterCommandService deadLetterCommandService,
    ILogger<AdminDeadLettersController> logger) : ControllerBase {
    /// <summary>
    /// Gets the authorized dead-letter count. Kept as the published 4.2.0 binary member.
    /// </summary>
    [NonAction]
    public Task<IActionResult> GetDeadLetterCount(CancellationToken ct) =>
        GetDeadLetterCount(tenantId: null, detailed: false, ct);

    /// <summary>
    /// Gets the authorized dead-letter count and optionally returns the oldest failure age.
    /// </summary>
    [HttpGet("count")]
    [Authorize(Policy = AdminAuthorizationPolicies.ReadOnly)]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DeadLetterSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetDeadLetterCount(
        [FromQuery] string? tenantId = null, [FromQuery] bool detailed = false, CancellationToken ct = default) {
        try {
            if (!TryResolveTenantScope(tenantId, out string? effectiveTenantId)) {
                return CreateProblemResult(StatusCodes.Status403Forbidden, "Forbidden", "Not authorized for the requested dead letters.");
            }

            if (detailed || effectiveTenantId is not null) {
                DeadLetterSummary summary = await deadLetterQueryService.GetDeadLetterSummaryAsync(effectiveTenantId, ct).ConfigureAwait(false);
                return detailed ? Ok(summary) : Ok(summary.Count);
            }

            int count = await deadLetterQueryService
                .GetDeadLetterCountAsync(ct)
                .ConfigureAwait(false);
            return Ok(count);
        }
        catch (UnauthorizedAccessException) {
            return CreateProblemResult(StatusCodes.Status403Forbidden, "Forbidden", "Not authorized for the requested dead letters.");
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
    [ProducesResponseType(typeof(PagedResult<DeadLetterEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ListDeadLetters(
        [FromQuery] string? tenantId,
        [FromQuery] int count = 100,
        [FromQuery] string? continuationToken = null,
        CancellationToken ct = default) {
        try {
            if (!TryResolveTenantScope(tenantId, out string? effectiveTenantId)) {
                return CreateProblemResult(
                    StatusCodes.Status403Forbidden,
                    "Tenant Access Denied",
                    "Not authorized for the requested tenant.");
            }

            if (count <= 0) {
                return InvalidListRequest();
            }

            PagedResult<DeadLetterEntry> result = await deadLetterQueryService
                .ListDeadLettersAsync(effectiveTenantId, count, continuationToken, ct)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (UnauthorizedAccessException) {
            return CreateProblemResult(StatusCodes.Status403Forbidden, "Forbidden", "Not authorized for the requested dead letters.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest) {
            return InvalidListRequest();
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
    [RequestSizeLimit(AdminRequestSizeLimits.OrdinaryJsonBody)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RetryDeadLetters(
        string tenantId,
        [FromBody] DeadLetterActionRequest request,
        CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(request);

        try {
            IReadOnlyList<string> messageIds = NormalizeMessageIds(request.MessageIds!);
            AdminOperationResult result = request.ActionId is { } actionId
                ? await deadLetterCommandService.RetryDeadLettersAsync(tenantId, messageIds, actionId, ct).ConfigureAwait(false)
                : await deadLetterCommandService.RetryDeadLettersAsync(tenantId, messageIds, ct).ConfigureAwait(false);
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
    [RequestSizeLimit(AdminRequestSizeLimits.OrdinaryJsonBody)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SkipDeadLetters(
        string tenantId,
        [FromBody] DeadLetterActionRequest request,
        CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(request);

        try {
            IReadOnlyList<string> messageIds = NormalizeMessageIds(request.MessageIds!);
            AdminOperationResult result = request.ActionId is { } actionId
                ? await deadLetterCommandService.SkipDeadLettersAsync(tenantId, messageIds, actionId, ct).ConfigureAwait(false)
                : await deadLetterCommandService.SkipDeadLettersAsync(tenantId, messageIds, ct).ConfigureAwait(false);
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
    [RequestSizeLimit(AdminRequestSizeLimits.OrdinaryJsonBody)]
    [ProducesResponseType(typeof(AdminOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ArchiveDeadLetters(
        string tenantId,
        [FromBody] DeadLetterActionRequest request,
        CancellationToken ct = default) {
        ArgumentNullException.ThrowIfNull(request);

        try {
            IReadOnlyList<string> messageIds = NormalizeMessageIds(request.MessageIds!);
            AdminOperationResult result = request.ActionId is { } actionId
                ? await deadLetterCommandService.ArchiveDeadLettersAsync(tenantId, messageIds, actionId, ct).ConfigureAwait(false)
                : await deadLetterCommandService.ArchiveDeadLettersAsync(tenantId, messageIds, ct).ConfigureAwait(false);
            return MapOperationResult(result);
        }
        catch (Exception ex) when (IsServiceUnavailable(ex)) {
            return ServiceUnavailable(nameof(ArchiveDeadLetters), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            return UnexpectedError(nameof(ArchiveDeadLetters), ex);
        }
    }

    private static IReadOnlyList<string> NormalizeMessageIds(IReadOnlyList<string> messageIds) {
        // De-duplicates with case-sensitive ordinal comparison to avoid replaying the same
        // dead-letter twice in one operator action while preserving the operator-provided order.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var deduped = new List<string>(messageIds.Count);
        foreach (string messageId in messageIds) {
            if (seen.Add(messageId)) {
                deduped.Add(messageId);
            }
        }

        return deduped;
    }

    private IActionResult InvalidListRequest()
        => CreateProblemResult(StatusCodes.Status400BadRequest, "Invalid Dead Letter List Request",
            "Supply a positive count and a continuation token for this view.");

    private bool TryResolveTenantScope(string? requestedTenantId, out string? effectiveTenantId)
    {
        effectiveTenantId = null;
        if (Request.Query["tenantId"].Count > 1 || Request.Query.ContainsKey("tenantId") && requestedTenantId is null
            || Request.Headers.ContainsKey("X-Hexalith-Scope") || Request.Headers.ContainsKey("X-Hexalith-Tenant")
            || !TenantCanonicalizer.TryCanonicalizeGrants(User.FindAll(AdminClaimTypes.Tenant).Select(claim => claim.Value), out IReadOnlySet<string> grants))
        {
            return false;
        }

        bool global = User.HasClaim(AdminClaimTypes.AdminRole, nameof(Abstractions.Models.Common.AdminRole.Admin));
        if (requestedTenantId is not null)
        {
            if (!TenantCanonicalizer.TryCanonicalize(requestedTenantId, out string tenantId) || !global && !grants.Contains(tenantId))
            {
                return false;
            }

            effectiveTenantId = tenantId;
            return true;
        }

        if (global)
        {
            return true;
        }

        if (grants.Count == 1)
        {
            effectiveTenantId = grants.Single();
            return true;
        }

        return false;
    }

    private IActionResult MapOperationResult(AdminOperationResult? result) {
        if (result is null) {
            return CreateProblemResult(StatusCodes.Status500InternalServerError, "Internal Server Error", "No result returned from the service.");
        }

        if (result.State == "pending") {
            return Accepted(result);
        }

        if (result.State == "failed") {
            return UnprocessableEntity(result);
        }

        if (result.Success) {
            return Ok(result);
        }

        return result.ErrorCode switch {
            "Unavailable" or "TIMEOUT" => CreateProblemResult(StatusCodes.Status503ServiceUnavailable, "Service Unavailable", result.Message),
            "NotFound" => CreateProblemResult(StatusCodes.Status404NotFound, "Not Found", result.Message),
            "Unauthorized" => CreateProblemResult(StatusCodes.Status403Forbidden, "Forbidden", result.Message),
            "InvalidOperation" => CreateProblemResult(StatusCodes.Status422UnprocessableEntity, "Invalid Operation", result.Message),
            _ => CreateProblemResult(StatusCodes.Status500InternalServerError, "Internal Server Error", result.Message),
        };
    }

    private static bool IsServiceUnavailable(Exception ex)
        => ex is HttpRequestException or TimeoutException or NotSupportedException
            || (ex is Grpc.Core.RpcException rpc && rpc.StatusCode is
                Grpc.Core.StatusCode.Unavailable or
                Grpc.Core.StatusCode.DeadlineExceeded or
                Grpc.Core.StatusCode.Aborted or
                Grpc.Core.StatusCode.ResourceExhausted);

    private ObjectResult ServiceUnavailable(string method, Exception ex) {
        logger.LogError("Admin service unavailable in {Method}. FailureType={FailureType}", method, ex.GetType().Name);
        return CreateProblemResult(
            StatusCodes.Status503ServiceUnavailable,
            "Service Unavailable",
            "The admin backend service is temporarily unavailable. Retry shortly.");
    }

    private ObjectResult UnexpectedError(string method, Exception ex) {
        logger.LogError("Unexpected error in {Method}. FailureType={FailureType}", method, ex.GetType().Name);
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
