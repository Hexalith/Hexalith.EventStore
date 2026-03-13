namespace Hexalith.EventStore.Server.Configuration;

/// <summary>
/// Configuration options for projection change notifications.
/// </summary>
public class ProjectionChangeNotifierOptions {
    /// <summary>
    /// Gets or sets the DAPR pub/sub component name used for projection change notifications.
    /// </summary>
    public string PubSubName { get; init; } = "pubsub";

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