// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base section 10.5.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Holds one cache-owned mutable key with an absolute monotonic lifetime and deterministic insertion order.</summary>
/// <param name="Bytes">The cache-owned key, never returned directly.</param>
/// <param name="CreatedAt">The monotonic insertion timestamp.</param>
/// <param name="Ordinal">The deterministic eviction ordinal.</param>
internal sealed record PayloadKeyCacheEntry(byte[] Bytes, long CreatedAt, ulong Ordinal)
{
    /// <summary>Never formats key material.</summary>
    public override string ToString() => nameof(PayloadKeyCacheEntry);
}
