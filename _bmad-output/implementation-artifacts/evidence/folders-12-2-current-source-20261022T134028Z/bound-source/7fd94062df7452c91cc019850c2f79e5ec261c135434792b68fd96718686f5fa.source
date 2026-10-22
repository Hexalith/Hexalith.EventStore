using System.ComponentModel;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Rest;
using Hexalith.McpCli.Abstractions;

namespace Hexalith.Tenants.Contracts.Queries;

/// <summary>
/// Query contract for retrieving tenants a specific user belongs to.
/// </summary>
[RestRoute(RestVerb.Get, "~/api/users/{userId}/tenants", ApiScope = "tenants")]
[RestQueryBinding(RestQueryBindingSource.Constant, "index", RestQueryBindingSource.Route, "userId")]
[HexalithQuery(
    "List the tenants one user is a member of, ordered by tenant identifier, with each tenant's name, status and the user's role. Callers always see their own memberships, a global administrator sees any user's, and a TenantOwner sees another user's memberships only in tenants the caller owns. Memberships the caller may not see are left out, so the list can be empty. Results are paged: pageSize takes 1 to 100 (default 20, larger values are reduced to 100); to read the next page, pass the returned next-page cursor as the cursor argument: it is the result document's cursor, also reported as paging.nextCursor when a paging argument was sent. offset is not supported.",
    AggregateId = "index",
    Example = """{"UserId":"8d3f2a61-5b7e-4c0a-9e21-3f4b6c7d8e90"}""")]
public sealed class GetUserTenantsQuery : IQueryContract {
    [HexalithIdentifier]
    [Description("Identifier of the user whose memberships are listed: the subject (sub) claim the identity provider issues for that user. Always set it, including for your own memberships: an omitted UserId does not default to you and usually returns an empty list. To list your own tenants without knowing your subject, use tenants.list-tenants; it returns no roles, and a global administrator gets every tenant there.")]
    public string UserId { get; init; } = string.Empty;

    [Description("Has no effect: use the pageSize and cursor call arguments instead.")]
    public string? Cursor { get; init; }

    [Description("Has no effect: use the pageSize and cursor call arguments instead.")]
    public int PageSize { get; init; }

    public static string QueryType => "get-user-tenants";

    public static string Domain => "tenants";

    public static string ProjectionType => "tenant-index";
}
