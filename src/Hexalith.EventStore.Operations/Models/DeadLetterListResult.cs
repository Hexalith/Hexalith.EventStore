namespace Hexalith.EventStore.Operations.Models;

/// <summary>
/// Represents a redacted actor page.
/// </summary>
/// <param name="Items">The page items.</param>
/// <param name="TotalCount">The total matching open item count.</param>
/// <param name="NextOffset">The next offset, or null.</param>
public sealed record DeadLetterListResult(
    IReadOnlyList<DeadLetterListItem> Items,
    int TotalCount,
    int? NextOffset)
{
    /// <summary>Gets the first observed failure among the authorized open entries.</summary>
    public DateTimeOffset? OldestFailureAt { get; init; }
}
