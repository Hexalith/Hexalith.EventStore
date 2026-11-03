// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 5-7; separately authorized isolated environment required.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests;
internal sealed class AzureLiveErasure : IErasureStateProvider
{
    public ValueTask<PayloadErasureStateResult> GetStateAsync(PayloadErasureStateRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new PayloadErasureStateResult(PayloadErasureState.Active, 1, PayloadErasureReasonCode.None, DateTimeOffset.UtcNow));
    }
}
