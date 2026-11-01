using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Client.Projections.Checkpointed;

/// <summary>
/// Content-free, source-generated diagnostics for checkpointed projections.
/// </summary>
internal static partial class CheckpointedProjectionLog {
    [LoggerMessage(
        EventId = 200301,
        Level = LogLevel.Warning,
        Message = "Checkpointed projection {StateKey} in store {StateStoreName} became corrupt at sequence {Sequence}: {Reason}.")]
    internal static partial void Corrupted(
        ILogger logger,
        string stateStoreName,
        string stateKey,
        long sequence,
        CheckpointedProjectionCorruptionReason reason);

    [LoggerMessage(
        EventId = 200302,
        Level = LogLevel.Warning,
        Message = "Checkpointed projection {StateKey} in store {StateStoreName} is gapped: expected {Expected}, observed through {RecoveryThrough}.")]
    internal static partial void Gapped(
        ILogger logger,
        string stateStoreName,
        string stateKey,
        long expected,
        long recoveryThrough);

    [LoggerMessage(
        EventId = 200303,
        Level = LogLevel.Information,
        Message = "Checkpointed projection {StateKey} in store {StateStoreName} recovered through sequence {Sequence} under fencing token {FencingToken}.")]
    internal static partial void Recovered(
        ILogger logger,
        string stateStoreName,
        string stateKey,
        long sequence,
        long fencingToken);

    [LoggerMessage(
        EventId = 200304,
        Level = LogLevel.Debug,
        Message = "Checkpointed projection {StateKey} in store {StateStoreName} lost compare-and-swap attempt {Attempt} of {MaxAttempts}.")]
    internal static partial void ConcurrencyConflict(
        ILogger logger,
        string stateStoreName,
        string stateKey,
        int attempt,
        int maxAttempts);

    [LoggerMessage(
        EventId = 200305,
        Level = LogLevel.Warning,
        Message = "Checkpointed projection {StateKey} in store {StateStoreName} is unavailable: {Reason}. The delivery stays retryable.")]
    internal static partial void Unavailable(
        ILogger logger,
        string stateStoreName,
        string stateKey,
        CheckpointedProjectionUnavailableReason reason);
}
