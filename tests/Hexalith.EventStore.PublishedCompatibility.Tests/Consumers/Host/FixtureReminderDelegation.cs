#if SELECTED
using Hexalith.EventStore.Client.Reminders;

namespace PublishedConsumer;

/// <summary>Supplies synthetic fixture delegation to the independently checked fixture policy.</summary>
public sealed class FixtureReminderDelegation : IReminderDelegationTokenProvider
{
    /// <inheritdoc/>
    public Task<string?> GetDelegationTokenAsync(ReminderDelegationRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>("fixture-only");
}
#endif
