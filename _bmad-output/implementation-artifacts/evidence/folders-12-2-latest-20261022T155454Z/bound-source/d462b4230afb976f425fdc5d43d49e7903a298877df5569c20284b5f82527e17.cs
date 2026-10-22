
using System.Collections.ObjectModel;

namespace Hexalith.EventStore.Client.Discovery;

/// <summary>
/// Contains the results of an assembly scan for domain types.
/// </summary>
public sealed record DiscoveryResult {
    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoveryResult"/> class.
    /// </summary>
    /// <param name="aggregates">Discovered aggregate types with domain names.</param>
    /// <param name="projections">Discovered projection types with domain names.</param>
    public DiscoveryResult(IEnumerable<DiscoveredDomain> aggregates, IEnumerable<DiscoveredDomain> projections) {
        Aggregates = new ReadOnlyCollection<DiscoveredDomain>(aggregates.ToArray());
        Projections = new ReadOnlyCollection<DiscoveredDomain>(projections.ToArray());
    }

    /// <summary>Gets the discovered aggregate types with domain names.</summary>
    public IReadOnlyList<DiscoveredDomain> Aggregates { get; }

    /// <summary>Gets the discovered projection types with domain names.</summary>
    public IReadOnlyList<DiscoveredDomain> Projections { get; }

    /// <summary>Gets the total number of discovered types.</summary>
    public int TotalCount => Aggregates.Count + Projections.Count;
}
