using System.ComponentModel;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Rest;
using Hexalith.McpCli.Abstractions;
using Hexalith.Tenants.Contracts.Identity;

namespace Hexalith.Tenants.Contracts.Commands;

[RestRoute(RestVerb.Post, "{tenantId}/enable", ApiScope = "tenants")]
[HexalithCommand(
    "Enable a disabled tenant so changes to its name, description, members, roles and configuration are accepted again. Only a global administrator can enable a tenant; enabling an unknown or already active tenant is rejected.",
    Example = """{"TenantId":"acme"}""")]
public record EnableTenant(
    [property: HexalithIdentifier]
    [property: Description("Identifier of the tenant to enable.")]
    string TenantId) : ICommandContract {
    public static string Domain => TenantIdentity.Domain;

    public static string CommandType => "enable-tenant";

    public string AggregateId => TenantId;
}
