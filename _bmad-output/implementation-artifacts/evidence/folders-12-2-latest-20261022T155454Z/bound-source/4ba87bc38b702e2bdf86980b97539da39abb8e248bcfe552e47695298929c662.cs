using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Rest;
using Hexalith.Tenants.Contracts.Identity;

namespace Hexalith.Tenants.Contracts.Commands;

[RestRoute(RestVerb.Post, "~/api/global-administrators/{userId}/set", ApiScope = "tenants")]
public record SetGlobalAdministrator(string UserId) : ICommandContract {
    public static string Domain => TenantIdentity.GlobalAdministratorsDomain;

    public static string CommandType => "set-global-administrator";

    public string AggregateId => TenantIdentity.GlobalAdministratorsAggregateId;
}
