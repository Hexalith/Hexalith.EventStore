using System.ComponentModel;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Rest;
using Hexalith.McpCli.Abstractions;
using Hexalith.Tenants.Contracts.Identity;

namespace Hexalith.Tenants.Contracts.Commands;

[RestRoute(RestVerb.Post, "{tenantId}/disable", ApiScope = "tenants")]
[HexalithCommand(
    "Disable an active tenant. Its data stays readable, but changes to its name, description, members, roles and configuration are rejected until it is enabled again. Only a global administrator can disable a tenant; disabling an unknown or already disabled tenant is rejected.",
    Example = """{"TenantId":"acme"}""")]
public record DisableTenant(
    [property: HexalithIdentifier]
    [property: Description("Identifier of the tenant to disable.")]
    string TenantId) : ICommandContract {
    public static string Domain => TenantIdentity.Domain;

    public static string CommandType => "disable-tenant";

    public string AggregateId => TenantId;
}
