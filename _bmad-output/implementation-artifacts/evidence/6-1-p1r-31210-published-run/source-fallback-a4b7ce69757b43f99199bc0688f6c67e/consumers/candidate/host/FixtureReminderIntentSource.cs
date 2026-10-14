#if P1R_CANDIDATE
using Hexalith.EventStore.Client.Reminders;
using Hexalith.EventStore.Contracts.Effects;
using Hexalith.EventStore.Contracts.Reminders;

/// <summary>Explicit fixture inputs; persisted reminder witnesses and target effects remain Dapr-owned.</summary>
internal sealed class FixtureReminderIntentSource : IReminderIntentSource
{
    /// <summary>Gets or sets the current bounded input selected by the invocation.</summary>
    internal ReminderIntent? Current { get; set; }

    /// <summary>Creates the same immutable witness before and after an application restart.</summary>
    internal static ReminderIntent Intent(int revision) => new("tenant-a", "counter", "fixture",
        DateTimeOffset.Parse(Environment.GetEnvironmentVariable("P1R_REMINDER_DUE")
            ?? throw new InvalidOperationException("Owned reminder due time unavailable."), System.Globalization.CultureInfo.InvariantCulture), EffectKindCatalog.DateResume,
        "p1r-counter-resume-v1", "{}"u8.ToArray(), "counter", "fixture", revision, revision);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ReminderIntent>> GetCurrentIntentsAsync(ReminderTarget target, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ReminderIntent>>(Current is null ? [] : [Current]);
    }

    /// <inheritdoc/>
    public ReminderCommand TranslateDueIntent(ReminderIntent intent) => new("P1R.Counter.IncrementCounter", intent.Payload);
}
#endif
