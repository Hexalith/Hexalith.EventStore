namespace Hexalith.EventStore.Client.Reminders;

/// <summary>
/// Obtains the short-lived workload delegation a reminder needs for trusted-effect submission. No
/// production implementation ships with EventStore; without one, reminder submission fails closed and the
/// pending work is retained.
/// </summary>
public interface IReminderDelegationTokenProvider
{
    /// <summary>Returns a delegation bound to the request, or <see langword="null"/> when none can be issued.</summary>
    /// <param name="request">The exact submission, workload, purpose, and causation the delegation must bind.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The signed delegation token, or <see langword="null"/>.</returns>
    Task<string?> GetDelegationTokenAsync(
        ReminderDelegationRequest request,
        CancellationToken cancellationToken = default);
}
