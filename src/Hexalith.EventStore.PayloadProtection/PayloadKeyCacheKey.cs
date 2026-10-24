// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base section 10.5.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>The complete scoped, versioned and epoch-bound cache identity.</summary>
/// <param name="ScopeDigest">The exact authenticated scope address.</param>
/// <param name="KeyReference">The canonical durable DEK reference.</param>
/// <param name="DekVersion">The positive DEK version.</param>
/// <param name="LifecycleEpoch">The strongly admitted lifecycle epoch.</param>
/// <param name="ProviderKeyVersionId">The exact recorded provider version, confined to engine memory.</param>
internal sealed record PayloadKeyCacheKey(string ScopeDigest, string KeyReference, uint DekVersion,
    ulong LifecycleEpoch, string ProviderKeyVersionId)
{
    /// <summary>Never exposes key or provider identities through formatting.</summary>
    public override string ToString() => nameof(PayloadKeyCacheKey);
}
