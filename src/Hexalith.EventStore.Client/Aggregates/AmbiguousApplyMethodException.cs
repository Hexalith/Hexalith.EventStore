using System.Globalization;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>
/// Diagnostic exception thrown when a persisted event type name matches more than one
/// <c>public void Apply(TEvent)</c> candidate on the state or read-model type. Replay must never
/// pick one candidate silently: which one wins would depend on reflection/dictionary enumeration
/// order and could differ between processes, binding the wrong event with no diagnostic.
/// </summary>
/// <remarks>
/// Derives from <see cref="InvalidOperationException"/> so existing broad handlers keep catching it
/// without code changes, mirroring <see cref="MissingApplyMethodException"/>. Addressing the event by
/// its full CLR name resolves the ambiguity, because full names are matched exactly before any
/// short-name or suffix candidate is considered.
/// </remarks>
public sealed class AmbiguousApplyMethodException : InvalidOperationException {
    /// <summary>
    /// Initializes a new instance of the <see cref="AmbiguousApplyMethodException"/> class.
    /// </summary>
    /// <param name="stateType">The aggregate state or read-model CLR type owning the colliding Apply methods.</param>
    /// <param name="eventTypeName">The event type name as recorded in the persisted stream entry.</param>
    /// <param name="candidateEventTypeNames">The full CLR names of every event type that matched.</param>
    /// <param name="candidateCount">The number of matching Apply methods. Never lower than the distinct candidate name count.</param>
    /// <param name="messageId">Optional message identifier of the event envelope, when available.</param>
    /// <param name="aggregateId">Optional aggregate identifier of the event envelope, when available.</param>
    public AmbiguousApplyMethodException(
        Type stateType,
        string eventTypeName,
        IEnumerable<string> candidateEventTypeNames,
        int candidateCount,
        string? messageId = null,
        string? aggregateId = null)
        : this(
            stateType,
            eventTypeName,
            NormalizeCandidates(candidateEventTypeNames),
            candidateCount,
            messageId,
            aggregateId) {
    }

    private AmbiguousApplyMethodException(
        Type stateType,
        string eventTypeName,
        IReadOnlyList<string> candidateEventTypeNames,
        int candidateCount,
        string? messageId,
        string? aggregateId)
        : base(BuildMessage(stateType, eventTypeName, candidateEventTypeNames, candidateCount, messageId, aggregateId)) {
        StateType = stateType;
        EventTypeName = eventTypeName;
        CandidateEventTypeNames = candidateEventTypeNames;
        CandidateCount = candidateCount;
        MessageId = messageId;
        AggregateId = aggregateId;
    }

    /// <summary>Gets the aggregate state or read-model CLR type owning the colliding Apply methods.</summary>
    public Type StateType { get; }

    /// <summary>Gets the event type name as recorded in the persisted stream entry.</summary>
    public string EventTypeName { get; }

    /// <summary>Gets the distinct, ordinally sorted full CLR names of every event type that matched.</summary>
    public IReadOnlyList<string> CandidateEventTypeNames { get; }

    /// <summary>Gets the number of matching Apply methods, which can exceed the distinct candidate name count when two candidate types share a full name.</summary>
    public int CandidateCount { get; }

    /// <summary>Gets the optional message identifier of the event envelope, when the diagnostic context provided one.</summary>
    public string? MessageId { get; }

    /// <summary>Gets the optional aggregate identifier of the event envelope, when the diagnostic context provided one.</summary>
    public string? AggregateId { get; }

    /// <summary>
    /// De-duplicates and ordinally sorts the candidate names so the exception message is byte-stable
    /// across processes regardless of reflection enumeration order.
    /// </summary>
    private static IReadOnlyList<string> NormalizeCandidates(IEnumerable<string> candidateEventTypeNames) {
        ArgumentNullException.ThrowIfNull(candidateEventTypeNames);
        return [.. candidateEventTypeNames
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)];
    }

    private static string BuildMessage(
        Type stateType,
        string eventTypeName,
        IReadOnlyList<string> candidateEventTypeNames,
        int candidateCount,
        string? messageId,
        string? aggregateId) {
        ArgumentNullException.ThrowIfNull(stateType);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventTypeName);

        // Ambiguity means two or more declarations by definition, and de-duplicating names can only ever
        // shrink the list below the declaration count. Both are construction-site invariants: violating
        // either would render a message that understates or misreports the collision.
        ArgumentOutOfRangeException.ThrowIfLessThan(candidateCount, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(candidateCount, candidateEventTypeNames.Count);

        string baseMessage = string.Format(
            CultureInfo.InvariantCulture,
            "Aggregate state '{0}' has {1} Apply methods matching persisted event type '{2}'. "
            + "Replay refuses to guess which one applies. Candidates: {3}. "
            + "Record the event under its full CLR type name, or remove the colliding Apply overload.",
            stateType.FullName ?? stateType.Name,
            candidateCount,
            eventTypeName,
            candidateEventTypeNames.Count > 0
                ? string.Join(", ", candidateEventTypeNames)
                : "(no candidate type name was reported)");

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

        return baseMessage + contextSuffix;
    }
}
