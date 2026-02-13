namespace Hexalith.EventStore.CommandApi.Controllers;

using System.Text.Json;

using Hexalith.EventStore.CommandApi.Middleware;
using Hexalith.EventStore.CommandApi.Models;
using Hexalith.EventStore.Server.Pipeline.Commands;

using MediatR;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/v1/commands")]
[Consumes("application/json")]
public class CommandsController(IMediator mediator, ILogger<CommandsController> logger) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> Submit([FromBody] SubmitCommandRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string correlationId = HttpContext.Items[CorrelationIdMiddleware.HttpContextKey]?.ToString()
            ?? Guid.NewGuid().ToString();

        // Store tenant in HttpContext for error handlers to include in ProblemDetails extensions
        if (!string.IsNullOrEmpty(request.Tenant))
        {
            HttpContext.Items["RequestTenantId"] = request.Tenant;
        }

        // Layer 3: Pre-pipeline tenant authorization (before MediatR pipeline)
        if (HttpContext?.User is null)
        {
            return CreateForbiddenProblemDetails("Authentication context unavailable.", correlationId, request.Tenant);
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return CreateForbiddenProblemDetails("User is not authenticated.", correlationId, request.Tenant);
        }

        List<string> tenantClaims = User.FindAll("eventstore:tenant")
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        if (tenantClaims.Count == 0)
        {
            LogTenantAuthorizationFailure(correlationId, request.Tenant, request.CommandType, request.Domain, "No tenant claims");
            return CreateForbiddenProblemDetails("No tenant authorization claims found. Access denied.", correlationId, request.Tenant);
        }

        if (!tenantClaims.Any(t => string.Equals(t, request.Tenant, StringComparison.OrdinalIgnoreCase)))
        {
            LogTenantAuthorizationFailure(correlationId, request.Tenant, request.CommandType, request.Domain, "Tenant not authorized");
            return CreateForbiddenProblemDetails($"Not authorized to submit commands for tenant '{request.Tenant}'.", correlationId, request.Tenant);
        }

        // Store authorized tenant for downstream use
        HttpContext.Items["AuthorizedTenant"] = request.Tenant;

        var command = new SubmitCommand(
            Tenant: request.Tenant,
            Domain: request.Domain,
            AggregateId: request.AggregateId,
            CommandType: request.CommandType,
            Payload: JsonSerializer.SerializeToUtf8Bytes(request.Payload),
            CorrelationId: correlationId,
            Extensions: request.Extensions);

        SubmitCommandResult result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        // RFC 7231: Location header should be absolute URI
        string absoluteLocationUri = $"{Request.Scheme}://{Request.Host}/api/v1/commands/status/{result.CorrelationId}";
        Response.Headers["Location"] = absoluteLocationUri;
        Response.Headers["Retry-After"] = "1";

        return Accepted(new SubmitCommandResponse(result.CorrelationId));
    }

    private ObjectResult CreateForbiddenProblemDetails(string detail, string correlationId, string tenantId)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Type = "https://tools.ietf.org/html/rfc9457#section-3",
            Detail = detail,
            Instance = HttpContext?.Request.Path,
            Extensions =
            {
                ["correlationId"] = correlationId,
                ["tenantId"] = tenantId,
            },
        };

        Response.ContentType = "application/problem+json";
        return new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status403Forbidden };
    }

    private void LogTenantAuthorizationFailure(string correlationId, string tenantId, string commandType, string domain, string reason)
    {
        string? sourceIp = HttpContext?.Connection.RemoteIpAddress?.ToString();
        logger.LogWarning(
            "Tenant authorization failed: CorrelationId={CorrelationId}, TenantId={TenantId}, CommandType={CommandType}, Domain={Domain}, Reason={Reason}, SourceIP={SourceIP}",
            correlationId,
            tenantId,
            commandType,
            domain,
            reason,
            sourceIp);
    }
}
