using System.Security.Claims;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Routing;

namespace Hexalith.EventStore.Authorization;

/// <summary>Maps authorized Tenants scopes to historical writable storage after catalog selection.</summary>
public static class LegacyTenantStorageScopeAdapter
{
    /// <summary>Returns whether a query uses the historical Tenants partition after tenant authorization.</summary>
    /// <param name="domain">The selected route domain.</param>
    /// <param name="queryType">The selected query type.</param>
    /// <returns>Whether the query has one explicit public tenant and a historical storage partition.</returns>
    public static bool IsSingleTenantRead(string domain, string queryType)
        => LegacyTenantStorageMap.IsSingleTenantRead(domain, queryType);

    /// <summary>Maps a declared, authorized platform scope to the retained storage partition.</summary>
    /// <param name="declaration">The route selected from the committed catalog.</param>
    /// <param name="scope">The explicit platform scope from the request.</param>
    /// <param name="principal">The authenticated human principal.</param>
    /// <param name="storageTenant">The retained storage partition when authorized.</param>
    /// <returns>Whether the platform scope and exact human permission agree with the selected declaration.</returns>
    public static bool TryMapPlatform(
        RouteContractDeclaration declaration,
        string scope,
        ClaimsPrincipal principal,
        out string storageTenant)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(principal);
        storageTenant = string.Empty;
        PlatformAdmissionDeclaration? admission = declaration.PlatformAdmission;
        if (principal.Identity?.IsAuthenticated != true || !TenantCanonicalizer.IsPlatformScope(scope)
            || admission is null || !string.Equals(scope, admission.Namespace, StringComparison.Ordinal)
            || declaration.Domain is not ("tenants" or "global-administrators")
            || principal.FindAll("eventstore:permission").Count(claim =>
                string.Equals(claim.Value, admission.RequiredHumanPermission, StringComparison.Ordinal)) != 1
            || !TenantCanonicalizer.TryCanonicalizeGrants(
                principal.FindAll("eventstore:tenant").Select(claim => claim.Value), out _))
        {
            return false;
        }

        storageTenant = TenantCanonicalizer.ReservedStorageTenant;
        return true;
    }
}
