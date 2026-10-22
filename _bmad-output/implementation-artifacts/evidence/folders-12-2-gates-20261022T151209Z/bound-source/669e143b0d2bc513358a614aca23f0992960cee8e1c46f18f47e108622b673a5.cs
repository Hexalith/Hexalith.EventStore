using System.ComponentModel;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Rest;
using Hexalith.McpCli.Abstractions;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Identity;

namespace Hexalith.Tenants.Contracts.Commands;

[RestRoute(RestVerb.Patch, "{tenantId}/users/{userId}/role", ApiScope = "tenants")]
[HexalithCommand(
    "Change the role of an existing member of an active tenant. Allowed for a global administrator or a TenantOwner of the tenant. A user who is not a member is rejected, and setting the role the member already has is accepted and changes nothing. Demoting the last TenantOwner is allowed; afterwards only a global administrator can manage the tenant's members, roles and configuration.",
    Example = """{"TenantId":"acme","UserId":"8d3f2a61-5b7e-4c0a-9e21-3f4b6c7d8e90","NewRole":"TenantOwner"}""")]
public record ChangeUserRole(
    [property: HexalithIdentifier]
    [property: Description("Identifier of the tenant whose member changes role.")]
    string TenantId,
    [property: HexalithIdentifier]
    [property: Description("Identifier of the member whose role changes: the subject (sub) claim the identity provider issues for that user.")]
    string UserId,
    [property: Description("Role that replaces the member's current role. Every role can read the tenant and its members; TenantContributor can also update its name and description; TenantOwner can also manage its members, roles and configuration. Unknown is rejected.")]
    TenantRole NewRole) : ICommandContract {
    public static string Domain => TenantIdentity.Domain;

    public static string CommandType => "change-user-role";

    public string AggregateId => TenantId;
}
