using Hexalith.EventStore.DomainService.DurableTasks;

namespace Hexalith.EventStore.DurableTaskLookupHost;

/// <summary>Denies interactive operations in the isolated lookup host.</summary>
internal sealed class LookupOnlyAdmissionAuthorizer : IDurableTaskAdmissionAuthorizer
{
    /// <inheritdoc/>
    public ValueTask<DurableTaskAdmissionAuthority> AuthorizeAsync(DurableTaskScope scope, string requestSha256, CancellationToken cancellationToken)
        => ValueTask.FromResult(new DurableTaskAdmissionAuthority(false, false, null));
}
