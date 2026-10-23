// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 10.2-10.4.
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
