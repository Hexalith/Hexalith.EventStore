namespace Hexalith.EventStore.Server.Projections;

/// <summary>Thread-safe atomic named projection route catalog.</summary>
public sealed class NamedProjectionRouteCatalog : INamedProjectionRouteCatalog {
    private NamedProjectionRouteCatalogSnapshot _current = NamedProjectionRouteCatalogSnapshot.Empty;

    /// <inheritdoc/>
    public NamedProjectionRouteCatalogSnapshot Current => Volatile.Read(ref _current);

    /// <inheritdoc/>
    public void Replace(NamedProjectionRouteCatalogSnapshot snapshot) {
        ArgumentNullException.ThrowIfNull(snapshot);
        Volatile.Write(ref _current, snapshot);
    }
}
