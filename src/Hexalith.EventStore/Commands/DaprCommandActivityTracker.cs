using System.Collections.Concurrent;

using Hexalith.EventStore.Admin.Abstractions.Models.Commands;
using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;

namespace Hexalith.EventStore.Commands;

/// <summary>
/// In-memory implementation of <see cref="ICommandActivityTracker"/>.
/// Maintains a bounded list of recent commands that is exposed via
/// <see cref="GetRecentCommands"/> for the admin UI Commands page.
/// </summary>
public sealed class InMemoryCommandActivityTracker : ICommandActivityTracker
{
    private const int MaxEntries = 1000;
    private readonly ConcurrentDictionary<string, CommandSummary> _commands = new();

    /// <inheritdoc/>
    public Task TrackAsync(
        string tenantId,
        string domain,
        string aggregateId,
        string correlationId,
        string commandType,
        CommandStatus status,
        DateTimeOffset timestamp,
        int? eventCount,
        string? failureReason,
        CancellationToken ct = default)
    {
        var summary = new CommandSummary(
            tenantId, domain, aggregateId, correlationId,
            commandType, status, timestamp, eventCount, failureReason);

        _ = _commands.AddOrUpdate(correlationId, summary, (_, _) => summary);

        // Evict oldest entries when over capacity
        if (_commands.Count > MaxEntries)
        {
            IEnumerable<string> keysToRemove = _commands
                .OrderBy(kvp => kvp.Value.Timestamp)
                .Take(_commands.Count - MaxEntries)
                .Select(kvp => kvp.Key);

            foreach (string key in keysToRemove)
            {
                _ = _commands.TryRemove(key, out _);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns recent commands, optionally filtered by tenant, status, and command type.
    /// Called by the admin commands endpoint.
    /// </summary>
    public PagedResult<CommandSummary> GetRecentCommands(
        string? tenantId,
        string? status,
        string? commandType,
        int count = 1000)
    {
        IEnumerable<CommandSummary> filtered = _commands.Values;

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            filtered = filtered.Where(c => c.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filtered = ApplyStatusFilter(filtered, status);
        }

        if (!string.IsNullOrWhiteSpace(commandType))
        {
            filtered = filtered.Where(c => c.CommandType.Contains(commandType, StringComparison.OrdinalIgnoreCase));
        }

        List<CommandSummary> filteredList = filtered.ToList();
        IReadOnlyList<CommandSummary> page = filteredList
            .OrderByDescending(c => c.Timestamp)
            .Take(count)
            .ToList();

        return new PagedResult<CommandSummary>(page, filteredList.Count, null);
    }

    private static IEnumerable<CommandSummary> ApplyStatusFilter(
        IEnumerable<CommandSummary> commands,
        string status)
    {
        string normalized = status.Trim().ToLowerInvariant();
        return normalized switch
        {
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
