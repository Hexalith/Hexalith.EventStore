using Hexalith.EventStore.Contracts.Projections;

namespace Hexalith.EventStore.Server.Configuration;

/// <summary>
/// Configuration options for projection change notifications.
/// </summary>
public class ProjectionChangeNotifierOptions {
    /// <summary>
    /// The supported DAPR pub/sub component name for projection change notifications.
    /// </summary>
    public const string DefaultPubSubName = "pubsub";

    /// <summary>
    /// The default maximum number of metadata entries in a detail notification.
    /// </summary>
    public const int DefaultMaxDetailMetadataEntries = ProjectionChangedMetadataLimits.MaximumEntries;

    /// <summary>
    /// The default maximum serialized UTF-8 byte size of detail notification metadata.
    /// </summary>
    public const int DefaultMaxDetailMetadataBytes = ProjectionChangedMetadataLimits.MaximumSerializedUtf8Bytes;

    /// <summary>
    /// Gets or sets the DAPR pub/sub component name used for projection change notifications.
    /// </summary>
    public string PubSubName { get; init; } = DefaultPubSubName;

    /// <summary>
    /// Gets or sets the transport used to notify EventStore about projection changes. Defaults to
    /// <see cref="ProjectionChangeTransport.Direct"/>. <see cref="ProjectionChangeTransport.PubSub"/> requires a
    /// provenance issuer that binds each notification to its tenant, projection type, and topic (Story 5.5); an OIDC
    /// authority cannot, so the host refuses pub/sub at startup in that mode.
    /// </summary>
    public ProjectionChangeTransport Transport { get; init; } = ProjectionChangeTransport.Direct;

    /// <summary>
    /// Gets or sets the maximum number of metadata entries carried by a detail notification, at most 16.
    /// </summary>
    public int MaxDetailMetadataEntries { get; init; } = DefaultMaxDetailMetadataEntries;

    /// <summary>
    /// Gets or sets the maximum compact JSON UTF-8 size of detail metadata, including punctuation
    /// and escaping. The supported range is 2 through 2,048 bytes; options may only lower the ceiling.
    /// </summary>
    public int MaxDetailMetadataBytes { get; init; } = DefaultMaxDetailMetadataBytes;

    /// <summary>
    /// Gets or sets the workload audience a pub/sub notification's signed provenance is issued for. It must equal
    /// the receiving EventStore's <c>Authentication:DaprInternal:Audience</c>.
    /// </summary>
    public string ProvenanceAudience { get; init; } = DefaultProvenanceAudience;

    /// <summary>
    /// Gets or sets the workloads allowed to publish projection-changed notifications. Empty means only
    /// <see cref="DefaultProvenanceAudience"/> (EventStore itself).
    /// </summary>
    public IList<string> AllowedPublishers { get; init; } = [];

    /// <summary>
    /// Gets the default provenance audience and publisher: EventStore's own workload identity.
    /// </summary>
    public const string DefaultProvenanceAudience = "eventstore";

    /// <summary>
    /// Gets the effective publisher allow-list.
    /// </summary>
    /// <returns>The configured publishers, or EventStore itself when none is configured.</returns>
    public IReadOnlyCollection<string> GetEffectiveAllowedPublishers()
        => AllowedPublishers is { Count: > 0 }
            ? [.. AllowedPublishers.Where(static publisher => !string.IsNullOrWhiteSpace(publisher)).Select(static publisher => publisher.Trim())]
            : [DefaultProvenanceAudience];
}
