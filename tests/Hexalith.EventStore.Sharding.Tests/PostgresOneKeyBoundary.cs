namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Observable boundaries of the ordinary implicit single-operation save.</summary>
internal enum PostgresOneKeyBoundary
{
    /// <summary>The complete execution-capable frame has not reached PostgreSQL.</summary>
    BeforeExecution,
    /// <summary>Successful command completion and idle commit were observed, with the response withheld.</summary>
    CommittedIdle,
}
