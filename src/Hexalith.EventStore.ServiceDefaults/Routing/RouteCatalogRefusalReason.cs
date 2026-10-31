namespace Hexalith.EventStore.ServiceDefaults.Routing;

/// <summary>Closed nonsecret diagnostic categories for catalog refusals.</summary>
public enum RouteCatalogRefusalReason
{
    /// <summary>Request or lifecycle content disagrees with committed authority.</summary>
    Disagreement,
    /// <summary>Required local composition facts are absent.</summary>
    Configuration,
    /// <summary>The deployment owner or proof is not trusted.</summary>
    Trust,
    /// <summary>Loaded component, ACL or host topology disagrees.</summary>
    Topology,
    /// <summary>Installed executable, admission or capability inventory disagrees.</summary>
    Inventory,
    /// <summary>The authoritative state transport or response is unavailable.</summary>
    Backend,
    /// <summary>A prepared transition is fenced while prior durable dispatches drain or receive owner-verified reconciliation.</summary>
    Draining,
}
