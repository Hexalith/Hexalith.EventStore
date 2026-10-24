// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 10.2-10.4.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Complete scope-index evidence; null head is permitted only for first conditional creation.</summary>
/// <param name="ScopeDigest">The authenticated scope address.</param>
/// <param name="Head">The head and ETag, or null for an absent initial index.</param>
/// <param name="Pages">All declared pages in page-number order.</param>
internal sealed record ScopeKeyIndexSnapshot(string ScopeDigest, PayloadProtectionStateValue<ScopeIndexHeadV1>? Head,
    IReadOnlyList<PayloadProtectionStateValue<ScopeIndexPageV1>> Pages)
{
    /// <summary>Formats no scope or key identities.</summary>
    public override string ToString() => nameof(ScopeKeyIndexSnapshot);
}
