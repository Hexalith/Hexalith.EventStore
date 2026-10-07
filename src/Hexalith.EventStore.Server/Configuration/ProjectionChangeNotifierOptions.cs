using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.Extensions.Options;

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
    public const int DefaultMaxDetailMetadataEntries = 16;

    /// <summary>
    /// The default maximum total UTF-8 byte size of detail notification metadata.
    /// </summary>
    public const int DefaultMaxDetailMetadataBytes = 2048;

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
    /// Gets or sets the maximum number of metadata entries carried by a detail notification.
    /// </summary>
    public int MaxDetailMetadataEntries { get; init; } = DefaultMaxDetailMetadataEntries;

    /// <summary>
    /// Gets or sets the maximum total UTF-8 byte size carried by detail notification metadata.
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

/// <summary>
/// Transport options for projection change notifications.
/// </summary>
public enum ProjectionChangeTransport {
    /// <summary>
    /// Publish a notification to DAPR pub/sub and let the EventStore subscriber regenerate the ETag.
    /// </summary>
    PubSub,

    /// <summary>
    /// Invoke the ETag actor directly via actor proxy in the local process.
    /// </summary>
    Direct,
}

/// <summary>
/// Validates projection change notifier configuration.
/// </summary>
/// <param name="provenanceIssuer">
/// The issuer of notification provenance. The pub/sub transport is valid only when it can bind provenance to the
/// notification's tenant, projection type, and topic.
/// </param>
public sealed class ValidateProjectionChangeNotifierOptions(IWorkloadAssertionIssuer? provenanceIssuer = null)
    : IValidateOptions<ProjectionChangeNotifierOptions> {
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, ProjectionChangeNotifierOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.PubSubName)) {
            return ValidateOptionsResult.Fail("Projection change pub/sub component name must not be empty.");
        }

        if (!Enum.IsDefined(typeof(ProjectionChangeTransport), options.Transport)) {
            return ValidateOptionsResult.Fail("Projection change transport must be a defined value.");
        }

        if (options.Transport == ProjectionChangeTransport.PubSub
            && !string.Equals(options.PubSubName, ProjectionChangeNotifierOptions.DefaultPubSubName, StringComparison.Ordinal)) {
            return ValidateOptionsResult.Fail(
                $"Projection change pub/sub transport currently requires PubSubName='{ProjectionChangeNotifierOptions.DefaultPubSubName}' to match the DAPR subscription route.");
        }

        // Story 5.5: the receiver denies unbound provenance, so pub/sub without a binding issuer would silently drop
        // every notification. Refuse it before the host starts.
        if (options.Transport == ProjectionChangeTransport.PubSub && provenanceIssuer?.CanBindResources != true) {
            return ValidateOptionsResult.Fail(
                "Projection change pub/sub transport requires a provenance issuer that binds each notification to its tenant, projection type, and topic. An OIDC authority cannot bind them; use Transport=Direct.");
        }

        if (options.MaxDetailMetadataEntries <= 0) {
            return ValidateOptionsResult.Fail("Projection change MaxDetailMetadataEntries must be greater than zero.");
        }

        if (options.MaxDetailMetadataBytes <= 0) {
            return ValidateOptionsResult.Fail("Projection change MaxDetailMetadataBytes must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.ProvenanceAudience)) {
            return ValidateOptionsResult.Fail("Projection change ProvenanceAudience must not be empty.");
        }

        return ValidateOptionsResult.Success;
    }
}
