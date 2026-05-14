namespace Hexalith.EventStore.Contracts.Queries;

/// <summary>
/// Public query policy limits shared by gateway validation, documentation, clients, and fakes.
/// </summary>
public static class QueryPolicyLimits {
    /// <summary>
    /// Default page size used when a caller omits query paging policy.
    /// </summary>
    public const int DefaultPageSize = 50;

    /// <summary>
    /// Maximum page size accepted by the gateway query policy contract.
    /// </summary>
    public const int MaxPageSize = 200;
}
