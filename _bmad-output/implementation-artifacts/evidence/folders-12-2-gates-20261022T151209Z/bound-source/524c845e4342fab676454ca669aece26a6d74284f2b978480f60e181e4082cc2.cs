using System.ComponentModel;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Rest;
using Hexalith.McpCli.Abstractions;
using Hexalith.Tenants.Contracts.Identity;

namespace Hexalith.Tenants.Contracts.Commands;

[RestRoute(RestVerb.Post, "{tenantId}/configuration/{key}/remove", ApiScope = "tenants")]
[HexalithCommand(
    "Delete one configuration setting from an active tenant. Allowed for a global administrator or a TenantOwner of the tenant; removing a key the tenant does not have is rejected.",
    Example = """{"TenantId":"acme","Key":"billing.plan"}""")]
public record RemoveTenantConfiguration(
    [property: HexalithIdentifier]
    [property: Description("Identifier of the tenant whose setting is deleted.")]
    string TenantId,
    [property: Description("Exact, case-sensitive configuration key to delete.")]
    string Key) : ICommandContract {
    public static string Domain => TenantIdentity.Domain;

    public static string CommandType => "remove-tenant-configuration";

    public string AggregateId => TenantId;
}
