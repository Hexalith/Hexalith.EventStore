// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; amendment sections 1-3.
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
