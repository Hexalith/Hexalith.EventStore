namespace Hexalith.EventStore.Operations.Models;

/// <summary>
/// Stores the ordered identifiers retained by one topic drain actor.
/// </summary>
/// <param name="MessageIds">The oldest-first message identifiers.</param>
public sealed record DeadLetterIndex(IReadOnlyList<string> MessageIds)
{
    /// <summary>Gets an empty index.</summary>
    public static DeadLetterIndex Empty { get; } = new([]);
}
