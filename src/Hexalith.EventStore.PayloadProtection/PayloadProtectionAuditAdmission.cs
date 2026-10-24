// Effective authority: 08bfd5855998a276999db38447c409b2231f3d4e253d4e371d4d8bfe369462d6; amendment section 6; base section 15.
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>Fails audit-scope admission constructively without allowing policy exception text across the engine boundary.</summary>
internal static class PayloadProtectionAuditAdmission
{
    /// <summary>Requires explicit affirmative permission before scope-bearing durable work is admitted.</summary>
    internal static void Require(Func<AggregateIdentity, bool>? permission, AggregateIdentity identity)
    {
        bool allowed;
        try { allowed = permission?.Invoke(identity) == true; }
        catch { allowed = false; }
        if (!allowed) { throw new PayloadProtectionStateException(UnreadableProtectedDataReason.ProviderDenied); }
    }
}
