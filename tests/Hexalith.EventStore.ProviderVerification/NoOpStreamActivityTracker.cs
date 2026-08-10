using Hexalith.EventStore.Server.Commands;

namespace Hexalith.EventStore.ProviderVerification;

internal sealed class NoOpStreamActivityTracker : IStreamActivityTracker
{
    public Task TrackAsync(
        string tenantId,
        string domain,
        string aggregateId,
        long newEventsAppended,
        DateTimeOffset timestamp,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
