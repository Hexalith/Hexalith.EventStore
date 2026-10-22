using Hexalith.EventStore.Contracts.Commands;
using Hexalith.Tenants.Contracts.Identity;

namespace Hexalith.Tenants.Contracts.Commands;

public record BootstrapGlobalAdmin(string UserId) : ICommandContract {
    public static string Domain => TenantIdentity.GlobalAdministratorsDomain;

    public static string CommandType => "bootstrap-global-admin";

    public string AggregateId => TenantIdentity.GlobalAdministratorsAggregateId;
}
