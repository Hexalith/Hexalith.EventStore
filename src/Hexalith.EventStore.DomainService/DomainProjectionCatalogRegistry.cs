using System.Collections.Immutable;

using Hexalith.EventStore.Client.Projections;

namespace Hexalith.EventStore.DomainService;

/// <summary>Retains exact route sets for capability-bound fingerprints issued by this domain service.</summary>
public sealed class DomainProjectionCatalogRegistry {
    private readonly object _sync = new();
    private ImmutableDictionary<string, ImmutableHashSet<ProjectionDispatchRoute>> _routesByFingerprint =
        ImmutableDictionary<string, ImmutableHashSet<ProjectionDispatchRoute>>.Empty.WithComparers(StringComparer.Ordinal);

    /// <summary>Registers one issued fingerprint and its exact canonical route set.</summary>
    /// <param name="fingerprint">The issued fingerprint.</param>
    /// <param name="routes">The exact routes represented by the fingerprint.</param>
    public void Register(string fingerprint, IEnumerable<ProjectionDispatchRoute> routes) {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ArgumentNullException.ThrowIfNull(routes);
        ImmutableHashSet<ProjectionDispatchRoute> materialized = routes.ToImmutableHashSet();
        if (materialized.Count == 0) {
            throw new ArgumentException("A projection route catalog cannot be empty.", nameof(routes));
        }

        lock (_sync) {
            if (_routesByFingerprint.TryGetValue(fingerprint, out ImmutableHashSet<ProjectionDispatchRoute>? existing)
                && !existing.SetEquals(materialized)) {
                throw new InvalidOperationException("One projection catalog fingerprint cannot identify different route sets.");
            }

            _routesByFingerprint = _routesByFingerprint.SetItem(fingerprint, materialized);
        }
    }

    /// <summary>Checks that a fingerprint issued by this service authorizes every requested exact route.</summary>
    /// <param name="fingerprint">The supplied catalog fingerprint.</param>
    /// <param name="domain">The exact request domain.</param>
    /// <param name="projectionTypes">The explicit requested projection types.</param>
    /// <returns><c>true</c> only when every exact route is present in the issued catalog.</returns>
    public bool Authorizes(string fingerprint, string domain, IEnumerable<string> projectionTypes) {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(projectionTypes);

        ImmutableDictionary<string, ImmutableHashSet<ProjectionDispatchRoute>> snapshot = _routesByFingerprint;
        return snapshot.TryGetValue(fingerprint, out ImmutableHashSet<ProjectionDispatchRoute>? routes)
            && projectionTypes.All(projectionType => routes.Contains(new ProjectionDispatchRoute(domain, projectionType)));
    }

    /// <summary>Checks whether this service issued the supplied fingerprint.</summary>
    /// <param name="fingerprint">The supplied catalog fingerprint.</param>
    /// <returns><c>true</c> when the fingerprint is currently registered.</returns>
    public bool Contains(string fingerprint) {
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        return _routesByFingerprint.ContainsKey(fingerprint);
    }
}
