namespace Hexalith.EventStore.Server.Projections;

/// <summary>Publishes the latest complete verified named projection route catalog.</summary>
public interface INamedProjectionRouteCatalog {
    /// <summary>Gets the current immutable catalog snapshot.</summary>
    NamedProjectionRouteCatalogSnapshot Current { get; }

    /// <summary>Atomically replaces the complete catalog snapshot.</summary>
    /// <param name="snapshot">The successfully loaded replacement.</param>
    void Replace(NamedProjectionRouteCatalogSnapshot snapshot);

    /// <summary>Atomically replaces only the supplied exact app/version/domain bindings.</summary>
    /// <param name="entries">The complete verified replacement entries.</param>
    void Upsert(IEnumerable<NamedProjectionRouteCatalogEntry> entries);

    /// <summary>Atomically removes one exact binding after a successful non-v2 refresh.</summary>
    /// <param name="appId">The exact DAPR app id.</param>
    /// <param name="serviceVersion">The exact service version.</param>
    /// <param name="domain">The exact domain.</param>
    void Remove(string appId, string serviceVersion, string domain);
}
