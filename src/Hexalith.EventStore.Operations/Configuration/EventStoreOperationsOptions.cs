namespace Hexalith.EventStore.Operations.Configuration;

/// <summary>
/// Configures subscriber dead-letter capture and replay.
/// </summary>
public sealed class EventStoreOperationsOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "EventStoreOperations";

    /// <summary>Gets or sets the Dapr pub/sub component name.</summary>
    public string PubSubName { get; set; } = "pubsub";

    /// <summary>Gets or sets the one dead-letter topic drained by this workload instance.</summary>
    public string TopicName { get; set; } = "deadletter.work.events";

    /// <summary>Gets or sets the HTTP route that receives the raw dead-letter delivery.</summary>
    public string CaptureRoute { get; set; } = "/dead-letters/work/events";

    /// <summary>Gets or sets the Dapr app id that may call operator endpoints.</summary>
    public string AdminCallerAppId { get; set; } = "eventstore-admin";

    /// <summary>Gets or sets the Dapr app id that receives replayed deliveries.</summary>
    public string ReplayAppId { get; set; } = "works";

    /// <summary>Gets or sets the replay target method.</summary>
    public string ReplayMethodName { get; set; } = "work/events";

    /// <summary>Gets or sets the maximum accepted dead-letter body size.</summary>
    public int MaxBodyBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets the maximum number of items accepted in one operator action.</summary>
    public int MaxActionItems { get; set; } = 100;

    /// <summary>Gets or sets the replay recovery reminder interval in seconds.</summary>
    public int ReplayReminderPeriodSeconds { get; set; } = 60;

    /// <summary>Gets or sets the number of delivery attempts before a retried item stops being retried.</summary>
    /// <remarks>
    /// A target that rejects an item permanently would otherwise be re-delivered every reminder period forever.
    /// On exhaustion the item becomes archived with a bounded reason code, so it leaves the backlog and its
    /// telemetry instead of consuming the drain indefinitely. The retained body is untouched and still
    /// inspectable through the operator surface.
    /// </remarks>
    public int MaxReplayAttempts { get; set; } = 10;
}
