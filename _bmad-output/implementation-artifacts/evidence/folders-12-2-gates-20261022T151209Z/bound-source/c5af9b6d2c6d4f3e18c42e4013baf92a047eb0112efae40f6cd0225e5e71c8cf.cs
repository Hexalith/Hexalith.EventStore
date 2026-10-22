using System.ComponentModel;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Rest;
using Hexalith.McpCli.Abstractions;
using Hexalith.Tenants.Contracts.Identity;

namespace Hexalith.Tenants.Contracts.Commands;

[RestRoute(RestVerb.Post, "{tenantId}", ApiScope = "tenants")]
[HexalithCommand(
    "Create a new tenant with an identifier, a display name and a description (null for none). Only a global administrator can create tenants. The tenant starts active with no members and no configuration; an identifier that already exists is rejected.",
    Example = """{"TenantId":"acme","Name":"Acme Corporation","Description":"Production workspace for Acme"}""")]
public record CreateTenant(
    [property: HexalithIdentifier]
    [property: Description("Identifier of the new tenant: 1 to 256 ASCII letters, digits, '.', '_' or '-', starting and ending with a letter or digit. It is case-sensitive and cannot be changed later.")]
    string TenantId,
    [property: Description("Display name of the tenant.")]
    string Name,
    [property: Description("Free-text description of the tenant. The key is required; send null for no description.")]
    string? Description) : ICommandContract {
    public static string Domain => TenantIdentity.Domain;

    public static string CommandType => "create-tenant";

    public string AggregateId => TenantId;
}
