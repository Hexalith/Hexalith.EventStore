using Hexalith.EventStore.ServiceDefaults.Routing;
using System.Security.Claims;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Controllers;

public sealed partial class AdminProjectionRebuildController
{
    /// <summary>Bootstraps an inventoried source chain while the complete writer fleet is quiesced.</summary>
    [HttpPost("{tenantId}/source-proof/{domain}/{aggregateId}/bootstrap")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> BootstrapSourceProofAsync(string tenantId, string domain, string aggregateId,
        [FromBody] ProjectionSourceBootstrapRequestBody? body, CancellationToken cancellationToken)
    {
        IActionResult? denied = EnsureGlobalAdministrator();
        if (denied is not null) return denied;
        string? operatorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(operatorId))
            return ProblemWithReason(403, "Forbidden", "An attributable operator identity is required.", StreamReplayReasonCodes.ForbiddenRole);
        AggregateIdentity identity;
        try
        {
            identity = new(tenantId, domain, aggregateId);
            _ = CreateScope(tenantId, domain);
            if (body is null) throw new ArgumentException("Missing maintenance attestations.");
        }
        catch (ArgumentException)
        {
            return ProblemWithReason(400, "Bad Request", "Source scope and maintenance attestations are required.", StreamReplayReasonCodes.MissingRequiredField);
        }

        IServiceProvider services = serviceProvider ?? HttpContext.RequestServices;
        SourceProofBootstrapAuthority? authority = services.GetService<SourceProofBootstrapAuthority>();
        IActorProxyFactory? factory = services.GetService<IActorProxyFactory>();
        IOptions<EventStoreActorOptions>? actorOptions = services.GetService<IOptions<EventStoreActorOptions>>();
        if (authority is null || factory is null || actorOptions is null)
            return ProblemWithReason(503, "Service Unavailable", "Source bootstrap is unavailable.", StreamReplayReasonCodes.ServiceUnavailable);
        try
        {
            SourceProofBootstrapRequest request = await authority.AuthorizeAsync(new(identity.TenantId, identity.Domain,
                identity.AggregateId, body.ExpectedHead, operatorId, body.CutoverCommit, body.BackupReference,
                body.SpecSha256, body.WritersQuiesced, body.BackupRestoreVerified, default, string.Empty, string.Empty),
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            IAggregateActor actor = factory.CreateActorProxy<IAggregateActor>(new ActorId(identity.ActorId),
                actorOptions.Value.AggregateActorTypeName);
            SourceProofInspection result = await RouteCatalogRemoteCall.InvokeAsync(() => actor.BootstrapSourceProofAsync(request), cancellationToken).ConfigureAwait(false);
            return Ok(new { result.Status, result.CurrentSequence, result.Erased, SourceIncarnation = result.Proof?.SourceIncarnation });
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException)
        {
            return ProblemWithReason(409, "Conflict", "Source bootstrap could not prove the inventoried source under maintenance authority.", StreamReplayReasonCodes.CheckpointConflict);
        }
        catch
        {
            return ProblemWithReason(503, "Service Unavailable", "Source bootstrap is unavailable.", StreamReplayReasonCodes.ServiceUnavailable);
        }
    }
}
