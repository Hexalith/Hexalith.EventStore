using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Identity;

/// <summary>Trusted gateway-only private-key signing capability; domain clients receive public keys only.</summary>
public interface IIdentityAdmissionSigner
{
    /// <summary>Signs evidence after authenticated source and exact-operation policy checks.</summary>
    string Sign(IdentityAdmissionEvidence evidence);
}
