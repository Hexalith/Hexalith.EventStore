namespace Hexalith.EventStore.DomainService.DurableTasks;

/// <summary>Provides fresh authoritative decisions for the admitted task's original actor.</summary>
public interface IDurableTaskAdmissionAuthorizer
{
    /// <summary>Evaluates the host's authenticated caller and owner authority for the exact scoped intent.</summary>
    /// <param name="scope">The requested original scope, to be checked against independently verified authority.</param>
    /// <param name="requestSha256">The normalized request hash for the registered operation.</param>
    /// <param name="cancellationToken">The caller cancellation token.</param>
    /// <returns>The decision and any current interactive evidence. Unavailability must fail closed.</returns>
    ValueTask<DurableTaskAdmissionAuthority> AuthorizeAsync(
        DurableTaskScope scope,
        string requestSha256,
        CancellationToken cancellationToken);
}
