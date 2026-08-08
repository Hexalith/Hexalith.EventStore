using System.Globalization;
using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Replay;
using Hexalith.EventStore.Contracts.Serialization;

namespace Hexalith.EventStore.Client.Aggregates;

/// <summary>
/// Side-effect-free replay engine that drives a domain state type's runtime Apply convention
/// against an event list. Used by <see cref="EventStoreAggregate{TState}"/> to satisfy the
/// canonical <c>/replay-state</c> contract; the same Apply discovery is shared with
/// <see cref="DomainProcessorStateRehydrator"/> so command-time and replay-time semantics stay aligned.
/// </summary>
public static class AggregateReplayer {
    /// <summary>
    /// Replays <paramref name="request"/> events through the supplied state type's Apply methods
    /// and returns a <see cref="AggregateReconstructionResult"/> categorizing the outcome.
    /// </summary>
    /// <typeparam name="TState">Aggregate state type owning the Apply convention.</typeparam>
    /// <param name="request">The reconstruction request.</param>
    /// <returns>The reconstruction result.</returns>
    public static AggregateReconstructionResult Replay<TState>(AggregateReconstructionRequest request)
        where TState : class, new() {
        ArgumentNullException.ThrowIfNull(request);

        ApplyMethodTable applyMethods = DomainProcessorStateRehydrator.DiscoverApplyMethods(typeof(TState));
        bool includeTimeline = request.IncludeTimeline;
        List<AggregateReconstructionTimelineEntry>? timeline = includeTimeline
            ? new List<AggregateReconstructionTimelineEntry>()
            : null;

        // Sort by stream sequence/version order only (story Replay Semantics).
        // Filter to the inclusive UpToSequence target before sorting so duplicate detection
        // does not flag events outside the target window.
        ReplayEventEnvelope[] eligible = request.Events
            .Where(e => e.SequenceNumber <= request.UpToSequence)
            .OrderBy(e => e.SequenceNumber)
            .ToArray();

        if (eligible.Length > 0 && eligible[0].SequenceNumber != 1) {
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.Unexpected,
                "Missing stream sequence 1 detected during replay; reconstruction cannot skip events.",
                failedSequenceNumber: 1,
                failedEventType: string.Empty);
        }

        // Duplicate / conflicting sequence guard: any two events sharing the same sequence
        // number cannot be unambiguously ordered, so reconstruction must fail explicitly
        // rather than pick one arbitrarily.
        for (int i = 1; i < eligible.Length; i++) {
            if (eligible[i].SequenceNumber == eligible[i - 1].SequenceNumber) {
                return AggregateReconstructionResult.Failed(
                    AggregateReconstructionErrorCategory.Unexpected,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Duplicate stream sequence {0} detected during replay; reconstruction cannot disambiguate ordering.",
                        eligible[i].SequenceNumber),
                    failedSequenceNumber: eligible[i].SequenceNumber,
                    failedEventType: eligible[i].EventTypeName);
            }

