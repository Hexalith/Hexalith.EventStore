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

    // Story 4.4 deliberately does NOT add TryCompleteRecoverableAsync here. This interface is public
    // and Hexalith.EventStore.Server ships as a NuGet package, so a new member with no default
    // implementation would break every external implementer. It would also buy nothing: the only
    // caller (AggregateActor.CompleteRecoverableIdempotencyAsync) constructs IdempotencyChecker
    // concretely, so the method lives on the concrete type where it is actually used.
}
