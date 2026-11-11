using Microsoft.AspNetCore.SignalR;

namespace Hexalith.EventStore.SignalR.Tests;

/// <summary>A real transport hub whose group acknowledgement can be held by a test.</summary>
public sealed class StalledProjectionHub(StalledProjectionRpc control) : Hub
{
    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        await control.Connections.Writer.WriteAsync(Context.ConnectionId).ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await control.Disconnections.Writer.WriteAsync(Context.ConnectionId).ConfigureAwait(false);
        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    /// <summary>Holds a join acknowledgement until explicitly released.</summary>
    public async Task JoinGroup(string projectionType, string tenantId)
    {
        if (control.HoldJoins && (control.HeldProjectionType is null || control.HeldProjectionType == projectionType))
        {
            control.JoinEntered.TrySetResult();
            await control.ReleaseJoin.Task.ConfigureAwait(false);
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, $"{projectionType}:{tenantId}").ConfigureAwait(false);
        await control.JoinFinished.Writer.WriteAsync((Context.ConnectionId, $"{projectionType}:{tenantId}")).ConfigureAwait(false);
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
