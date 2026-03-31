using Hexalith.EventStore.Admin.Abstractions.Models.Commands;
using Hexalith.EventStore.Admin.Abstractions.Models.Common;

namespace Hexalith.EventStore.Commands;

/// <summary>
/// Reads recent command activity for admin query endpoints.
/// Kept separate from <see cref="Server.Commands.ICommandActivityTracker"/> so the
/// server-side tracking contract remains backward compatible for external implementers.
/// </summary>
public interface ICommandActivityReader {
    /// <summary>
    /// Returns recent commands, optionally filtered by tenant, status, and command type.
    /// </summary>
    Task<PagedResult<CommandSummary>> GetRecentCommandsAsync(
        string? tenantId,
        string? status,
        string? commandType,
        int count = 1000,
        CancellationToken ct = default);
}
