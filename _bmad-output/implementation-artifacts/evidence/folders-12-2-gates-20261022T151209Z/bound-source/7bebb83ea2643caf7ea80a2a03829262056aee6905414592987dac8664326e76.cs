using System.ComponentModel;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Rest;
using Hexalith.McpCli.Abstractions;

namespace Hexalith.Tenants.Contracts.Queries;

/// <summary>
/// Query contract for listing tenants with cursor-based pagination.
/// </summary>
[RestRoute(RestVerb.Get, "", ApiScope = "tenants")]
[HexalithQuery(
    "List the tenants visible to the caller, ordered by tenant identifier: every tenant for a global administrator, otherwise only the tenants the caller is a member of. Each item gives the tenant identifier, name and status (Active or Disabled). Results are paged: pageSize takes 1 to 100 (default 20, larger values are reduced to 100); to read the next page, pass the returned next-page cursor as the cursor argument: it is the result document's cursor, also reported as paging.nextCursor when a paging argument was sent. offset is not supported.",
    AggregateId = "index",
    Example = "{}")]
public sealed class ListTenantsQuery : IQueryContract {
    [Description("Has no effect: use the pageSize and cursor call arguments instead.")]
    public string? Cursor { get; init; }

    [Description("Has no effect: use the pageSize and cursor call arguments instead.")]
    public int PageSize { get; init; }

    public static string QueryType => "list-tenants";

    public static string Domain => "tenants";

    public static string ProjectionType => "tenant-index";
}
