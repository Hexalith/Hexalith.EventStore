using System.Security.Claims;

namespace Hexalith.EventStore.Client.Processes;

/// <summary>Durably records privileged process disposition before any mutation.</summary>
public interface IProcessDispositionAuditSink
{
    /// <summary>Appends one attributed audit record; failure must prevent disposition.</summary>
    Task RecordAsync(ClaimsPrincipal principal, ProcessSource source, string reason, CancellationToken cancellationToken = default);
}
