namespace Hexalith.EventStore.Contracts.Rest;

/// <summary>
/// Identifies the source used to resolve the tenant for a generated REST endpoint.
/// </summary>
public enum RestTenantSource
{
    /// <summary>Use the assembly default when applied to a message route.</summary>
    Inherit = -1,
    /// <summary>Require an explicit tenant header and authorize it with the authenticated caller's grants.</summary>
    Claims,

    /// <summary>Resolve the tenant from a route parameter.</summary>
    Route,

    /// <summary>Legacy fixed-storage metadata; generated public requests using this mode are rejected.</summary>
    System,

    /// <summary>Require an explicit cataloged platform scope in the request header.</summary>
    Platform,
}
