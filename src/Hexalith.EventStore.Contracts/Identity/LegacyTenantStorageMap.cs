namespace Hexalith.EventStore.Contracts.Identity;

/// <summary>Names the approved Tenants read routes that retain the historical storage partition.</summary>
public static class LegacyTenantStorageMap
{
    /// <summary>Checks a single-tenant read route without changing its public request scope.</summary>
    /// <param name="domain">The declared domain.</param>
    /// <param name="queryType">The declared query type.</param>
    /// <returns>Whether the route reads historical Tenants storage.</returns>
    public static bool IsSingleTenantRead(string domain, string queryType)
        => domain == "tenants" && queryType is "get-tenant" or "get-tenant-users" or "get-tenant-audit";

    /// <summary>Checks the exact bound workload source-read namespace for retained Tenants histories.</summary>
    /// <param name="scope">The explicit request scope.</param>
    /// <param name="domain">The bound source domain.</param>
    /// <returns>Whether the scope maps to that historical source domain.</returns>
    public static bool IsPlatformSourceRead(string scope, string domain)
        => scope == "platform:tenants" && domain == "tenants"
            || scope == "platform:global-administrators" && domain == "global-administrators";
}
