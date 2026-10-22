using Hexalith.EventStore.Client.Reminders;
using Hexalith.EventStore.Contracts.Reminders;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Synthetic domain intent source for the live reminder proof; it translates to the counter domain.</summary>
internal sealed class LiveReminderIntentSource(LiveReminderIntents intents) : IReminderIntentSource
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<ReminderIntent>> GetCurrentIntentsAsync(ReminderTarget target, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(intents.Get(target));
    }

    /// <inheritdoc/>
    public ReminderCommand TranslateDueIntent(ReminderIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        return new ReminderCommand("IncrementCounter", [.. intent.Payload]);
    }
}
