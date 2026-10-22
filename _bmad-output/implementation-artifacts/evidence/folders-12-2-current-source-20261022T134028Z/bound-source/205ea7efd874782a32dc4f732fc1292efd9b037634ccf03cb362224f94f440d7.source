using Hexalith.EventStore.Contracts.Commands;

namespace Hexalith.EventStore.Server.Actors;

/// <summary>Immutable actor-owned evidence for retrying an exact committed terminal advisory.</summary>
/// <param name="TenantId">The owning actor tenant.</param>
/// <param name="Domain">The owning actor domain.</param>
/// <param name="AggregateId">The owning actor aggregate.</param>
/// <param name="MessageId">The original command message identity.</param>
/// <param name="CorrelationId">The original command correlation.</param>
/// <param name="CausationId">The normalized original command causation.</param>
/// <param name="CommandType">The original command type.</param>
/// <param name="EventCount">The original event count.</param>
/// <param name="StartSequence">The original committed range start, when present.</param>
/// <param name="EndSequence">The original committed range end, when present.</param>
/// <param name="Status">The exact terminal status to write on every retry.</param>
internal sealed record TerminalAdvisoryIntent(
    string TenantId,
    string Domain,
    string AggregateId,
    string MessageId,
    string CorrelationId,
    string CausationId,
    string CommandType,
    int EventCount,
    long? StartSequence,
    long? EndSequence,
    CommandStatusRecord Status)
{
    /// <summary>Returns the actor-state key for this command's pending advisory.</summary>
    internal static string GetStateKey(string messageId) => $"terminal-advisory:{messageId}";

    /// <summary>Returns a reminder name independent of event publication ownership.</summary>
    internal static string GetReminderName(string messageId) => $"repair-terminal-advisory-{messageId}";

    /// <summary>The prefix used to dispatch terminal advisory reminders.</summary>
    internal const string ReminderPrefix = "repair-terminal-advisory-";
}
