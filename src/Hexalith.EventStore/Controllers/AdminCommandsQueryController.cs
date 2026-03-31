using Hexalith.EventStore.Admin.Abstractions.Models.Commands;
using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Commands;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hexalith.EventStore.Controllers;

/// <summary>
/// Serves recent command activity for the admin UI Commands page.
/// The admin server queries this endpoint via DAPR service invocation.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/admin/streams")]
[Tags("Admin - Command Queries")]
public class AdminCommandsQueryController(
    ICommandActivityReader activityReader) : ControllerBase {
    /// <summary>
    /// Returns recent commands tracked by this EventStore instance.
    /// </summary>
    [HttpGet("commands")]
    [ProducesResponseType(typeof(PagedResult<CommandSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentCommands(
        [FromQuery] string? tenantId,
        [FromQuery] string? status,
        [FromQuery] string? commandType,
        [FromQuery] int count = 1000,
        CancellationToken ct = default) {
        PagedResult<CommandSummary> result = await activityReader
            .GetRecentCommandsAsync(tenantId, status, commandType, count, ct)
            .ConfigureAwait(false);
        return Ok(result);
    }
}
