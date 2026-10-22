namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Supplies one domain's strongly observed erasure state to the payload-protection boundary.
/// Implements Story 8.1 section 9 under normative digest
/// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
/// </summary>
public interface IErasureStateProvider {
    /// <summary>Gets the exact public contract version.</summary>
    int ContractVersion => 1;

    /// <summary>Gets the current erasure state for the requested aggregate and key reference.</summary>
    /// <param name="request">The provider-neutral erasure-state request.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>The bounded state, epoch, reason, and observation time.</returns>
    ValueTask<PayloadErasureStateResult> GetStateAsync(
        PayloadErasureStateRequest request,
        CancellationToken cancellationToken = default);
}
