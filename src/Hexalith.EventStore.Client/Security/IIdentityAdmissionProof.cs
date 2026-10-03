using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Client.Security;

/// <summary>Verifies gateway-owned, time-bounded exact-operation admission.</summary>
public interface IIdentityAdmissionProof
{
    /// <summary>Returns evidence only when its signature, scope, current policy and validity match.</summary>
    IdentityAdmissionEvidence? Verify(string? proof, IdentityAdmissionScope expected);
}
