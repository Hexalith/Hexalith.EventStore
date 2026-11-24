using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace Hexalith.EventStore.Server.Queries;

/// <summary>
/// Default <see cref="ISafeDenialQueryRoutePolicy"/> backed by an explicit, immutable set of
/// (domain, query type) routes supplied at registration time. Routes not present in the set
/// never opt into the safe-denial boundary. Comparisons are ordinal (case-sensitive), matching
/// how domain and query type values are compared everywhere else in the routing pipeline.
/// </summary>
public sealed class SafeDenialQueryRouteRegistry : ISafeDenialQueryRoutePolicy {
    private readonly HashSet<(string Domain, string QueryType)> _routes;
    private readonly ReadOnlyCollection<(string Domain, string QueryType)> _routesView;
    private readonly ConcurrentDictionary<(string Domain, string QueryType), byte> _reportedCaseMismatches = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SafeDenialQueryRouteRegistry"/> class.
    /// </summary>
    /// <param name="routes">The (domain, query type) routes that opt into the safe-denial boundary.</param>
    public SafeDenialQueryRouteRegistry(IEnumerable<(string Domain, string QueryType)> routes) {
        ArgumentNullException.ThrowIfNull(routes);
        _routes = [.. routes];

        // A genuinely read-only copy, not the live _routes HashSet merely typed as
        // IReadOnlyCollection: the array backing this ReadOnlyCollection is never shared with any
        // other reference, so nothing outside this instance can cast Routes back to a mutable
        // collection type and mutate the registry's authorization-relevant route set.
        _routesView = new ReadOnlyCollection<(string Domain, string QueryType)>([.. _routes]);
    }

    /// <summary>
    /// Gets the immutable snapshot of (domain, query type) routes this instance was constructed
    /// with. Intended for startup diagnostics (see <see cref="SafeDenialQueryRouteStartupLogger"/>)
    /// -- authorization decisions must use <see cref="IsOptedIn"/>, not this enumeration.
    /// </summary>
    public IReadOnlyCollection<(string Domain, string QueryType)> Routes => _routesView;

    /// <inheritdoc/>
    public bool IsOptedIn(string domain, string queryType) {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryType);
        return _routes.Contains((domain, queryType));
    }

    /// <summary>Returns each newly observed registration that differs from the successful route only by case.</summary>
    /// <param name="domain">The successful route's wire domain.</param>
    /// <param name="queryType">The successful route's wire query type.</param>
    /// <returns>The configured routes to report to operators.</returns>
    internal IEnumerable<(string Domain, string QueryType)> RecordCaseMismatches(string domain, string queryType)
    {
        foreach ((string Domain, string QueryType) route in _routes)
        {
            if (route != (domain, queryType)
                && string.Equals(route.Domain, domain, StringComparison.OrdinalIgnoreCase)
                && string.Equals(route.QueryType, queryType, StringComparison.OrdinalIgnoreCase)
                && _reportedCaseMismatches.TryAdd(route, 0))
            {
                yield return route;
            }
        }
    }
}
