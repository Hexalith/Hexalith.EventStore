// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; amendment sections 1-4.
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
