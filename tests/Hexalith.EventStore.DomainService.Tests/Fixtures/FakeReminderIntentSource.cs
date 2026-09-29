using Hexalith.EventStore.Client.Reminders;
using Hexalith.EventStore.Contracts.Reminders;

namespace Hexalith.EventStore.DomainService.Tests.Fixtures;

/// <summary>A synthetic domain intent source whose "stream fold" is set directly by the test.</summary>
internal sealed class FakeReminderIntentSource : IReminderIntentSource
{
    private readonly Dictionary<ReminderTarget, List<ReminderIntent>> _intents = [];

    /// <summary>Gets the targets whose stream reads fail.</summary>
    public HashSet<ReminderTarget> Failing { get; } = [];

    /// <summary>Gets or sets an optional translator override.</summary>
    public Func<ReminderIntent, ReminderCommand>? Translator { get; set; }

    /// <summary>Gets the number of stream re-folds.</summary>
    public int Reads { get; private set; }

    /// <summary>Replaces the target's current intents.</summary>
    /// <param name="target">The target.</param>
    /// <param name="intents">The intents the stream now holds.</param>
    public void Set(ReminderTarget target, params ReminderIntent[] intents) => _intents[target] = [.. intents];

    /// <inheritdoc/>
    public Task<IReadOnlyList<ReminderIntent>> GetCurrentIntentsAsync(ReminderTarget target, CancellationToken cancellationToken = default)
    {
        Reads++;
        if (Failing.Contains(target))
        {
            throw new InvalidOperationException("Synthetic stream read failure.");
        }

        return Task.FromResult<IReadOnlyList<ReminderIntent>>(
            _intents.TryGetValue(target, out List<ReminderIntent>? intents) ? [.. intents] : []);
    }

    /// <inheritdoc/>
    public ReminderCommand TranslateDueIntent(ReminderIntent intent)
        => Translator?.Invoke(intent) ?? new ReminderCommand("ResumeWidget", [.. intent.Payload]);
}
