using System.Collections.Immutable;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Represents one capability-bound domain entry in the named projection route catalog.</summary>
public sealed class NamedProjectionRouteCatalogEntry {
    /// <summary>Initializes a new named route catalog entry.</summary>
    /// <param name="appId">The exact DAPR app id.</param>
    /// <param name="serviceVersion">The exact domain-service version.</param>
    /// <param name="domain">The canonical domain.</param>
    /// <param name="dispatchVersion">The dispatch protocol version.</param>
    /// <param name="dispatchCapability">The dispatch capability marker.</param>
    /// <param name="catalogFingerprint">The verified full-catalog fingerprint.</param>
    /// <param name="projectionTypes">The exact canonical projection types.</param>
    public NamedProjectionRouteCatalogEntry(
        string appId,
        string serviceVersion,
        string domain,
        int dispatchVersion,
        string dispatchCapability,
        string catalogFingerprint,
        IEnumerable<string> projectionTypes) {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentException.ThrowIfNullOrWhiteSpace(dispatchCapability);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogFingerprint);
        ArgumentNullException.ThrowIfNull(projectionTypes);

        AppId = appId;
        ServiceVersion = serviceVersion;
        Domain = domain;
        DispatchVersion = dispatchVersion;
        DispatchCapability = dispatchCapability;
        CatalogFingerprint = catalogFingerprint;
        ProjectionTypes = projectionTypes.ToImmutableArray();
    }

    /// <summary>Gets the exact DAPR app id.</summary>
    public string AppId { get; }

    /// <summary>Gets the exact domain-service version.</summary>
    public string ServiceVersion { get; }

    /// <summary>Gets the canonical domain.</summary>
    public string Domain { get; }

    /// <summary>Gets the dispatch protocol version.</summary>
    public int DispatchVersion { get; }

    /// <summary>Gets the dispatch capability marker.</summary>
    public string DispatchCapability { get; }

    /// <summary>Gets the verified full-catalog fingerprint.</summary>
    public string CatalogFingerprint { get; }

    /// <summary>Gets the exact canonical projection types in ordinal order.</summary>
    public IReadOnlyList<string> ProjectionTypes { get; }
}
