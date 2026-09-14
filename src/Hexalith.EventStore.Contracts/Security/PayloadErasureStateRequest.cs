using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Requests the domain-owned erasure state for an aggregate and optional durable DEK reference.
/// Implements Story 8.1 section 9 under normative digest
/// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
/// </summary>
/// <param name="Identity">The aggregate identity whose state is requested.</param>
/// <param name="KeyReference">The optional canonical durable DEK reference.</param>
/// <param name="DekVersion">The optional positive DEK version.</param>
public sealed record PayloadErasureStateRequest(
    AggregateIdentity Identity,
    string? KeyReference,
    uint? DekVersion) {
    /// <summary>Returns a bounded diagnostic name without identity or key-reference data.</summary>
    /// <returns>The contract type name.</returns>
    public override string ToString() => nameof(PayloadErasureStateRequest);
}
