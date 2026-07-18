namespace Hexalith.EventStore.Server.Queries;

/// <summary>
/// Identifies query routes that have opted into the safe-denial boundary, where a Forbidden
/// result and a genuine not-found result must be externally indistinguishable.
/// </summary>
public interface ISafeDenialQueryRoutePolicy {
    /// <summary>
    /// Determines whether the specified query route has opted into the safe-denial boundary.
    /// </summary>
    /// <param name="domain">The domain targeted by the query.</param>
    /// <param name="queryType">The query type discriminator.</param>
    /// <returns><see langword="true"/> when Forbidden and not-found results must be unified for this route; otherwise, <see langword="false"/>.</returns>
    bool IsOptedIn(string domain, string queryType);
}
