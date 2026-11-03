using Microsoft.AspNetCore.SignalR;

namespace Hexalith.EventStore.Server.Tests.SignalR;

/// <summary>Wraps the actual backend to distinguish execution from acknowledgement failure.</summary>
/// <param name="inner">The real SignalR backend.</param>
/// <param name="failure">The single fault control.</param>
public sealed class FaultingProjectionGroupManager(IGroupManager inner, ProjectionGroupFailure failure) : IGroupManager
{
    /// <inheritdoc/>
    public async Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        await inner.AddToGroupAsync(connectionId, groupName, cancellationToken);
        if (failure.AddAfterApply && failure.Fail(groupName)) { throw new IOException("Applied admission acknowledgement was lost."); }
    }

    /// <inheritdoc/>
    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        if (!failure.AddAfterApply && failure.Fail(groupName)) { throw new IOException("Removal failed before execution."); }
        return inner.RemoveFromGroupAsync(connectionId, groupName, cancellationToken);
    }
}
