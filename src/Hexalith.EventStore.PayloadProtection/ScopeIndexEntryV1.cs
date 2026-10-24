// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9-10, 13-15; lifecycle amendment sections 1-8.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed ScopeIndexEntryV1 durable schema; encodings are checked by the state codec.</summary>
/// <param name="KeyRef">The canonical keyRef field.</param>
/// <param name="DekVersion">The canonical dekVersion field.</param>
/// <param name="ProviderVersionDigest">The canonical providerVersionDigest field.</param>
/// <param name="State">The canonical state field.</param>
internal sealed record ScopeIndexEntryV1(
    string KeyRef,
    uint DekVersion,
    string ProviderVersionDigest,
    string State)
{
    /// <summary>Returns only a bounded schema name.</summary>
    public override string ToString() => nameof(ScopeIndexEntryV1);
}
