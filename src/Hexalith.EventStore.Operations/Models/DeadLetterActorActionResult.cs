namespace Hexalith.EventStore.Operations.Models;

/// <summary>
/// Reports one actor action without revealing whether a hidden item exists.
/// </summary>
/// <param name="Success">Whether every requested action completed.</param>
/// <param name="ReasonCode">A bounded non-sensitive result code.</param>
public sealed record DeadLetterActorActionResult(bool Success, string ReasonCode);
