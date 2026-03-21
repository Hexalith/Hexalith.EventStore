namespace Hexalith.EventStore.Admin.Abstractions.Models.Common;

/// <summary>
/// A paginated result container for list operations.
/// </summary>
/// <typeparam name="T">The type of items in the result.</typeparam>
/// <param name="Items">The items in the current page.</param>
/// <param name="TotalCount">The total number of items across all pages.</param>
/// <param name="ContinuationToken">An opaque token for fetching the next page, or null if this is the last page.</param>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, string? ContinuationToken)
{
    /// <summary>Gets the items in the current page.</summary>
    public IReadOnlyList<T> Items { get; } = Items ?? throw new ArgumentNullException(nameof(Items));
}
