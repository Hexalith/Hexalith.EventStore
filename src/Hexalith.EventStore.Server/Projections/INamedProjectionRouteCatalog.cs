namespace Hexalith.EventStore.Server.Projections;

/// <summary>Publishes the latest complete verified named projection route catalog.</summary>
public interface INamedProjectionRouteCatalog {
    /// <summary>Gets the current immutable catalog snapshot.</summary>
    NamedProjectionRouteCatalogSnapshot Current { get; }

    /// <summary>Atomically replaces the complete catalog snapshot.</summary>
    /// <param name="snapshot">The successfully loaded replacement.</param>
    void Replace(NamedProjectionRouteCatalogSnapshot snapshot);
}
