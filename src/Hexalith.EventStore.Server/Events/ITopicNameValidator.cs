namespace Hexalith.EventStore.Server.Events;

using Hexalith.EventStore.Contracts.Identity;

/// <summary>
/// Validates and derives pub/sub topic names per D6 convention ({tenant}.{domain}.events).
/// Defense-in-depth layer -- primary enforcement is via AggregateIdentity input validation.
/// </summary>
public interface ITopicNameValidator {
    /// <summary>
    /// Validates that a topic name conforms to the D6 pattern and is compatible
    /// with DAPR-supported pub/sub backends (Kafka, RabbitMQ, Azure Service Bus).
    /// </summary>
    /// <param name="topicName">The topic name to validate.</param>
    /// <returns>True if the topic name is valid; false otherwise.</returns>
    bool IsValidTopicName(string topicName);

    /// <summary>
    /// Derives the canonical topic name from an aggregate identity with validation.
    /// Delegates to <see cref="AggregateIdentity.PubSubTopic"/> and validates the result.
    /// </summary>
    /// <param name="identity">The aggregate identity providing topic components.</param>
    /// <returns>The validated topic name.</returns>
    /// <exception cref="ArgumentException">Thrown when the derived topic name is invalid.</exception>
    string DeriveTopicName(AggregateIdentity identity);
}
