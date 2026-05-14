namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Stable public query warning codes.
/// </summary>
public static class QueryWarningCodes {
    /// <summary>
    /// Search behavior was served through a degraded path.
    /// </summary>
    public const string DegradedSearch = "degraded_search";

    /// <summary>
    /// ETag metadata was unavailable, so cache optimization failed open.
    /// </summary>
    public const string ETagUnavailable = "etag_unavailable";
}
