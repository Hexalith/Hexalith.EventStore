// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 10.2-10.4.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Complete environment-wide KEK inventory evidence, retaining its exact comparison metadata.</summary>
/// <param name="BackendDigest">The exact backend address digest.</param>
/// <param name="ProviderDigest">The exact provider-version address digest.</param>
/// <param name="Head">The head and ETag, or null for an absent initial inventory.</param>
/// <param name="Pages">All declared pages in page-number order.</param>
internal sealed record KekKeyIndexSnapshot(string BackendDigest, string ProviderDigest,
    PayloadProtectionStateValue<KekInventoryHeadV1>? Head, IReadOnlyList<PayloadProtectionStateValue<KekInventoryPageV1>> Pages)
{
    /// <summary>Formats no backend, provider or key identities.</summary>
    public override string ToString() => nameof(KekKeyIndexSnapshot);
}
