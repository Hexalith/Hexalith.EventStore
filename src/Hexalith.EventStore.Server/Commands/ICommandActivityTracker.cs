using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Tracks command activity for admin index population.
/// Implementations should be advisory — failures are logged but do not block command processing.
/// </summary>
public interface ICommandActivityTracker {
    /// <summary>
    /// Records a command in the admin activity index so it appears in the admin UI Commands page.
    /// </summary>
    Task TrackAsync(
        string tenantId,
        string domain,
        string aggregateId,
        string correlationId,
        string commandType,
        CommandStatus status,
        DateTimeOffset timestamp,
        int? eventCount,
        string? failureReason,
        CancellationToken ct = default);
}
