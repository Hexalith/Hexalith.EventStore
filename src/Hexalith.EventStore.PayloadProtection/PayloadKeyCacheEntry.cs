// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base section 10.5.
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
