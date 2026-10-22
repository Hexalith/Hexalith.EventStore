using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Server.Commands;

namespace Hexalith.EventStore.ProviderVerification;

internal sealed class NoOpCommandActivityTracker : ICommandActivityTracker
{
    public Task TrackAsync(
        string tenantId,
        string domain,
        string aggregateId,
        string correlationId,
        string commandType,
        CommandStatus status,
        DateTimeOffset timestamp,
        int? eventCount,
        string? failureReason,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
