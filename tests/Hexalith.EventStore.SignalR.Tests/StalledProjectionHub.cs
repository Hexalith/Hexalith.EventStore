using Microsoft.AspNetCore.SignalR;

namespace Hexalith.EventStore.SignalR.Tests;

/// <summary>A real transport hub whose group acknowledgement can be held by a test.</summary>
public sealed class StalledProjectionHub(StalledProjectionRpc control) : Hub
{
    /// <summary>Holds a join acknowledgement until explicitly released.</summary>
    public async Task JoinGroup(string projectionType, string tenantId)
    {
        control.JoinEntered.TrySetResult();
        await control.ReleaseJoin.Task.ConfigureAwait(false);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"{projectionType}:{tenantId}").ConfigureAwait(false);
        control.JoinFinished.TrySetResult();
    }

    /// <summary>Holds a leave acknowledgement until explicitly released.</summary>
    public async Task LeaveGroup(string projectionType, string tenantId)
    {
        control.LeaveEntered.TrySetResult();
        await control.ReleaseLeave.Task.ConfigureAwait(false);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"{projectionType}:{tenantId}").ConfigureAwait(false);
        control.LeaveFinished.TrySetResult();
    }
    /// <summary>Holds a scoped detail join acknowledgement.</summary>
    public Task JoinGroupScoped(string projectionType, string tenantId, string? groupScope)
        => JoinGroup(projectionType, groupScope is null ? tenantId : $"{tenantId}:{groupScope}");

    /// <summary>Holds a scoped detail leave acknowledgement.</summary>
    public Task LeaveGroupScoped(string projectionType, string tenantId, string? groupScope)
        => LeaveGroup(projectionType, groupScope is null ? tenantId : $"{tenantId}:{groupScope}");
}
