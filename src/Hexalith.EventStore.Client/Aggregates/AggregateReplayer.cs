using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Client.Events;
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
        where TState : class, new()
        => Replay<TState>(request, CancellationToken.None);

    /// <summary>Replays legacy events with cancellation checks around each synchronous domain call.</summary>
    /// <typeparam name="TState">The aggregate state type owning the Apply convention.</typeparam>
    /// <param name="request">The reconstruction request.</param>
    /// <param name="cancellationToken">The originating request cancellation token.</param>
    /// <returns>The complete reconstruction result, or a typed non-cancellation failure.</returns>
    /// <remarks>Synchronous Apply and serialization cannot be interrupted; cancellation is observed at their boundaries.</remarks>
    public static AggregateReconstructionResult Replay<TState>(AggregateReconstructionRequest request, CancellationToken cancellationToken)
        where TState : class, new() {
        return Replay<TState>(request, cancellationToken, null);
    }

    /// <summary>Replays through an explicitly configured immutable event evolution registry.</summary>
    public static AggregateReconstructionResult Replay<TState>(AggregateReconstructionRequest request,
        CancellationToken cancellationToken, EventPayloadEvolutionRegistry? evolution)
        where TState : class, new() {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.PagedContext is not null) {
            throw new InvalidOperationException("ReplayRestartRequired: stored-alias replay cannot consume a paged context.");
        }

        using LegacyReplayInput input = LegacyReplayInput.Capture(request, cancellationToken);
        return ReplayAdmitted<TState>(request, input, cancellationToken, evolution);
    }

    /// <summary>Replays under a router-owned private input without allocating another complete payload copy.</summary>
    /// <param name="request">The admitted legacy request.</param>
    /// <param name="input">The private input retained by the caller until replay completes.</param>
    /// <param name="cancellationToken">The originating request cancellation token.</param>
    /// <param name="evolution">The immutable registry; the state assembly convention is used when omitted.</param>
    /// <returns>The complete legacy reconstruction result.</returns>
    internal static AggregateReconstructionResult ReplayAdmitted<TState>(AggregateReconstructionRequest request,
        LegacyReplayInput input, CancellationToken cancellationToken, EventPayloadEvolutionRegistry? evolution = null)
        where TState : class, new() {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        if (input.Refusal is not null) {
            return input.Refusal;
        }

        ApplyMethodTable applyMethods = DomainProcessorStateRehydrator.DiscoverApplyMethods(typeof(TState));
        evolution ??= EventPayloadEvolutionRegistry.ForApplyState(typeof(TState));
        bool includeTimeline = request.IncludeTimeline;
        List<AggregateReconstructionTimelineEntry>? timeline = includeTimeline
            ? new List<AggregateReconstructionTimelineEntry>()
            : null;

        // Sort by stream sequence/version order only (story Replay Semantics).
        // Filter to the inclusive UpToSequence target before sorting so duplicate detection
        // does not flag events outside the target window.
        IReadOnlyList<ReplayEventEnvelope> eligible = input.Events;
        cancellationToken.ThrowIfCancellationRequested();

        if (eligible.Count > 0 && eligible[0].SequenceNumber != 1) {
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.Unexpected,
                "Missing stream sequence 1 detected during replay; reconstruction cannot skip events.",
                failedSequenceNumber: 1,
                failedEventType: string.Empty);
        }

        // Refuse the complete eligible batch before creating state or invoking Apply.
        // A legacy alias route cannot verify a canonical effective event carrier.
        ReplayEventEnvelope? versioned = eligible.FirstOrDefault(static item => item.MetadataVersion != 1
            || item.StoredEventContractType is not null || item.StoredPayloadVersion is < 1 or > 1024
            || item.StoredSerializationFormat is not null || item.StoredEventTypeName is not null
            || item.StoredDigest is not null || item.RegistryFingerprint is not null || item.IsAdapted is not null
            || item.EffectiveEventContractType is not null || item.EffectivePayloadVersion is not null
            || item.EffectiveSerializationFormat is not null || item.EffectivePayload is not null);
        if (versioned is not null) {
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.UnsupportedVersion,
                "RollbackReaderCapabilityHold: replay cannot consume unsupported event metadata.",
                failedSequenceNumber: versioned.SequenceNumber,
                failedEventType: versioned.EventTypeName);
        }

        // Duplicate / conflicting sequence guard: any two events sharing the same sequence
        // number cannot be unambiguously ordered, so reconstruction must fail explicitly
        // rather than pick one arbitrarily.
        for (int i = 1; i < eligible.Count; i++) {
            cancellationToken.ThrowIfCancellationRequested();
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

        long lastApplied = 0;
        var prepared = new List<(ReplayEventEnvelope Envelope, MethodInfo Method, object Payload)>(eligible.Count);
        foreach (ReplayEventEnvelope evt in eligible) {
            cancellationToken.ThrowIfCancellationRequested();
            if (evt.MetadataVersion != 1) {
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

            ResolvedEventPayload effective;
            try {
                effective = evolution.ReadForReplay(evt.EventTypeName, evt.StoredPayloadVersion, evt.Payload, evt.SequenceNumber);
            }
            catch (EventPayloadEvolutionException error) {
                return AggregateReconstructionResult.Failed(
                    AggregateReconstructionErrorCategory.UnsupportedVersion,
                    error.Message,
                    failedSequenceNumber: evt.SequenceNumber,
                    failedEventType: evt.EventTypeName,
                    lastAppliedSequenceNumber: lastApplied);
            }

            MethodInfo? applyMethod;
            try {
                applyMethod = ApplyMethodResolver.TryResolve(
                    applyMethods,
                    effective.EventTypeName,
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
                using JsonDocument doc = effective.Payload is { Length: > 0 }
                    ? JsonDocument.Parse(effective.Payload)
                    : JsonDocument.Parse("{}");
                deserialized = JsonSerializer.Deserialize(doc.RootElement, eventClrType, EventStorePayloadSerialization.Options);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException) {
                cancellationToken.ThrowIfCancellationRequested();
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

            cancellationToken.ThrowIfCancellationRequested();
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

            prepared.Add((evt, applyMethod, deserialized));
        }

        var state = new TState();
        cancellationToken.ThrowIfCancellationRequested();
        // Retain detached canonical bytes before domain code can mutate a working object.
        // A failing Apply must never turn that damaged object into last-good evidence.
        if (!TrySerializeState(state, out string? lastGoodStateJson)) {
            cancellationToken.ThrowIfCancellationRequested();
            return AggregateReconstructionResult.Failed(
                AggregateReconstructionErrorCategory.Unexpected,
                "Aggregate initial state could not be serialized during replay.");
        }

        foreach ((ReplayEventEnvelope evt, MethodInfo applyMethod, object deserialized) in prepared) {
            Type eventClrType = applyMethod.GetParameters()[0].ParameterType;
            try {
                cancellationToken.ThrowIfCancellationRequested();
                _ = applyMethod.Invoke(state, [deserialized]);
            }
            catch (TargetInvocationException error) when (error.InnerException is OperationCanceledException) {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
            catch (TargetInvocationException) {
                cancellationToken.ThrowIfCancellationRequested();
                return AggregateReconstructionResult.Partial(
                    stateJson: lastGoodStateJson,
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

            cancellationToken.ThrowIfCancellationRequested();
            if (!TrySerializeState(state, out string? successorStateJson)) {
                cancellationToken.ThrowIfCancellationRequested();
                return AggregateReconstructionResult.Failed(
                    AggregateReconstructionErrorCategory.Unexpected,
                    "Aggregate state could not be serialized after Apply during replay.",
                    failedSequenceNumber: evt.SequenceNumber,
                    failedEventType: evt.EventTypeName,
                    lastAppliedSequenceNumber: lastApplied);
            }

            cancellationToken.ThrowIfCancellationRequested();
            lastApplied = evt.SequenceNumber;
            lastGoodStateJson = successorStateJson;

            timeline?.Add(new AggregateReconstructionTimelineEntry(
                    SequenceNumber: evt.SequenceNumber,
                    EventTypeName: evt.EventTypeName,
                    StateJson: lastGoodStateJson));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return AggregateReconstructionResult.Succeeded(
            stateJson: lastGoodStateJson,
            lastAppliedSequenceNumber: lastApplied,
            timeline: includeTimeline ? timeline : null);
    }

    private static bool TrySerializeState<TState>(TState state,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? stateJson)
        where TState : class {
        try {
            stateJson = JsonSerializer.Serialize(state, EventStorePayloadSerialization.Options);
            return true;
        }
        catch (Exception error) when (error is not OperationCanceledException) {
            stateJson = null;
            return false;
        }
    }
}
