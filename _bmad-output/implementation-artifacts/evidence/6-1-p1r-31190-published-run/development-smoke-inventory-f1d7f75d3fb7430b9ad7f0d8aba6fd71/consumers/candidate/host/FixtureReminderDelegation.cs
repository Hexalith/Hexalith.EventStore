#if P1R_CANDIDATE
using Hexalith.EventStore.Client.Reminders;

/// <summary>Supplies the invocation's private bounded fixture credential; production identity remains a separate gate.</summary>
internal sealed class FixtureReminderDelegation(IConfiguration configuration) : IReminderDelegationTokenProvider
{
    /// <inheritdoc/>
    public Task<string?> GetDelegationTokenAsync(ReminderDelegationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(configuration["P1R_DELEGATION"]);
    }
}
#endif
