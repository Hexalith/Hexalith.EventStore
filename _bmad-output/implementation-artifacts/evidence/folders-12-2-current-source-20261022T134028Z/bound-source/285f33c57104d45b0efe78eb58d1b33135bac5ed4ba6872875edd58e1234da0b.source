using System.Collections.Concurrent;

using Hexalith.EventStore.Contracts.Reminders;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>
/// Synthetic stand-in for a domain stream's re-folded reminder intents. The fixture owns it, so the intents
/// survive a host restart exactly as a committed stream would.
/// </summary>
public sealed class LiveReminderIntents
{
    private readonly ConcurrentDictionary<ReminderTarget, ReminderIntent[]> _intents = new();

    /// <summary>Replaces the intents the target's stream currently holds.</summary>
    /// <param name="target">The target stream.</param>
    /// <param name="intents">The current intents.</param>
    public void Set(ReminderTarget target, params ReminderIntent[] intents)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(intents);
        _intents[target] = [.. intents];
    }

    /// <summary>Gets the intents the target's stream currently holds.</summary>
    /// <param name="target">The target stream.</param>
    /// <returns>The current intents.</returns>
    public IReadOnlyList<ReminderIntent> Get(ReminderTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return _intents.TryGetValue(target, out ReminderIntent[]? intents) ? [.. intents] : [];
    }
}
