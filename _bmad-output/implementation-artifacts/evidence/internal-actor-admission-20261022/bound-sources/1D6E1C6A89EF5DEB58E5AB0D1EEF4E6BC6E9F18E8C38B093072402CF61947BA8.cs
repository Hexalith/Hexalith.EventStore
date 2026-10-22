namespace Hexalith.EventStore.Server.Commands;

/// <summary>
/// Describes the advisory result of adding a message to the correlation index.
/// </summary>
public enum CommandCorrelationIndexAddOutcome
{
    /// <summary>The new mapping was stored.</summary>
    Added,

    /// <summary>The mapping already existed and no duplicate was added.</summary>
    Duplicate,

    /// <summary>The index remained full and was marked overflowed without evicting live data.</summary>
    Overflow,

    /// <summary>ETag conflicts exhausted the bounded retry budget.</summary>
    RetryExhausted,
}
