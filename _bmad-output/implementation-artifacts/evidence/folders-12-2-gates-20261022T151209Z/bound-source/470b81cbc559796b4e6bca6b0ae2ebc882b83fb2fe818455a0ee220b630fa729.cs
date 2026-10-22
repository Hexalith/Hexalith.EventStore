using Hexalith.EventStore.Client.Reminders;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Fixture-only delegation accepted by <see cref="SyntheticTrustedEffectAdmissionPolicy"/>.</summary>
internal sealed class SyntheticReminderDelegationTokenProvider : IReminderDelegationTokenProvider
{
    /// <inheritdoc/>
    public Task<string?> GetDelegationTokenAsync(ReminderDelegationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<string?>("fixture-only");
    }
}
