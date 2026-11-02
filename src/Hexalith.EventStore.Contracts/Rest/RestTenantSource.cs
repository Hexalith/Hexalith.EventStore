namespace Hexalith.EventStore.Contracts.Rest;

/// <summary>
/// Identifies the source used to resolve the tenant for a generated REST endpoint.
/// </summary>
public enum RestTenantSource
{
    /// <summary>Use the assembly default when applied to a message route.</summary>
    Inherit = -1,
    /// <summary>Resolve the tenant from the authenticated caller's claims.</summary>
    Claims,

    /// <summary>Resolve the tenant from a route parameter.</summary>
    Route,

    /// <summary>Use the fixed "system" tenant (no per-request resolution).</summary>
    System,

    /// <summary>Require an explicit cataloged platform scope in the request header.</summary>
    Platform,
}
