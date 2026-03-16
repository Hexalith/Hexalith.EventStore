using System.Security.Claims;
using System.Text.Json;

using Hexalith.EventStore.CommandApi.ErrorHandling;
using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.CommandApi.Models;
using Hexalith.EventStore.CommandApi.Validation;
using Hexalith.EventStore.Server.Pipeline.Commands;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.CommandApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/commands")]
[Consumes("application/json")]
public class CommandsController(IMediator mediator, ExtensionMetadataSanitizer extensionSanitizer, ILogger<CommandsController> logger) : ControllerBase
{
    private const string GlobalAdminExtensionKey = "actor:globalAdmin";

    [HttpPost]
    [RequestSizeLimit(1_048_576)]
    [ProducesResponseType(typeof(SubmitCommandResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> Submit([FromBody] SubmitCommandRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();

        // Store tenant in HttpContext for error handlers and rate limiter OnRejected callback
        if (!string.IsNullOrEmpty(request.Tenant))
        {
            HttpContext.Items["RequestTenantId"] = request.Tenant;
        }

        // Extract UserId from JWT -- use 'sub' claim ONLY (F-RT2: 'name' may be user-controllable)
        string? userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            logger.LogWarning(
                "JWT 'sub' claim missing for command submission. Rejecting request as unauthorized. CorrelationId={CorrelationId}.",
                correlationId);

            return Unauthorized();
        }

        // SEC-4: Extension metadata sanitization at API gateway
        SanitizeResult sanitizeResult = extensionSanitizer.Sanitize(request.Extensions);
        if (!sanitizeResult.IsSuccess)
        {
            logger.LogWarning(
                "Security event: SecurityEvent={SecurityEvent}, CorrelationId={CorrelationId}, Tenant={TenantId}, Domain={Domain}, Reason={Reason}",
                "ExtensionMetadataRejected",
                correlationId,
                request.Tenant,
                request.Domain,
                sanitizeResult.RejectionReason);

            ProblemDetails problemDetails = ValidationProblemDetailsFactory.Create(
                sanitizeResult.RejectionReason!,
                new Dictionary<string, string> { ["extensions"] = sanitizeResult.RejectionReason! },
                correlationId,
                request.Tenant);
            problemDetails.Instance = HttpContext?.Request.Path;

            var sanitizationResponse = new ObjectResult(problemDetails)
            { StatusCode = StatusCodes.Status400BadRequest };
            sanitizationResponse.ContentTypes.Add("application/problem+json");
            return sanitizationResponse;
        }

        Dictionary<string, string>? extensions = BuildTrustedExtensions(request.Extensions);

        var command = new SubmitCommand(
            MessageId: request.MessageId,
            Tenant: request.Tenant,
            Domain: request.Domain,
            AggregateId: request.AggregateId,
            CommandType: request.CommandType,
            Payload: JsonSerializer.SerializeToUtf8Bytes(request.Payload),
            CorrelationId: string.IsNullOrWhiteSpace(request.CorrelationId) ? request.MessageId : request.CorrelationId,
            UserId: userId,
            Extensions: extensions,
            IsGlobalAdmin: IsGlobalAdministrator(User));

        SubmitCommandResult result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        // RFC 7231: Location header should be absolute URI
        string absoluteLocationUri = $"{Request.Scheme}://{Request.Host}/api/v1/commands/status/{result.CorrelationId}";
        Response.Headers["Location"] = absoluteLocationUri;
        Response.Headers["Retry-After"] = "1";

        return Accepted(new SubmitCommandResponse(result.CorrelationId));
    }

    private Dictionary<string, string>? BuildTrustedExtensions(IDictionary<string, string>? requestExtensions)
    {
        if (requestExtensions is null || requestExtensions.Count == 0)
        {
            return null;
        }

        var trustedExtensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> extension in requestExtensions)
        {
            if (string.Equals(extension.Key, GlobalAdminExtensionKey, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "Reserved extension metadata key '{ExtensionKey}' was ignored for command submission.",
                    extension.Key);
                continue;
            }

            trustedExtensions[extension.Key] = extension.Value;
        }

        return trustedExtensions.Count > 0 ? trustedExtensions : null;
    }

    private static bool IsGlobalAdministrator(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        foreach (Claim claim in principal.Claims)
        {
            if (IsGlobalAdministratorClaim(claim))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGlobalAdministratorClaim(Claim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);

        if (claim.Type is "global_admin" or "is_global_admin")
        {
            return bool.TryParse(claim.Value, out bool isGlobalAdmin) && isGlobalAdmin;
        }

        if (claim.Type is ClaimTypes.Role or "role")
        {
            return IsGlobalAdministratorValue(claim.Value);
        }

        if (claim.Type == "roles")
        {
            return ClaimValueContainsGlobalAdministrator(claim.Value);
        }

        return false;
    }

    private static bool ClaimValueContainsGlobalAdministrator(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.StartsWith('['))
        {
            try
            {
                string[]? roles = JsonSerializer.Deserialize<string[]>(value);
                if (roles is not null)
                {
                    return roles.Any(IsGlobalAdministratorValue);
                }
            }
            catch (JsonException)
            {
                // Fall through to delimiter-based parsing below.
            }
        }

        return value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
            .Any(IsGlobalAdministratorValue);
    }

    private static bool IsGlobalAdministratorValue(string value)
        => value is not null
            && (string.Equals(value, "GlobalAdministrator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "global-administrator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "global-admin", StringComparison.OrdinalIgnoreCase));

}
