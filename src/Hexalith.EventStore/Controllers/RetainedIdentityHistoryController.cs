using Hexalith.EventStore.Contracts.Streams;
using Hexalith.EventStore.Server.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.Controllers;

/// <summary>Explicitly composed internal attribution endpoint; ordinary stream privileges do not grant it.</summary>
[ApiController]
[Authorize]
[Route("api/v1/identity-history")]
public sealed class RetainedIdentityHistoryController(RetainedIdentityHistorySourceReader? reader = null) : ControllerBase
{
    /// <summary>Reads only the exact current owner-authorized purpose; missing composition fails closed.</summary>
    [HttpPost("read")]
    [RequestSizeLimit(16_384)]
    public async Task<ActionResult<RetainedIdentityHistoryReadResult>> ReadAsync(
        RetainedIdentityHistoryReadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RetainedIdentityHistoryReadResult result = reader is null
            ? new(null, "history-unavailable")
            : await reader.ReadAsync(User, request, cancellationToken).ConfigureAwait(false);
        return result.IsAuthoritative ? Ok(result) : StatusCode(StatusCodes.Status503ServiceUnavailable,
            new RetainedIdentityHistoryReadResult(null, "history-unavailable"));
    }
}
