namespace Hexalith.EventStore.Server.Actors;

/// <summary>
/// Checks for and records command idempotency using actor state.
/// </summary>
public interface IIdempotencyChecker
{
    /// <summary>
    /// Checks idempotency using the complete normalized command identity.
    /// </summary>
    /// <param name="identity">The exact command identity to check.</param>
    /// <returns>An explicit lookup result that distinguishes misses, duplicates, recovery, and conflicts.</returns>
    Task<IdempotencyCheckResult> CheckAsync(CommandProcessingIdentity identity);

    /// <summary>Inspects only the exact message-keyed record without staging migration or other mutation.</summary>
    Task<IdempotencyCheckResult> InspectAsync(CommandProcessingIdentity identity);

    /// <summary>
    /// Stages a command processing result under its message-id key.
    /// </summary>
    /// <param name="identity">The exact command identity to record.</param>
    /// <param name="result">The processing result to store.</param>
    /// <param name="expiresAt">The application-visible expiration time.</param>
    /// <param name="disposition">Whether the record is terminal or recoverable.</param>
    Task RecordAsync(
        CommandProcessingIdentity identity,
        CommandProcessingResult result,
        DateTimeOffset expiresAt,
        IdempotencyRecordDisposition disposition);
}
