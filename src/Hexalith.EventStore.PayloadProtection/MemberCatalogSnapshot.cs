// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; amendment sections 1-3.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>A complete revision-consistent catalog observation, retained only for an enclosing CAS transaction.</summary>
/// <param name="Head">The confirmed head and its comparison metadata.</param>
/// <param name="Pages">All declared pages in page-number order.</param>
internal sealed record MemberCatalogSnapshot(PayloadProtectionStateValue<MemberHeadV1> Head,
    IReadOnlyList<PayloadProtectionStateValue<MemberPageV1>> Pages)
{
    /// <summary>Returns no member identity or comparison metadata.</summary>
    public override string ToString() => nameof(MemberCatalogSnapshot);
}
