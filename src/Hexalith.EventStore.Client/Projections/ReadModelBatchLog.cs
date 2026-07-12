using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Bounded, source-generated structured logging for the coordinated batch protocol. Only the scope hash,
/// status, profile, operation count, and a non-sensitive recovery reason are emitted; values, ETags, raw
/// keys, tenant identifiers, cursors, tokens, and exception payload dumps are never logged.
/// </summary>
internal static partial class ReadModelBatchLog {
    [LoggerMessage(
        EventId = 200110,
        Level = LogLevel.Debug,
        Message = "Read-model batch {ScopeHash} prepared on {Profile} profile with {OperationCount} operations.")]
    public static partial void Prepared(ILogger logger, string scopeHash, string profile, int operationCount);

    [LoggerMessage(
        EventId = 200111,
        Level = LogLevel.Debug,
        Message = "Read-model batch {ScopeHash} committed on {Profile} profile with {OperationCount} operations.")]
    public static partial void Committed(ILogger logger, string scopeHash, string profile, int operationCount);

    [LoggerMessage(
        EventId = 200112,
        Level = LogLevel.Information,
        Message = "Read-model batch {ScopeHash} reported {Status} on {Profile} profile ({Reason}).")]
    public static partial void Outcome(ILogger logger, string scopeHash, string status, string profile, string reason);

    [LoggerMessage(
        EventId = 200113,
        Level = LogLevel.Warning,
        Message = "Read-model batch {ScopeHash} compensated a pre-commit conflict on {Profile} profile.")]
    public static partial void Compensated(ILogger logger, string scopeHash, string profile);

    [LoggerMessage(
        EventId = 200114,
        Level = LogLevel.Warning,
        Message = "Read-model batch {ScopeHash} reconciling ambiguous dispatch on {Profile} profile ({Reason}).")]
    public static partial void Reconciling(ILogger logger, string scopeHash, string profile, string reason);
}
