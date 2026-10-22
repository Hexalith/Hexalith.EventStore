using System.Security.Claims;

using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.Server.Identity;

/// <summary>Gateway-owned asynchronous exact-operation authorization and provenance enrichment.</summary>
public interface IIdentityGatewayAdmission
{
    /// <summary>Declares operations requiring admission before lookup or effects.</summary>
    bool RequiresAdmission(string domain, string operation);

    /// <summary>Verifies authenticated tenant, target, source and actor capability before signing evidence.</summary>
    Task<string?> AdmitAsync(ClaimsPrincipal principal, IdentityAdmissionScope scope, byte[] payload,
        CancellationToken cancellationToken = default);
}
