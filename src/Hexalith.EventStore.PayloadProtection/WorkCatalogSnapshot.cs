// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; amendment sections 1-4.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>A complete revision-consistent pending-work catalog observation.</summary>
/// <param name="Head">The confirmed head and its comparison metadata.</param>
/// <param name="Pages">All declared pages in page-number order.</param>
internal sealed record WorkCatalogSnapshot(PayloadProtectionStateValue<WorkHeadV1> Head,
    IReadOnlyList<PayloadProtectionStateValue<WorkPageV1>> Pages)
{
    /// <summary>Returns no work identity or comparison metadata.</summary>
    public override string ToString() => nameof(WorkCatalogSnapshot);
}
