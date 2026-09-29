namespace Hexalith.EventStore.DomainService;

/// <summary>The scheduler operations available inside one reminder actor turn.</summary>
internal interface IReminderScheduler
{
    /// <summary>Registers or replaces a periodic reminder.</summary>
    /// <param name="reminderName">The reminder name.</param>
    /// <param name="dueTime">The delay before the first firing.</param>
    /// <param name="period">The period of later firings until the reminder is cancelled.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the scheduler accepted the reminder.</returns>
    Task ArmAsync(string reminderName, TimeSpan dueTime, TimeSpan period, CancellationToken cancellationToken);

    /// <summary>Returns whether the scheduler still holds a reminder.</summary>
    /// <param name="reminderName">The reminder name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the scheduler holds the reminder.</returns>
    Task<bool> IsArmedAsync(string reminderName, CancellationToken cancellationToken);

    /// <summary>Cancels a reminder; cancelling an absent reminder is not an error.</summary>
    /// <param name="reminderName">The reminder name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the scheduler released the reminder.</returns>
    Task CancelAsync(string reminderName, CancellationToken cancellationToken);
}
