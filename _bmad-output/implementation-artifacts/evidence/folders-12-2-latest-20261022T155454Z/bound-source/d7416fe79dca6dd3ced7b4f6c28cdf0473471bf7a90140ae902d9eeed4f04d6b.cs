namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>
/// Public lifecycle states for projection rebuild operations.
/// </summary>
public enum ProjectionRebuildStatus {
    /// <summary>The rebuild has not started.</summary>
    NotStarted,

    /// <summary>The rebuild is running.</summary>
    Running,

    /// <summary>A pause has been requested.</summary>
    Pausing,

    /// <summary>The rebuild is paused.</summary>
    Paused,

    /// <summary>A resume has been requested.</summary>
    Resuming,

    /// <summary>A cancel has been requested.</summary>
    Canceling,

    /// <summary>The rebuild was canceled.</summary>
    Canceled,

    /// <summary>The rebuild is retrying after a transient failure.</summary>
    Retrying,

    /// <summary>The rebuild completed successfully.</summary>
    Succeeded,

    /// <summary>The rebuild failed.</summary>
    Failed,
}
