// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13-15; lifecycle amendment sections 1-7.
using System.Text.Json.Serialization;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The closed KekInventoryEntryV1 durable schema; encodings are checked by the state codec.</summary>
/// <param name="ScopeDigest">The canonical scopeDigest field.</param>
/// <param name="KeyRef">The canonical keyRef field.</param>
/// <param name="DekVersion">The canonical dekVersion field.</param>
internal sealed record KekInventoryEntryV1(
    string ScopeDigest,
    string KeyRef,
    uint DekVersion)
{
    /// <summary>Returns only a bounded schema name.</summary>
    public override string ToString() => nameof(KekInventoryEntryV1);
}
