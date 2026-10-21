namespace Hexalith.EventStore.Server.Configuration;

/// <summary>
/// Transport options for projection change notifications.
/// </summary>
public enum ProjectionChangeTransport
{
    /// <summary>
    /// Publish a notification to DAPR pub/sub and let the EventStore subscriber regenerate the ETag.
    /// </summary>
    PubSub,

    /// <summary>
    /// Invoke the ETag actor directly via actor proxy in the local process.
    /// </summary>
    Direct,
}
