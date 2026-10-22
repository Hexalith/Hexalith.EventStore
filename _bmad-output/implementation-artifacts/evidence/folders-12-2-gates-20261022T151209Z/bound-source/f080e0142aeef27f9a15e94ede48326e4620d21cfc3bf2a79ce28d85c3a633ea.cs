using System.ComponentModel;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Rest;
using Hexalith.McpCli.Abstractions;

namespace Hexalith.Tenants.Contracts.Queries;

/// <summary>
/// Query contract for retrieving a specific tenant's full details.
/// </summary>
[RestRoute(RestVerb.Get, "{tenantId}", ApiScope = "tenants")]
[RestQueryBinding(RestQueryBindingSource.Route, "tenantId", RestQueryBindingSource.Route, "tenantId")]
[HexalithQuery(
    "Read one tenant's full details: name, description, status (Active or Disabled), creation time, members with their roles, and configuration settings. The caller must be a member of the tenant or a global administrator. A missing tenant is reported as not found to a global administrator and as forbidden to anyone else. It is not paged: all members are returned and the pageSize, offset and cursor arguments are not used; to page a long member list, use tenants.get-tenant-users.",
    AggregateIdProperty = nameof(TenantId),
    Example = """{"TenantId":"acme"}""")]
public sealed class GetTenantQuery : IQueryContract {
    [HexalithIdentifier]
    [Description("Identifier of the tenant to read. Required; a call without it is rejected.")]
    public string TenantId { get; init; } = string.Empty;

    public static string QueryType => "get-tenant";

    public static string Domain => "tenants";

    public static string ProjectionType => "tenants";
}
