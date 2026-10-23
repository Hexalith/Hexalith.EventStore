using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.Tests;

/// <summary>Explicitly owned erasure-state fixture with no provider exception text.</summary>
internal sealed class LifecycleTestErasureProvider : IErasureStateProvider
{
    /// <summary>Gets or sets the current strongly supplied fixture state.</summary>
    internal PayloadErasureStateResult State { get; set; } = new(PayloadErasureState.Active, 1, PayloadErasureReasonCode.None, DateTimeOffset.UtcNow);
    /// <summary>Returns only the configured typed erasure state.</summary>
    public ValueTask<PayloadErasureStateResult> GetStateAsync(PayloadErasureStateRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(State);
    }
}
