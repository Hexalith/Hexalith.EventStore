using System.Security.Claims;

namespace Hexalith.EventStore.Client.Processes;

/// <summary>Authorizes an operator's tenant-scoped process disposition.</summary>
public interface IProcessDispositionAuthorizer
{
    /// <summary>Returns whether the authenticated operator may revalidate the published source.</summary>
    Task<bool> AuthorizeAsync(ClaimsPrincipal principal, ProcessSource source, CancellationToken cancellationToken = default);
}
