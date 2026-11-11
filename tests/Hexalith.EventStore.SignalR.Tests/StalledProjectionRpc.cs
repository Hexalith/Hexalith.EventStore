namespace Hexalith.EventStore.SignalR.Tests;

/// <summary>Per-host deterministic controls for real group invocations.</summary>
public sealed class StalledProjectionRpc
{
    /// <summary>Gets actual physical connection identities as transports connect.</summary>
    public System.Threading.Channels.Channel<string> Connections { get; } = System.Threading.Channels.Channel.CreateUnbounded<string>();

    /// <summary>Gets actual physical connection termination notifications.</summary>
    public System.Threading.Channels.Channel<string> Disconnections { get; } = System.Threading.Channels.Channel.CreateUnbounded<string>();

    /// <summary>Gets whether subsequent joins pause while previously established groups stay available.</summary>
    public bool HoldJoins { get; set; } = true;

    /// <summary>Gets the optional projection whose join is held independently of surviving groups.</summary>
    public string? HeldProjectionType { get; set; }

    /// <summary>Gets the join entry notification.</summary>
    public TaskCompletionSource JoinEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the leave entry notification.</summary>
    public TaskCompletionSource LeaveEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets notification that the delayed join has actually finished on the server.</summary>
    public System.Threading.Channels.Channel<(string ConnectionId, string Group)> JoinFinished { get; } = System.Threading.Channels.Channel.CreateUnbounded<(string ConnectionId, string Group)>();

    /// <summary>Gets notification that the delayed leave has actually finished on the server.</summary>
    public TaskCompletionSource LeaveFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the join acknowledgement release.</summary>
    public TaskCompletionSource ReleaseJoin { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the leave acknowledgement release.</summary>
    public TaskCompletionSource ReleaseLeave { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
