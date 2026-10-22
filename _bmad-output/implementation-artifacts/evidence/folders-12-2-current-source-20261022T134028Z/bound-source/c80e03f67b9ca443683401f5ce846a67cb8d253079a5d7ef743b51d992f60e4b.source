using System.ComponentModel;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Rest;
using Hexalith.McpCli.Abstractions;
using Hexalith.Tenants.Contracts.Identity;

namespace Hexalith.Tenants.Contracts.Commands;

[RestRoute(RestVerb.Put, "{tenantId}", ApiScope = "tenants")]
[HexalithCommand(
    "Replace the display name and description of an existing active tenant. Allowed for a global administrator or a tenant member with the TenantOwner or TenantContributor role. Unknown or disabled tenants are rejected.",
    Example = """{"TenantId":"acme","Name":"Acme Corporation EMEA","Description":"European production workspace"}""")]
public record UpdateTenant(
    [property: HexalithIdentifier]
    [property: Description("Identifier of the tenant to update.")]
    string TenantId,
    [property: Description("New display name; replaces the current one.")]
    string Name,
    [property: Description("New description; replaces the current one and null clears it. To keep the current description while renaming, send it again (read it with tenants.get-tenant).")]
    string? Description) : ICommandContract {
    public static string Domain => TenantIdentity.Domain;

    public static string CommandType => "update-tenant";

    public string AggregateId => TenantId;
}
