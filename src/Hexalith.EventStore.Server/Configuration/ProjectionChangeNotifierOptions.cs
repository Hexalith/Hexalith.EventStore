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
    /// Gets or sets the DAPR pub/sub component name used for projection change notifications.
    /// </summary>
    public string PubSubName { get; init; } = DefaultPubSubName;

    /// <summary>
    /// Gets or sets the transport used to notify EventStore about projection changes.
    /// Defaults to pub/sub for cross-process compatibility.
    /// </summary>
    public ProjectionChangeTransport Transport { get; init; } = ProjectionChangeTransport.PubSub;
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
public sealed class ValidateProjectionChangeNotifierOptions : IValidateOptions<ProjectionChangeNotifierOptions> {
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, ProjectionChangeNotifierOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.PubSubName)) {
            return ValidateOptionsResult.Fail("Projection change pub/sub component name must not be empty.");
        }

        if (options.Transport == ProjectionChangeTransport.PubSub
            && !string.Equals(options.PubSubName, ProjectionChangeNotifierOptions.DefaultPubSubName, StringComparison.Ordinal)) {
            return ValidateOptionsResult.Fail(
                $"Projection change pub/sub transport currently requires PubSubName='{ProjectionChangeNotifierOptions.DefaultPubSubName}' to match the DAPR subscription route.");
        }

        return ValidateOptionsResult.Success;
    }
}