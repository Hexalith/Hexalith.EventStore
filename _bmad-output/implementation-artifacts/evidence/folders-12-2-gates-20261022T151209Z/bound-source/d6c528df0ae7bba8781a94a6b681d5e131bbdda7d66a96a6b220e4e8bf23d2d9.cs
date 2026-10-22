using System.ComponentModel;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Rest;
using Hexalith.McpCli.Abstractions;

namespace Hexalith.Tenants.Contracts.Queries;

/// <summary>
/// Query contract for retrieving users in a specific tenant with their roles.
/// </summary>
[RestRoute(RestVerb.Get, "{tenantId}/users", ApiScope = "tenants")]
[RestQueryBinding(RestQueryBindingSource.Route, "tenantId", RestQueryBindingSource.Route, "tenantId")]
[HexalithQuery(
    "List the members of one tenant with their roles, ordered by user identifier. The caller must be a member of the tenant or a global administrator; a missing tenant is reported as not found to a global administrator and as forbidden to anyone else. Results are paged: pageSize takes 1 to 100 (default 20, larger values are reduced to 100); to read the next page, pass the returned next-page cursor as the cursor argument: it is the result document's cursor, also reported as paging.nextCursor when a paging argument was sent. offset is not supported.",
    AggregateIdProperty = nameof(TenantId),
    Example = """{"TenantId":"acme"}""")]
public sealed class GetTenantUsersQuery : IQueryContract {
    [HexalithIdentifier]
    [Description("Identifier of the tenant whose members are listed. Required; a call without it is rejected.")]
    public string TenantId { get; init; } = string.Empty;

    [Description("Has no effect: use the pageSize and cursor call arguments instead.")]
    public string? Cursor { get; init; }

    [Description("Has no effect: use the pageSize and cursor call arguments instead.")]
    public int PageSize { get; init; }

    public static string QueryType => "get-tenant-users";

    public static string Domain => "tenants";

    public static string ProjectionType => "tenants";
}
