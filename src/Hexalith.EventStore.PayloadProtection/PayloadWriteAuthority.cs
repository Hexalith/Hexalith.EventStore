// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 10.3 and 13; amendment sections 3-4.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Current write authority and its exact CAS conditions, confined to one operation.</summary>
/// <param name="Lifecycle">The strongly observed lifecycle.</param>
/// <param name="Fence">The strongly observed operator fence and irreversible watermark.</param>
/// <param name="Conditions">The open-deployment and serving-member comparisons.</param>
internal sealed record PayloadWriteAuthority(PayloadProtectionStateValue<LifecycleV1> Lifecycle,
    PayloadProtectionStateValue<FenceV1> Fence, IReadOnlyList<PayloadProtectionStateMutation> Conditions)
{
    /// <summary>Formats no authority identities or comparison values.</summary>
    public override string ToString() => nameof(PayloadWriteAuthority);
}
