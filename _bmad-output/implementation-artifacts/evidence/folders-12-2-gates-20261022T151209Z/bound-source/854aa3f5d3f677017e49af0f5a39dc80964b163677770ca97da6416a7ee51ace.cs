using System.Collections.Immutable;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Immutable complete snapshot of verified named projection routes.</summary>
public sealed class NamedProjectionRouteCatalogSnapshot {
    private readonly ImmutableDictionary<string, NamedProjectionRouteCatalogEntry> _entriesByKey;

    /// <summary>Initializes a complete immutable snapshot.</summary>
    /// <param name="entries">The verified catalog entries.</param>
    public NamedProjectionRouteCatalogSnapshot(IEnumerable<NamedProjectionRouteCatalogEntry> entries) {
        ArgumentNullException.ThrowIfNull(entries);

        ImmutableArray<NamedProjectionRouteCatalogEntry> materialized = [.. entries
            .OrderBy(static entry => entry.AppId, StringComparer.Ordinal)
            .ThenBy(static entry => entry.ServiceVersion, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Domain, StringComparer.Ordinal)];
        var builder = ImmutableDictionary.CreateBuilder<string, NamedProjectionRouteCatalogEntry>(StringComparer.Ordinal);
        foreach (NamedProjectionRouteCatalogEntry entry in materialized) {
            string key = CreateKey(entry.AppId, entry.ServiceVersion, entry.Domain);
            if (!builder.TryAdd(key, entry)) {
                throw new InvalidOperationException(
                    $"Duplicate named projection catalog binding '{entry.AppId}/{entry.ServiceVersion}/{entry.Domain}'.");
            }
        }

        Entries = materialized;
        _entriesByKey = builder.ToImmutable();
    }

    /// <summary>Gets an empty fail-closed snapshot.</summary>
    public static NamedProjectionRouteCatalogSnapshot Empty { get; } = new([]);

    /// <summary>Gets all verified catalog entries.</summary>
    public IReadOnlyList<NamedProjectionRouteCatalogEntry> Entries { get; }

    /// <summary>Looks up one exact app/version/domain binding.</summary>
    /// <param name="appId">The exact DAPR app id.</param>
    /// <param name="serviceVersion">The exact domain-service version.</param>
    /// <param name="domain">The exact canonical domain.</param>
    /// <param name="entry">The verified entry when present.</param>
    /// <returns><c>true</c> only when the exact binding is present.</returns>
    public bool TryGet(
        string appId,
        string serviceVersion,
        string domain,
        out NamedProjectionRouteCatalogEntry? entry)
        => _entriesByKey.TryGetValue(CreateKey(appId, serviceVersion, domain), out entry);

    private static string CreateKey(string appId, string serviceVersion, string domain) {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        return $"{appId}\u001f{serviceVersion}\u001f{domain}";
    }
}
