using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Server.Commands;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.Controllers;

/// <summary>Authenticated ingress for target-receipted effects.</summary>
[ApiController]
[Authorize]
[Route("api/v1/trusted-effects")]
public sealed class TrustedEffectsController(
    ITrustedEffectAdmissionPolicy admissionPolicy,
    ITrustedEffectGatewayProof gatewayProof,
    ITrustedEffectRouter router) : ControllerBase
{
    /// <summary>Submits or replays a delegated effect without using gateway status authority.</summary>
    [HttpPost]
    [RequestSizeLimit(1_048_576)]
    [ProducesResponseType(typeof(TrustedEffectResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitAsync(
        [FromBody] TrustedEffectSubmitRequest request,
        CancellationToken cancellationToken)
    {
        if (request?.Submission is null)
        {
            return BadRequest();
        }

        string? workload = User.FindFirst("dapr_caller_app_id")?.Value;
        if (string.IsNullOrWhiteSpace(workload))
        {
            return Forbid();
        }

        var context = new TrustedEffectContext(
            workload,
            request.Purpose,
            request.CausationId,
            request.DelegationToken);
        TrustedEffectAdmission admission;
        try
        {
            admission = await admissionPolicy.AdmitAsync(request.Submission, context, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            return Forbid();
        }

        string proof = await gatewayProof.SignAsync(admission, cancellationToken).ConfigureAwait(false);
        TrustedEffectResult result = await router
            .RouteAsync(request.Submission, context, proof, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}
