namespace Hexalith.EventStore.Operations.Models;

/// <summary>
/// Reports one actor action without revealing whether a hidden item exists.
/// </summary>
/// <param name="Success">Whether every requested action completed.</param>
/// <param name="ReasonCode">A bounded non-sensitive result code.</param>
public sealed record DeadLetterActorActionResult(bool Success, string ReasonCode)
{
    /// <summary>Gets the stable identity of the persisted action.</summary>
    public string? OperationId { get; init; }

    /// <summary>Gets the durable action state.</summary>
    public string? State { get; init; }
}
