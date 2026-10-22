using System.ComponentModel;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Rest;
using Hexalith.McpCli.Abstractions;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Identity;

namespace Hexalith.Tenants.Contracts.Commands;

[RestRoute(RestVerb.Post, "{tenantId}/users/{userId}/add", ApiScope = "tenants")]
[HexalithCommand(
    "Add a user to an active tenant with a role. Allowed for a global administrator or a TenantOwner of the tenant; if the tenant has never had a member, any caller may add its first member with any role. A user who is already a member is rejected; use tenants.change-user-role to change an existing member's role.",
    Example = """{"TenantId":"acme","UserId":"8d3f2a61-5b7e-4c0a-9e21-3f4b6c7d8e90","Role":"TenantContributor"}""")]
public record AddUserToTenant(
    [property: HexalithIdentifier]
    [property: Description("Identifier of the tenant to add the user to.")]
    string TenantId,
    [property: HexalithIdentifier]
    [property: Description("Identifier of the user to add: the subject (sub) claim the identity provider issues for that user. It is not checked against the identity provider.")]
    string UserId,
    [property: Description("Role to grant. Every role can read the tenant and its members; TenantContributor can also update its name and description; TenantOwner can also manage its members, roles and configuration. Unknown is rejected.")]
    TenantRole Role) : ICommandContract {
    public static string Domain => TenantIdentity.Domain;

    public static string CommandType => "add-user-to-tenant";

    public string AggregateId => TenantId;
}
