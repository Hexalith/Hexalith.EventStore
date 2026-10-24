using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests;

/// <summary>Explicit test domain authority for the PF-02 state lane.</summary>
internal sealed class LifecycleQualificationErasure : IErasureStateProvider
{
    /// <inheritdoc/>
    public ValueTask<PayloadErasureStateResult> GetStateAsync(PayloadErasureStateRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new PayloadErasureStateResult(PayloadErasureState.Active, 1, PayloadErasureReasonCode.None, DateTimeOffset.UtcNow));
    }
}
