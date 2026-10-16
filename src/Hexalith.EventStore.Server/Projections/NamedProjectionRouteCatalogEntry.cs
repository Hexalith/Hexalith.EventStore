using System.Collections.Immutable;

using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Projections;

/// <summary>Represents one capability-bound domain entry in the named projection route catalog.</summary>
public sealed class NamedProjectionRouteCatalogEntry {
    /// <summary>Gets the explicitly enrolled source-guard descriptors from this verified catalog.</summary>
    public IReadOnlyDictionary<string, ProjectionSourceGuardCapability> SourceGuardCapabilities { get; init; }
        = ImmutableDictionary<string, ProjectionSourceGuardCapability>.Empty;

    /// <summary>Gets explicitly verified original-manifest recovery bindings without changing legacy catalog bytes.</summary>
    public IReadOnlyDictionary<string, ProjectionReplayRecoveryBinding> ReplayRecoveryBindings { get; init; }
        = ImmutableDictionary<string, ProjectionReplayRecoveryBinding>.Empty;
    /// <summary>Initializes a new named route catalog entry.</summary>
    /// <param name="appId">The exact DAPR app id.</param>
    /// <param name="serviceVersion">The exact domain-service version.</param>
    /// <param name="domain">The canonical domain.</param>
    /// <param name="dispatchVersion">The dispatch protocol version.</param>
    /// <param name="dispatchCapability">The dispatch capability marker.</param>
    /// <param name="catalogFingerprint">The verified full-catalog fingerprint.</param>
    /// <param name="projectionTypes">The exact canonical projection types.</param>
    /// <param name="fullReplayProjectionTypes">Routes that support one-owner full replay; omitted for legacy catalogs.</param>
    public NamedProjectionRouteCatalogEntry(
        string appId,
        string serviceVersion,
        string domain,
        int dispatchVersion,
        string dispatchCapability,
        string catalogFingerprint,
        IEnumerable<string> projectionTypes,
        IEnumerable<string>? fullReplayProjectionTypes = null,
        IEnumerable<string>? mappedOnlyStreamedProjectionTypes = null,
        IEnumerable<string>? sharedEpochProjectionTypes = null,
        IEnumerable<string>? singleEventProjectionTypes = null,
        IEnumerable<string>? incrementalReplayProjectionTypes = null)
        : this(appId, serviceVersion, domain, dispatchVersion, dispatchCapability, catalogFingerprint,
            projectionTypes, fullReplayProjectionTypes, mappedOnlyStreamedProjectionTypes, sharedEpochProjectionTypes,
            singleEventProjectionTypes, incrementalReplayProjectionTypes, null) { }

