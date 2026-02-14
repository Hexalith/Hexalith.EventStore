namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Checks for and records command idempotency using actor state.
/// </summary>
public interface IIdempotencyChecker
{
    /// <summary>
    /// Checks whether a command with the specified causation ID has already been processed.
    /// </summary>
    /// <param name="causationId">The causation identifier to check.</param>
    /// <returns>The cached result if found; otherwise, <c>null</c>.</returns>
    Task<CommandProcessingResult?> CheckAsync(string causationId);

    /// <summary>
    /// Records a command processing result for future idempotency checks.
    /// </summary>
    /// <param name="causationId">The causation identifier to record.</param>
    /// <param name="result">The processing result to cache.</param>
    Task RecordAsync(string causationId, CommandProcessingResult result);
}