            long expectedSequence = eligible[i - 1].SequenceNumber + 1;
            if (eligible[i].SequenceNumber != expectedSequence) {
                return AggregateReconstructionResult.Failed(
                    AggregateReconstructionErrorCategory.Unexpected,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Missing stream sequence {0} detected during replay; reconstruction cannot skip events.",
                        expectedSequence),
                    failedSequenceNumber: expectedSequence,
                    failedEventType: string.Empty);
            }
        }

        var state = new TState();
        long lastApplied = 0;

        foreach (ReplayEventEnvelope evt in eligible) {
            if (evt.MetadataVersion < 1) {
                return AggregateReconstructionResult.Failed(
                    AggregateReconstructionErrorCategory.UnsupportedVersion,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Event '{0}' at sequence {1} has unsupported metadata version {2}.",
                        evt.EventTypeName,
                        evt.SequenceNumber,
                        evt.MetadataVersion),
                    failedSequenceNumber: evt.SequenceNumber,
                    failedEventType: evt.EventTypeName,
                    lastAppliedSequenceNumber: lastApplied);
            }

            if (string.IsNullOrWhiteSpace(evt.EventTypeName)) {
                return AggregateReconstructionResult.Failed(
                    AggregateReconstructionErrorCategory.UnknownEventType,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Event at sequence {0} is missing the required EventTypeName metadata.",
                        evt.SequenceNumber),
                    failedSequenceNumber: evt.SequenceNumber,
                    failedEventType: evt.EventTypeName,
                    lastAppliedSequenceNumber: lastApplied);
            }

            if (!string.Equals(evt.SerializationFormat, "json", StringComparison.OrdinalIgnoreCase)) {
                return AggregateReconstructionResult.Failed(
                    AggregateReconstructionErrorCategory.UnsupportedVersion,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Event '{0}' at sequence {1} uses unsupported serialization format '{2}'. Replay supports 'json' only.",
                        evt.EventTypeName,
                        evt.SequenceNumber,
                        evt.SerializationFormat),
                    failedSequenceNumber: evt.SequenceNumber,
                    failedEventType: evt.EventTypeName,
                    lastAppliedSequenceNumber: lastApplied);
            }

            MethodInfo? applyMethod;
            try {
                applyMethod = ApplyMethodResolver.TryResolve(
                    applyMethods,
                    evt.EventTypeName,
                    evt.MessageId,
                    request.AggregateId);
            }
            catch (AmbiguousApplyMethodException ex) {
                // Replay returns a categorized result by contract. Letting the ambiguity escape would turn a
                // diagnosable resolution failure into an unhandled 500 on the replay endpoint.
                //
                // The wire message is rebuilt rather than forwarding ex.Message verbatim: the exception text
                // carries the state type's full CLR name plus remediation prose aimed at a developer, while
                // every other Failed(...) here names the state type by its short name. The candidate names
                // are kept because they are the whole diagnostic — colliding candidates share a short name
                // by definition, so short-name candidates would say nothing.
                return AggregateReconstructionResult.Failed(
                    AggregateReconstructionErrorCategory.UnknownEventType,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Event type '{0}' at sequence {1} matches {2} Apply methods on aggregate state '{3}': {4}. Reconstruction cannot disambiguate them.",
                        evt.EventTypeName,
                        evt.SequenceNumber,
                        ex.CandidateCount,
                        typeof(TState).Name,
                        string.Join(", ", ex.CandidateEventTypeNames)),
                    failedSequenceNumber: evt.SequenceNumber,
                    failedEventType: evt.EventTypeName,
                    lastAppliedSequenceNumber: lastApplied);
            }

            if (applyMethod is null) {
                return AggregateReconstructionResult.Failed(
                    applyMethods.Count == 0
                        ? AggregateReconstructionErrorCategory.ApplyHandlerMissing
                        : AggregateReconstructionErrorCategory.UnknownEventType,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        applyMethods.Count == 0
                            ? "Aggregate state '{0}' has no public Apply methods for event '{1}' at sequence {2}."
                            : "Event type '{1}' at sequence {2} is not recognized by aggregate state '{0}'.",
                        typeof(TState).Name,
                        evt.EventTypeName,
                        evt.SequenceNumber),
                    failedSequenceNumber: evt.SequenceNumber,
                    failedEventType: evt.EventTypeName,
                    lastAppliedSequenceNumber: lastApplied);
            }

            Type eventClrType = applyMethod.GetParameters()[0].ParameterType;
            object? deserialized;
            try {
                using JsonDocument doc = evt.Payload is { Length: > 0 }
                    ? JsonDocument.Parse(evt.Payload)
                    : JsonDocument.Parse("{}");
                deserialized = JsonSerializer.Deserialize(doc.RootElement, eventClrType, EventStorePayloadSerialization.Options);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException) {
                return AggregateReconstructionResult.Failed(
                    AggregateReconstructionErrorCategory.DeserializationFailed,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Payload for event '{0}' at sequence {1} could not be deserialized to '{2}'.",
                        evt.EventTypeName,
                        evt.SequenceNumber,
                        eventClrType.Name),
                    failedSequenceNumber: evt.SequenceNumber,
                    failedEventType: evt.EventTypeName,
                    lastAppliedSequenceNumber: lastApplied);
            }

            if (deserialized is null) {
                return AggregateReconstructionResult.Failed(
                    AggregateReconstructionErrorCategory.DeserializationFailed,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Payload for event '{0}' at sequence {1} deserialized to null for '{2}'.",
                        evt.EventTypeName,
                        evt.SequenceNumber,
                        eventClrType.Name),
                    failedSequenceNumber: evt.SequenceNumber,
                    failedEventType: evt.EventTypeName,
                    lastAppliedSequenceNumber: lastApplied);
            }

            try {
                _ = applyMethod.Invoke(state, [deserialized]);
            }
            catch (TargetInvocationException) {
                string stateJson = SerializeState(state);
                return AggregateReconstructionResult.Partial(
                    stateJson: stateJson,
                    lastAppliedSequenceNumber: lastApplied,
                    failedSequenceNumber: evt.SequenceNumber,
                    failedEventType: evt.EventTypeName,
                    errorCategory: AggregateReconstructionErrorCategory.ApplyFailed,
                    message: string.Format(
                        CultureInfo.InvariantCulture,
                        "Apply({0}) failed at sequence {1}.",
                        eventClrType.Name,
                        evt.SequenceNumber),
                    timeline: includeTimeline ? timeline : null);
            }

            lastApplied = evt.SequenceNumber;

            timeline?.Add(new AggregateReconstructionTimelineEntry(
                    SequenceNumber: evt.SequenceNumber,
                    EventTypeName: evt.EventTypeName,
                    StateJson: SerializeState(state)));
        }

        return AggregateReconstructionResult.Succeeded(
            stateJson: SerializeState(state),
            lastAppliedSequenceNumber: lastApplied,
            timeline: includeTimeline ? timeline : null);
    }

    private static string SerializeState<TState>(TState state)
        where TState : class
        => JsonSerializer.Serialize(state, EventStorePayloadSerialization.Options);
}
