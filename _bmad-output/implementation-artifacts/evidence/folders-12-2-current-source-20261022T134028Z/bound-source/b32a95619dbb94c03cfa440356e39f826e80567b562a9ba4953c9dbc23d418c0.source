using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.Tenants.Contracts.Identity;

public static class TenantIdentity {
    public const string DefaultTenantId = "system";
    public const string Domain = "tenants";

    /// <summary>
    /// Logical domain for the global-administrators aggregate. Distinct from <see cref="Domain"/>
    /// so projection requests for global-administrator events arrive at <c>/project</c> with
    /// <c>ProjectionRequest.Domain == "global-administrators"</c> and route to the dedicated
    /// projection handler instead of being mishandled as tenant-domain events.
    /// </summary>
    public const string GlobalAdministratorsDomain = "global-administrators";
    public const string GlobalAdministratorsAggregateId = "global-administrators";

    public static AggregateIdentity ForTenant(string managedTenantId)
        => new(DefaultTenantId, Domain, managedTenantId);

    public static AggregateIdentity ForGlobalAdministrators()
        => new(DefaultTenantId, GlobalAdministratorsDomain, GlobalAdministratorsAggregateId);
}
