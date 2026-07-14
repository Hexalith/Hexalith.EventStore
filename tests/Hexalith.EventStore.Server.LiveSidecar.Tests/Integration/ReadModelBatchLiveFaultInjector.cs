using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

/// <summary>Injects one deterministic failure into the live DAPR read-model batch protocol.</summary>
/// <param name="targetPhase">The protocol phase that triggers the failure.</param>
/// <param name="targetOrdinal">The operation ordinal that triggers the failure.</param>
/// <param name="beforeThrow">An optional action invoked immediately before the failure.</param>
internal sealed class ReadModelBatchLiveFaultInjector(
    ReadModelBatchPhase targetPhase,
    int targetOrdinal,
    Action<CancellationToken>? beforeThrow = null) : IReadModelBatchFaultInjector {
    private int _triggered;

    /// <inheritdoc/>
    public Task InjectAsync(ReadModelBatchPhase phase, int ordinal, CancellationToken cancellationToken) {
        if (phase == targetPhase
            && ordinal == targetOrdinal
            && Interlocked.Exchange(ref _triggered, 1) == 0) {
            beforeThrow?.Invoke(cancellationToken);
            throw new OperationCanceledException(cancellationToken);
        }

        return Task.CompletedTask;
    }
}
