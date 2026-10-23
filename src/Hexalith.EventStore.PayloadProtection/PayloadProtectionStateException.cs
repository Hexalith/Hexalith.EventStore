// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; base sections 9-10, 13-15; lifecycle amendment sections 1-7.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Contains a constructive bounded failure without a provider exception or stored data.</summary>
internal sealed class PayloadProtectionStateException : Exception
{
    /// <summary>Creates a typed state failure with a fixed message.</summary>
    internal PayloadProtectionStateException(UnreadableProtectedDataReason reason) : base("Payload protection state operation failed.")
    {
        Reason = Enum.IsDefined(reason) ? reason : UnreadableProtectedDataReason.ConsistencyMismatch;
    }

    /// <summary>Gets the closed public failure classification.</summary>
    internal UnreadableProtectedDataReason Reason { get; }
}
