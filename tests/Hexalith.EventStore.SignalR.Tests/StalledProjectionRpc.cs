namespace Hexalith.EventStore.SignalR.Tests;

/// <summary>Per-host deterministic controls for real group invocations.</summary>
public sealed class StalledProjectionRpc
{
    /// <summary>Gets the join entry notification.</summary>
    public TaskCompletionSource JoinEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the leave entry notification.</summary>
    public TaskCompletionSource LeaveEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets notification that the delayed join has actually finished on the server.</summary>
    public TaskCompletionSource JoinFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets notification that the delayed leave has actually finished on the server.</summary>
    public TaskCompletionSource LeaveFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the join acknowledgement release.</summary>
    public TaskCompletionSource ReleaseJoin { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the leave acknowledgement release.</summary>
    public TaskCompletionSource ReleaseLeave { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
