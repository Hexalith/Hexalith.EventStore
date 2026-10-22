using System.ComponentModel;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Rest;
using Hexalith.McpCli.Abstractions;
using Hexalith.Tenants.Contracts.Identity;

namespace Hexalith.Tenants.Contracts.Commands;

[RestRoute(RestVerb.Post, "{tenantId}/users/{userId}/remove", ApiScope = "tenants")]
[HexalithCommand(
    "Remove a user's membership from an active tenant, revoking the user's role in that tenant. Allowed for a global administrator or a TenantOwner of the tenant; removing a user who is not a member is rejected. Removing the last TenantOwner is allowed; afterwards only a global administrator can manage the tenant's members, roles and configuration.",
    Example = """{"TenantId":"acme","UserId":"8d3f2a61-5b7e-4c0a-9e21-3f4b6c7d8e90"}""")]
public record RemoveUserFromTenant(
    [property: HexalithIdentifier]
    [property: Description("Identifier of the tenant to remove the user from.")]
    string TenantId,
    [property: HexalithIdentifier]
    [property: Description("Identifier of the member to remove: the subject (sub) claim the identity provider issues for that user.")]
    string UserId) : ICommandContract {
    public static string Domain => TenantIdentity.Domain;

    public static string CommandType => "remove-user-from-tenant";

    public string AggregateId => TenantId;
}