    /// <summary>Initializes a catalog with an explicit ordinary-delivery subset and retained rebuild routes.</summary>
    /// <param name="appId">The exact DAPR app id.</param>
    /// <param name="serviceVersion">The domain-service version.</param>
    /// <param name="domain">The canonical domain.</param>
    /// <param name="dispatchVersion">The dispatch protocol version.</param>
    /// <param name="dispatchCapability">The capability marker.</param>
    /// <param name="catalogFingerprint">The complete capability fingerprint.</param>
    /// <param name="projectionTypes">The complete named route set.</param>
    /// <param name="fullReplayProjectionTypes">One-owner full-replay routes.</param>
    /// <param name="mappedOnlyStreamedProjectionTypes">Mapped-only streamed routes.</param>
    /// <param name="sharedEpochProjectionTypes">Fenced shared epoch routes.</param>
    /// <param name="singleEventProjectionTypes">Single-event ordinary routes.</param>
    /// <param name="incrementalReplayProjectionTypes">Incremental replay routes.</param>
    /// <param name="ordinaryProjectionTypes">Enabled ordinary routes; null preserves historical behavior.</param>
    public NamedProjectionRouteCatalogEntry(
        string appId,
        string serviceVersion,
        string domain,
        int dispatchVersion,
        string dispatchCapability,
        string catalogFingerprint,
        IEnumerable<string> projectionTypes,
        IEnumerable<string>? fullReplayProjectionTypes,
        IEnumerable<string>? mappedOnlyStreamedProjectionTypes,
        IEnumerable<string>? sharedEpochProjectionTypes,
        IEnumerable<string>? singleEventProjectionTypes,
        IEnumerable<string>? incrementalReplayProjectionTypes,
        IEnumerable<string>? ordinaryProjectionTypes) {
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
        OrdinaryProjectionTypes = (ordinaryProjectionTypes ?? ProjectionTypes).ToImmutableArray();
        if (OrdinaryProjectionTypes.Distinct(StringComparer.Ordinal).Count() != OrdinaryProjectionTypes.Count
            || OrdinaryProjectionTypes.Any(type => !ProjectionTypes.Contains(type, StringComparer.Ordinal))) {
            throw new ArgumentException("Ordinary delivery must be a unique subset of the named catalog.", nameof(ordinaryProjectionTypes));
        }
        FullReplayProjectionTypes = (fullReplayProjectionTypes ?? ProjectionTypes).ToImmutableArray();
        MappedOnlyStreamedProjectionTypes = (mappedOnlyStreamedProjectionTypes ?? []).ToImmutableArray();
        SharedEpochProjectionTypes = (sharedEpochProjectionTypes ?? []).ToImmutableArray();
        SingleEventProjectionTypes = (singleEventProjectionTypes ?? []).ToImmutableArray();
        IncrementalReplayProjectionTypes = (incrementalReplayProjectionTypes ?? []).ToImmutableArray();
        if (FullReplayProjectionTypes.Any(type => !ProjectionTypes.Contains(type, StringComparer.Ordinal))) {
            throw new ArgumentException("A full-replay route is absent from the named catalog.", nameof(fullReplayProjectionTypes));
        }
        if (MappedOnlyStreamedProjectionTypes.Any(type => !FullReplayProjectionTypes.Contains(type, StringComparer.Ordinal))) {
            throw new ArgumentException("A mapped-only streamed route is absent from full replay.", nameof(mappedOnlyStreamedProjectionTypes));
        }
        if (IncrementalReplayProjectionTypes.Any(type => !MappedOnlyStreamedProjectionTypes.Contains(type,
            StringComparer.Ordinal))) {
            throw new ArgumentException("An incremental replay route is absent from mapped streamed replay.",
                nameof(incrementalReplayProjectionTypes));
        }
        if (SharedEpochProjectionTypes.Any(type => !ProjectionTypes.Contains(type, StringComparer.Ordinal)
            || FullReplayProjectionTypes.Contains(type, StringComparer.Ordinal))) {
            throw new ArgumentException("A shared epoch route is absent from the named catalog or belongs to full replay.", nameof(sharedEpochProjectionTypes));
        }
        if (SingleEventProjectionTypes.Any(type => !ProjectionTypes.Contains(type, StringComparer.Ordinal)
            || FullReplayProjectionTypes.Contains(type, StringComparer.Ordinal))) {
            throw new ArgumentException("A single-event route is absent from the named catalog or belongs to full replay.", nameof(singleEventProjectionTypes));
        }
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

    /// <summary>Gets named routes that accept ordinary delivery in the current host mode.</summary>
    public IReadOnlyList<string> OrdinaryProjectionTypes { get; }

    /// <summary>Gets named routes that support aggregate-owned full replay for one owner.</summary>
    public IReadOnlyList<string> FullReplayProjectionTypes { get; }

    /// <summary>Gets full-replay routes that can bypass legacy actor state and consume bounded source pages.</summary>
    public IReadOnlyList<string> MappedOnlyStreamedProjectionTypes { get; }

    /// <summary>Gets streamed full-replay routes that accept contiguous ordinary suffixes.</summary>
    public IReadOnlyList<string> IncrementalReplayProjectionTypes { get; }

    /// <summary>Gets named routes whose normal delivery is fenced by the shared epoch journal.</summary>
    public IReadOnlyList<string> SharedEpochProjectionTypes { get; }

    /// <summary>Gets named routes whose normal handler safely accepts repeated individual source events.</summary>
    public IReadOnlyList<string> SingleEventProjectionTypes { get; }
}
