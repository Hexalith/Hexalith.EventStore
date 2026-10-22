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

    /// <inheritdoc/>
    public void Upsert(IEnumerable<NamedProjectionRouteCatalogEntry> entries) {
        ArgumentNullException.ThrowIfNull(entries);
        NamedProjectionRouteCatalogEntry[] replacements = [.. entries];
        if (replacements.Length == 0) {
            return;
        }

        while (true) {
            NamedProjectionRouteCatalogSnapshot current = Current;
            var replacementKeys = new HashSet<string>(replacements.Select(CreateKey), StringComparer.Ordinal);
            var next = new NamedProjectionRouteCatalogSnapshot(
                current.Entries.Where(entry => !replacementKeys.Contains(CreateKey(entry))).Concat(replacements));
            if (ReferenceEquals(Interlocked.CompareExchange(ref _current, next, current), current)) {
                return;
            }
        }
    }

    /// <inheritdoc/>
    public void Remove(string appId, string serviceVersion, string domain) {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        string removalKey = $"{appId}\u001f{serviceVersion}\u001f{domain}";
        while (true) {
            NamedProjectionRouteCatalogSnapshot current = Current;
            var next = new NamedProjectionRouteCatalogSnapshot(
                current.Entries.Where(entry => !string.Equals(CreateKey(entry), removalKey, StringComparison.Ordinal)));
            if (ReferenceEquals(Interlocked.CompareExchange(ref _current, next, current), current)) {
                return;
            }
        }
    }

    private static string CreateKey(NamedProjectionRouteCatalogEntry entry)
        => $"{entry.AppId}\u001f{entry.ServiceVersion}\u001f{entry.Domain}";
}
