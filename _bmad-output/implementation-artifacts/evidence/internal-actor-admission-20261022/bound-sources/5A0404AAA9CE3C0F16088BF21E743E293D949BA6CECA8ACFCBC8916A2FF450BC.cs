using Hexalith.EventStore.Admin.Abstractions.Models.Commands;
using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Commands;

/// <summary>
/// Single source of truth for command status filter logic used by the activity tracker
/// and its tests.
/// </summary>
public static class CommandStatusFilterHelper {
    /// <summary>
    /// Filters commands by a human-friendly status string.
    /// </summary>
    public static IEnumerable<CommandSummary> ApplyStatusFilter(
        IEnumerable<CommandSummary> commands,
        string status) {
        ArgumentNullException.ThrowIfNull(status);
        string normalized = status.Trim().ToLowerInvariant();
        return normalized switch {
            "completed" => commands.Where(c => c.Status == CommandStatus.Completed),
            "processing" => commands.Where(c => c.Status is CommandStatus.Received
                or CommandStatus.Processing
                or CommandStatus.EventsStored
                or CommandStatus.EventsPublished),
            "rejected" => commands.Where(c => c.Status == CommandStatus.Rejected),
            "failed" => commands.Where(c => c.Status is CommandStatus.PublishFailed
                or CommandStatus.TimedOut),
            _ when Enum.TryParse(status.Trim(), ignoreCase: true, out CommandStatus parsedStatus)
                => commands.Where(c => c.Status == parsedStatus),
            _ => commands,
        };
    }
}