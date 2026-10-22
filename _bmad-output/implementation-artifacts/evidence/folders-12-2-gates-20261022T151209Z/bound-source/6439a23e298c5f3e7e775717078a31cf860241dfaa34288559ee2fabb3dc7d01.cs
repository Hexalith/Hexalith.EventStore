namespace Hexalith.EventStore.Contracts.Commands;

/// <summary>
/// Extension methods for <see cref="CommandStatus"/>.
/// </summary>
public static class CommandStatusExtensions {
    /// <summary>
    /// Determines whether the command has reached a terminal state — i.e., a state from which
    /// no further status transitions occur.
    /// </summary>
    /// <param name="status">The command lifecycle status to inspect.</param>
    /// <returns>
    /// <c>true</c> if the status is one of <see cref="CommandStatus.Completed"/>,
    /// <see cref="CommandStatus.Rejected"/>, <see cref="CommandStatus.PublishFailed"/>, or
    /// <see cref="CommandStatus.TimedOut"/>; <c>false</c> for the in-flight states
    /// <see cref="CommandStatus.Received"/>, <see cref="CommandStatus.Processing"/>,
    /// <see cref="CommandStatus.EventsStored"/>, and <see cref="CommandStatus.EventsPublished"/>.
    /// </returns>
    /// <remarks>
    /// The terminal/in-flight split is determined by the numeric convention
    /// <c>status &gt;= CommandStatus.Completed</c>, which is the single source of truth
    /// for this contract. Callers MUST consume this extension instead of re-implementing
    /// the convention to keep the rule centralized and testable.
    /// </remarks>
    public static bool IsTerminal(this CommandStatus status)
        => status >= CommandStatus.Completed;
}
