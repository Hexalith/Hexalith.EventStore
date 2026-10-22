using System.ComponentModel;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Contracts.Rest;
using Hexalith.McpCli.Abstractions;
using Hexalith.Tenants.Contracts.Enums;

namespace Hexalith.Tenants.Contracts.Queries;

/// <summary>
/// Query contract for tenant audit entry queries.
/// </summary>
[RestRoute(RestVerb.Get, "{tenantId}/audit", ApiScope = "tenants")]
[RestQueryBinding(RestQueryBindingSource.Route, "tenantId", RestQueryBindingSource.Route, "tenantId")]
[HexalithQuery(
    "Read the audit trail of one tenant, oldest first: each entry gives the event, its category, the acting user, the time and the affected user or setting. Only a global administrator can read it; an unknown tenant returns an empty trail. For TenantId system, the trail also includes the grants and revocations of global administrators. From and To keep entries inside an inclusive time range and Category keeps only Access or Administrative entries; a From later than To is rejected. Results are paged: pageSize takes 1 to 200 (default 100); to read the next page, pass the returned next-page cursor as the cursor argument: it is the result document's cursor, also reported as paging.nextCursor when a paging argument was sent. offset is not supported.",
    AggregateIdProperty = nameof(TenantId),
    Example = """{"TenantId":"acme","From":"2026-01-01T00:00:00Z","To":"2026-12-31T23:59:59Z","Category":"Access"}""")]
public sealed class GetTenantAuditQuery : IQueryContract {
    [HexalithIdentifier]
    [Description("Identifier of the tenant whose audit trail is read. Required; a call without it is rejected.")]
    public string TenantId { get; init; } = string.Empty;

    [Description("Optional inclusive lower bound on the entry time, as an ISO 8601 date and time with an offset.")]
    public DateTimeOffset? From { get; init; }

    [Description("Optional inclusive upper bound on the entry time, as an ISO 8601 date and time with an offset.")]
    public DateTimeOffset? To { get; init; }

    [Description("Optional category filter: Access keeps membership and role changes; Administrative keeps tenant creation, name and description changes, disabling, enabling and configuration changes. Omit it for both.")]
    public AuditEventCategory? Category { get; init; }

    /// <summary>Optional owner selector: tenant or global-administrators (system scope only).</summary>
    [Description("Has no effect for calls made with this tool. Leave unset.")]
    public string? Stream { get; init; }

    [Description("Has no effect: use the pageSize and cursor call arguments instead.")]
    public string? Cursor { get; init; }

    [Description("Has no effect: use the pageSize and cursor call arguments instead.")]
    public int PageSize { get; init; }

    public static string QueryType => "get-tenant-audit";

    public static string Domain => "tenants";

    public static string ProjectionType => "tenants";
}
