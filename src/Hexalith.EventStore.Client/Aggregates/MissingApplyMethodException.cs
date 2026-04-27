using System.Globalization;

using Hexalith.EventStore.Contracts.Aggregates;
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>
/// Diagnostic exception thrown by aggregate state rehydration when an event in the
/// persisted stream has no matching <c>public void Apply(TEvent)</c> method on the
/// state type. Distinguishes Apply-lookup misses from JSON shape, payload, or
/// infrastructure failures so operators can alert on this fault class specifically.
/// </summary>
/// <remarks>
/// Derives from <see cref="InvalidOperationException"/> so existing broad handlers
/// continue to catch it without code changes. New handlers can target this type
/// directly to surface event-contract drift, missing tombstoning Apply methods, or
/// other replay-time domain integrity issues.
/// </remarks>
public sealed class MissingApplyMethodException : InvalidOperationException {
    /// <summary>
    /// Initializes a new instance of the <see cref="MissingApplyMethodException"/> class.
    /// </summary>
    /// <param name="stateType">The aggregate state CLR type that lacks an Apply method for the offending event.</param>
    /// <param name="eventTypeName">The event type name as recorded in the persisted stream entry.</param>
    /// <param name="messageId">Optional message identifier of the event envelope, when available.</param>
    /// <param name="aggregateId">Optional aggregate identifier of the event envelope, when available.</param>
    public MissingApplyMethodException(
        Type stateType,
        string eventTypeName,
        string? messageId = null,
        string? aggregateId = null)
        : base(BuildMessage(stateType, eventTypeName, messageId, aggregateId)) {
        StateType = stateType;
        EventTypeName = eventTypeName;
        MessageId = messageId;
        AggregateId = aggregateId;
    }

    /// <summary>Gets the aggregate state CLR type that lacks an Apply method for the offending event.</summary>
    public Type StateType { get; }

    /// <summary>Gets the event type name as recorded in the persisted stream entry.</summary>
    public string EventTypeName { get; }

    /// <summary>Gets the optional message identifier of the event envelope, when the diagnostic context provided one.</summary>
    public string? MessageId { get; }

    /// <summary>Gets the optional aggregate identifier of the event envelope, when the diagnostic context provided one.</summary>
    public string? AggregateId { get; }

    private static string BuildMessage(
        Type stateType,
        string eventTypeName,
        string? messageId,
        string? aggregateId) {
        ArgumentNullException.ThrowIfNull(stateType);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);

        string baseMessage = string.Format(
            CultureInfo.InvariantCulture,
            "Aggregate state '{0}' has no public void Apply({1}) method. The event exists in the persisted stream but cannot be replayed.",
            stateType.FullName ?? stateType.Name,
            eventTypeName);

        string contextSuffix = (messageId, aggregateId) switch {
            (not null, not null) => string.Format(
                CultureInfo.InvariantCulture,
                " AggregateId='{0}', MessageId='{1}'.",
                aggregateId,
                messageId),
            (not null, null) => string.Format(
                CultureInfo.InvariantCulture,
                " MessageId='{0}'.",
                messageId),
            (null, not null) => string.Format(
                CultureInfo.InvariantCulture,
                " AggregateId='{0}'.",
                aggregateId),
            _ => string.Empty,
        };

        bool isTerminatableHint = typeof(ITerminatable).IsAssignableFrom(stateType)
            || string.Equals(eventTypeName, nameof(AggregateTerminated), StringComparison.Ordinal);

        string tombstoneHint = isTerminatableHint
            ? string.Format(
                CultureInfo.InvariantCulture,
                " Hint: states implementing ITerminatable must declare a no-op Apply({0}) method because rejection events are persisted and replayed.",
                nameof(AggregateTerminated))
            : string.Empty;

        return baseMessage + contextSuffix + tombstoneHint;
    }
}
