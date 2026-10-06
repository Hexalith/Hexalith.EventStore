using System.Security.Claims;

using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Server.Security;

/// <summary>Separately grants exact-source retained attribution from current authenticated authority.</summary>
public interface IRetainedIdentityHistoryAdmission
{
    /// <summary>Authorizes before source lookup, never from profile type, a client flag or cached role.</summary>
    Task<RetainedIdentityHistoryGrant?> AdmitAsync(ClaimsPrincipal principal, RetainedIdentityHistoryReadRequest request,
        CancellationToken cancellationToken = default);
}
