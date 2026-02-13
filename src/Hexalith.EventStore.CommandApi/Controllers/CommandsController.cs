namespace Hexalith.EventStore.CommandApi.Controllers;

using System.Text.Json;

using Hexalith.EventStore.CommandApi.Models;
using Hexalith.EventStore.Server.Pipeline.Commands;

using MediatR;

using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/commands")]
[Consumes("application/json")]
public class CommandsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(1_048_576)]
    public async Task<IActionResult> Submit([FromBody] SubmitCommandRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string correlationId = HttpContext.Items["CorrelationId"]?.ToString()
            ?? Guid.NewGuid().ToString();

        // Store tenant in HttpContext for error handlers to include in ProblemDetails extensions
        if (!string.IsNullOrEmpty(request.Tenant))
        {
            HttpContext.Items["RequestTenantId"] = request.Tenant;
        }

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
}
