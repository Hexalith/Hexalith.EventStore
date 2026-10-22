namespace Hexalith.EventStore.Operations.Models;

/// <summary>
/// Reports the result of a durable capture attempt.
/// </summary>
/// <param name="Outcome">The bounded capture outcome.</param>
public sealed record DeadLetterCaptureResult(DeadLetterCaptureOutcome Outcome);
