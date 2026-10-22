namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Public ordering expression placeholder for gateway query policy.
/// </summary>
/// <param name="Field">The public field identifier to order by.</param>
/// <param name="Direction">The requested sort direction.</param>
public sealed record QuerySort(string Field, QuerySortDirection Direction = QuerySortDirection.Ascending);

/// <summary>
/// Sort direction values for public query ordering policy.
/// </summary>
public enum QuerySortDirection {
    /// <summary>
    /// Sort in ascending order.
    /// </summary>
    Ascending,

    /// <summary>
    /// Sort in descending order.
    /// </summary>
    Descending,
}
