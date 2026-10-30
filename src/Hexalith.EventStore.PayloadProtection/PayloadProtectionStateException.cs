// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base sections 9-10, 13-15; lifecycle amendment sections 1-8.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Contains a constructive bounded failure without a provider exception or stored data.</summary>
internal sealed class PayloadProtectionStateException : Exception
{
    /// <summary>Creates a typed state failure with a fixed message.</summary>
    internal PayloadProtectionStateException(UnreadableProtectedDataReason reason, bool isAdmissionLimit = false) : base("Payload protection state operation failed.")
    {
        Reason = Enum.IsDefined(reason) ? reason : UnreadableProtectedDataReason.ConsistencyMismatch;
        IsAdmissionLimit = isAdmissionLimit;
    }

    /// <summary>Gets the closed public failure classification.</summary>
    internal UnreadableProtectedDataReason Reason { get; }

    /// <summary>Gets whether a qualified operational limit rejected admission before submitting a transaction.</summary>
    internal bool IsAdmissionLimit { get; }
}
