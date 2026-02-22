namespace Hexalith.EventStore.Server.Events;

using System.Text.RegularExpressions;

using Hexalith.EventStore.Contracts.Identity;

using Microsoft.Extensions.Logging;

/// <summary>
/// Validates pub/sub topic names per D6 convention ({tenant}.{domain}.events) and ensures
/// compatibility with DAPR-supported backends: Kafka (249 chars), RabbitMQ (255 chars),
/// Azure Service Bus (260 chars). Defense-in-depth -- primary enforcement is via
/// AggregateIdentity input validation (NFR28).
/// </summary>
public class TopicNameValidator(
    ILogger<TopicNameValidator> logger) : ITopicNameValidator {
    /// <summary>
    /// Maximum topic length compatible with the most restrictive DAPR pub/sub backend (Kafka: 249 chars).
    /// </summary>
    internal const int MaxTopicLength = 249;

    /// <summary>
    /// Warning threshold for topic name length. Topics approaching backend limits trigger a warning log.
    /// </summary>
    internal const int WarningLengthThreshold = 200;

    private static readonly Regex _topicPattern = new(
        @"^[a-z0-9]([a-z0-9-]*[a-z0-9])?\.[a-z0-9]([a-z0-9-]*[a-z0-9])?\.events$",
        RegexOptions.Compiled);

    /// <inheritdoc/>
    public bool IsValidTopicName(string topicName) {
        ArgumentNullException.ThrowIfNull(topicName);

        if (string.IsNullOrWhiteSpace(topicName)) {
            return false;
        }

        if (!_topicPattern.IsMatch(topicName)) {
            return false;
        }

        if (topicName.Length > MaxTopicLength) {
            logger.LogWarning(
                "Topic name exceeds maximum backend length: Length={Length}, MaxLength={MaxLength}, Topic={Topic}",
                topicName.Length,
                MaxTopicLength,
                topicName);
            return false;
        }

        if (topicName.Length > WarningLengthThreshold) {
            logger.LogWarning(
                "Topic name approaching backend length limits: Length={Length}, WarningThreshold={WarningThreshold}, Topic={Topic}",
                topicName.Length,
                WarningLengthThreshold,
                topicName);
        }

        return true;
    }

    /// <inheritdoc/>
    public string DeriveTopicName(AggregateIdentity identity) {
        ArgumentNullException.ThrowIfNull(identity);

        string topicName = identity.PubSubTopic;

        if (!IsValidTopicName(topicName)) {
            throw new ArgumentException(
                $"Derived topic name '{topicName}' is invalid. Expected D6 pattern: {{tenant}}.{{domain}}.events with lowercase alphanumeric + hyphens, max {MaxTopicLength} chars.",
                nameof(identity));
        }

        return topicName;
    }
}
