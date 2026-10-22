namespace Hexalith.EventStore.Server.Actors;

/// <summary>Discoverability and reminder registration for one terminal advisory intent.</summary>
/// <param name="MessageId">The original command message identity.</param>
/// <param name="ReminderArmedAt">When reminder registration was confirmed, or null before registration.</param>
internal sealed record TerminalAdvisoryRepairEntry(string MessageId, DateTimeOffset? ReminderArmedAt = null);
