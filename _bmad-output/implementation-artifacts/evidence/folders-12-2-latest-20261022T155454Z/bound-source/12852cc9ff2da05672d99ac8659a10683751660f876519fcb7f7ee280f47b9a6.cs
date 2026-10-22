namespace Hexalith.EventStore.Contracts.Projections;

/// <summary>
/// Closed durable outcome classification for named projection dispatch.
/// </summary>
public enum ProjectionDispatchStatus {
    /// <summary>Required persistence completed durably in this dispatch.</summary>
    Completed = 0,

    /// <summary>The same stable dispatch identity had already completed durably.</summary>
    AlreadyCompleted = 1,

    /// <summary>Known incomplete or conflicting work may converge when retried with the same identity.</summary>
    Retryable = 2,

    /// <summary>Durable completion could not be proven.</summary>
    Indeterminate = 3,

    /// <summary>A known terminal validation, configuration, or domain failure occurred.</summary>
    Failed = 4,
}
