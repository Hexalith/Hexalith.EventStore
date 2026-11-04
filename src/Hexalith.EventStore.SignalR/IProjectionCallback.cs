namespace Hexalith.EventStore.SignalR;

/// <summary>Owns revocation and completion independently of the signal payload type.</summary>
internal interface IProjectionCallback
{
    /// <summary>Gets the caller's callback identity.</summary>
    Delegate Original { get; }

    /// <summary>Gets whether this thread is executing the registration.</summary>
    bool IsExecuting { get; }

    /// <summary>Gets completion of every invocation after revocation.</summary>
    Task Completion { get; }

    /// <summary>Revokes entry and drains calls except the current callback frame.</summary>
    Task RevokeAndDrainAsync();
}
