using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Actors;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.TestUtilities;

/// <summary>Delivers only reminders whose timer-manager registration succeeded and whose due time elapsed.</summary>
internal sealed class RegisteredReminderHarness
{
    private readonly Dictionary<string, (ActorReminder Reminder, TimeSpan NextDue)> _registered = new(StringComparer.Ordinal);
    private TimeSpan _elapsed;

    /// <summary>Creates a timer manager that records successful scheduler operations.</summary>
    internal RegisteredReminderHarness()
    {
        Manager = Substitute.For<ActorTimerManager>();
        Manager.RegisterReminderAsync(Arg.Any<ActorReminder>()).Returns(call =>
        {
            if (FailRegistration) { return Task.FromException(new IOException("scheduler unavailable")); }
            ActorReminder reminder = call.Arg<ActorReminder>();
            _registered[reminder.Name] = (reminder, _elapsed + reminder.DueTime);
            return Task.CompletedTask;
        });
        Manager.UnregisterReminderAsync(Arg.Any<ActorReminderToken>()).Returns(call =>
        {
            _registered.Remove(call.Arg<ActorReminderToken>().Name);
            return Task.CompletedTask;
        });
    }

    /// <summary>Gets the actor's timer manager.</summary>
    internal ActorTimerManager Manager { get; }

    /// <summary>Gets or sets whether scheduler registration fails before retaining a callback.</summary>
    internal bool FailRegistration { get; set; }

    /// <summary>Gets the number of callbacks actually delivered.</summary>
    internal int DeliveryCount { get; private set; }

    /// <summary>Gets successfully registered names.</summary>
    internal IReadOnlyCollection<string> RegisteredNames => _registered.Keys;

    /// <summary>Advances scheduler time and delivers each due registered callback at most once.</summary>
    internal async Task AdvanceAsync(AggregateActor actor, TimeSpan elapsed)
    {
        _elapsed += elapsed;
        foreach (string name in _registered.Keys.ToArray())
        {
            if (!_registered.TryGetValue(name, out var scheduled) || scheduled.NextDue > _elapsed) { continue; }
            _registered[name] = (scheduled.Reminder, _elapsed + scheduled.Reminder.Period);
            DeliveryCount++;
            await actor.ReceiveReminderAsync(name, scheduled.Reminder.State ?? [],
                scheduled.Reminder.DueTime, scheduled.Reminder.Period).ConfigureAwait(false);
        }
    }
}
