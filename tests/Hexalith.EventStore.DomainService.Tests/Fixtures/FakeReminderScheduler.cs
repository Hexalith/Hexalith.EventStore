namespace Hexalith.EventStore.DomainService.Tests.Fixtures;

/// <summary>An in-memory scheduler for one reminder actor; it survives simulated host restarts.</summary>
internal sealed class FakeReminderScheduler : IReminderScheduler
{
    /// <summary>Gets the reminders the scheduler currently holds.</summary>
    public Dictionary<string, (TimeSpan DueTime, TimeSpan Period)> Armed { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets every cancelled reminder name, in order.</summary>
    public List<string> Cancelled { get; } = [];

    /// <summary>Gets or sets the failure every arm call throws, simulating an unavailable scheduler.</summary>
    public Exception? ArmFailure { get; set; }

    /// <summary>Gets or sets a probe invoked before each accepted arm call.</summary>
    public Action<string>? OnArm { get; set; }

    /// <summary>Gets the number of accepted arm calls.</summary>
    public int ArmCalls { get; private set; }

    /// <inheritdoc/>
    public Task ArmAsync(string reminderName, TimeSpan dueTime, TimeSpan period, CancellationToken cancellationToken)
    {
        if (ArmFailure is not null)
        {
            throw ArmFailure;
        }

        OnArm?.Invoke(reminderName);
        Armed[reminderName] = (dueTime, period);
        ArmCalls++;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> IsArmedAsync(string reminderName, CancellationToken cancellationToken)
        => Task.FromResult(Armed.ContainsKey(reminderName));

    /// <inheritdoc/>
    public Task CancelAsync(string reminderName, CancellationToken cancellationToken)
    {
        _ = Armed.Remove(reminderName);
        Cancelled.Add(reminderName);
        return Task.CompletedTask;
    }

    /// <summary>Drops a reminder without a callback, as a lost scheduler job would.</summary>
    /// <param name="reminderName">The reminder name.</param>
    public void Lose(string reminderName) => _ = Armed.Remove(reminderName);
}
