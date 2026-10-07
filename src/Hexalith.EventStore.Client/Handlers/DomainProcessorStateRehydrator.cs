using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;

using Hexalith.EventStore.Client.Aggregates;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Serialization;

namespace Hexalith.EventStore.Client.Handlers;

internal static class DomainProcessorStateRehydrator {
    /// <summary>
    /// Gets the shared Apply table for <paramref name="stateType"/>. Discovery and resolution live in
    /// <see cref="ApplyMethodResolver"/> so the rehydrate and projection paths cannot diverge.
    /// </summary>
    /// <param name="stateType">The aggregate state type declaring the Apply methods.</param>
    /// <returns>The shared Apply table.</returns>
    internal static ApplyMethodTable DiscoverApplyMethods(Type stateType)
        => ApplyMethodResolver.GetOrBuildTable(stateType);

    internal static TState? RehydrateState<TState>(object? currentState, ApplyMethodTable applyMethods, CancellationToken cancellationToken = default)
        where TState : class, new() {
        cancellationToken.ThrowIfCancellationRequested();
        TState? rehydratedState;
        using var input = new LegacyCommandReplayInput(cancellationToken);
        try {
            currentState = CaptureReplayInput<TState>(currentState, cancellationToken, input);
            rehydratedState = currentState switch {
                null => null,
                TState typed => typed,
                DomainServiceCurrentState state => RehydrateFromDomainServiceCurrentState<TState>(state, applyMethods, cancellationToken),
                JsonElement je when IsDomainServiceCurrentState(je) =>
                    RehydrateFromDomainServiceCurrentState<TState>(DeserializeDomainServiceCurrentState(je), applyMethods, cancellationToken),
                JsonElement je when je.ValueKind == JsonValueKind.Object => RehydrateFromJsonObject<TState>(je, cancellationToken),
                JsonElement je when je.ValueKind == JsonValueKind.Array => ReplayEventsFromJsonArray<TState>(je, applyMethods, cancellationToken),
                JsonElement je when je.ValueKind == JsonValueKind.Null => null,
                System.Collections.IEnumerable events when currentState is not string => ReplayEventsFromEnumerable<TState>(events, applyMethods, cancellationToken),
                _ => throw new InvalidOperationException(
                    $"Expected state type '{typeof(TState).Name}' but received '{currentState.GetType().Name}'."),
            };
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested) {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return rehydratedState;
    }

    private static object? CaptureReplayInput<TState>(object? input, CancellationToken cancellationToken, LegacyCommandReplayInput owner, int depth = 0)
        where TState : class, new() {
        cancellationToken.ThrowIfCancellationRequested();
        switch (input) {
            case TState:
                return input;
            case DomainServiceCurrentState current:
                owner.AdmitSnapshotWrapper(depth);
                var events = CaptureEvents(current.Events, cancellationToken, owner).Cast<EventEnvelope>().ToArray();
                return current with {
                    Events = events,
                    SnapshotState = CaptureReplayInput<TState>(current.SnapshotState, cancellationToken, owner, depth + 1),
                };
            case JsonElement json when IsDomainServiceCurrentState(json):
                owner.AdmitSnapshotWrapper(depth);
                // Check aliases before transport deserialization can select one of duplicate metadata members.
                foreach (JsonElement envelope in json.GetProperty("events").EnumerateArray()) {
                    ValidateJsonReplayMetadata(GetReplayProperty(envelope, "metadata") ?? envelope);
                }
                return CaptureReplayInput<TState>(DeserializeDomainServiceCurrentState(json), cancellationToken, owner, depth);
            case JsonElement { ValueKind: JsonValueKind.Array } json:
                JsonElement captured = json.Clone();
                foreach (JsonElement item in captured.EnumerateArray()) {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (item.ValueKind == JsonValueKind.Object) { ValidateJsonReplayMetadata(item); }
                }
                return captured;
            case System.Collections.IEnumerable sequence when input is not string:
                return CaptureEvents(sequence, cancellationToken, owner);
            default:
                return input;
        }
    }

    private static List<object?> CaptureEvents(System.Collections.IEnumerable events, CancellationToken cancellationToken, LegacyCommandReplayInput owner) {
        List<object?> captured = owner.CaptureEvents(events);

        // No payload converters run until every caller-owned reference and byte array is detached.
        foreach (object? item in captured) {
            cancellationToken.ThrowIfCancellationRequested();
            switch (item) {
                case EventEnvelope envelope:
                    RequireSupportedReplayMetadata(envelope.Metadata.MetadataVersion, envelope.Metadata.SerializationFormat,
                        envelope.Metadata.EventContractType, envelope.Metadata.PayloadVersion);
                    break;
                case JsonElement { ValueKind: JsonValueKind.Object } json:
                    ValidateJsonReplayMetadata(json);
                    break;
            }
        }
        return captured;
    }

    private static JsonElement? GetReplayProperty(JsonElement json, string name) {
        JsonElement? found = null;
        foreach (JsonProperty property in json.EnumerateObject()) {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) { continue; }
            if (found is { } prior && !JsonElement.DeepEquals(prior, property.Value)) {
                throw new InvalidOperationException("CapabilityMismatch: replay metadata contains conflicting property aliases.");
            }
            found = property.Value;
        }
        return found;
    }

    private static void ValidateJsonReplayMetadata(JsonElement json) {
        JsonElement? version = GetReplayProperty(json, "metadataVersion");
        JsonElement? format = GetReplayProperty(json, "serializationFormat");
        JsonElement? contract = GetReplayProperty(json, "eventContractType");
        JsonElement? payloadVersion = GetReplayProperty(json, "payloadVersion");
        RequireSupportedReplayMetadata(version is null ? 1 : version.Value.GetInt32(),
            format is null ? "json" : format.Value.GetString(),
            contract is { ValueKind: not JsonValueKind.Null } contractValue ? contractValue.GetString() : null,
            payloadVersion is { ValueKind: not JsonValueKind.Null } payloadValue ? payloadValue.GetInt32() : null);
    }

    private static DomainServiceCurrentState DeserializeDomainServiceCurrentState(JsonElement json) =>
        json.Deserialize<DomainServiceCurrentState>(EventStorePayloadSerialization.Options)
        ?? throw new InvalidOperationException("Unable to deserialize snapshot-aware current state payload.");

    private static bool IsDomainServiceCurrentState(JsonElement json) =>
        json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty("currentSequence", out _)
        && json.TryGetProperty("events", out _);

    private static TState? RehydrateFromDomainServiceCurrentState<TState>(
        DomainServiceCurrentState currentState,
        ApplyMethodTable applyMethods,
        CancellationToken cancellationToken)
        where TState : class, new() {
        cancellationToken.ThrowIfCancellationRequested();
        // Resolve and deserialize the entire tail before snapshot replay can invoke Apply.
        var prepared = currentState.Events.Select(envelope =>
            PrepareContractEventEnvelope<TState>(envelope, applyMethods, cancellationToken)).ToList();
        TState? state = currentState.SnapshotState switch {
            null when currentState.Events.Count == 0 => null,
            null => new TState(),
            TState typed => typed,
            DomainServiceCurrentState nestedState => RehydrateFromDomainServiceCurrentState<TState>(nestedState, applyMethods, cancellationToken),
            JsonElement je when IsDomainServiceCurrentState(je) =>
                RehydrateFromDomainServiceCurrentState<TState>(DeserializeDomainServiceCurrentState(je), applyMethods, cancellationToken),
            JsonElement je when je.ValueKind == JsonValueKind.Object => RehydrateFromJsonObject<TState>(je, cancellationToken),
            JsonElement je when je.ValueKind == JsonValueKind.Array => ReplayEventsFromJsonArray<TState>(je, applyMethods, cancellationToken),
            JsonElement je when je.ValueKind == JsonValueKind.Null && currentState.Events.Count == 0 => null,
            JsonElement je when je.ValueKind == JsonValueKind.Null => new TState(),
            System.Collections.IEnumerable events when currentState.SnapshotState is not string => ReplayEventsFromEnumerable<TState>(events, applyMethods, cancellationToken),
            _ => RehydrateFromArbitrarySnapshot<TState>(currentState.SnapshotState, applyMethods, cancellationToken),
        };

        if (state is null) {
            return null;
        }

        ApplyPreparedEvents(state, prepared, cancellationToken);
        return state;
    }

    private static TState? RehydrateFromArbitrarySnapshot<TState>(
        object? snapshotState,
        ApplyMethodTable applyMethods,
        CancellationToken cancellationToken)
        where TState : class, new() {
        cancellationToken.ThrowIfCancellationRequested();
        if (snapshotState is null) {
            return null;
        }

        JsonElement json = JsonSerializer.SerializeToElement(snapshotState, snapshotState.GetType(), EventStorePayloadSerialization.Options);
        return json.ValueKind switch {
            JsonValueKind.Object when IsDomainServiceCurrentState(json) =>
                RehydrateFromDomainServiceCurrentState<TState>(DeserializeDomainServiceCurrentState(json), applyMethods, cancellationToken),
            JsonValueKind.Object => RehydrateFromJsonObject<TState>(json, cancellationToken),
            JsonValueKind.Array => ReplayEventsFromJsonArray<TState>(json, applyMethods, cancellationToken),
            JsonValueKind.Null => null,
            _ => throw new InvalidOperationException(
                $"Expected state type '{typeof(TState).Name}' but received '{snapshotState.GetType().Name}'."),
        };
    }

    private static TState RehydrateFromJsonObject<TState>(JsonElement jsonObject, CancellationToken cancellationToken)
        where TState : class, new() {
        cancellationToken.ThrowIfCancellationRequested();
        var state = new TState();

        var jsonProperties = jsonObject
            .EnumerateObject()
            .ToDictionary(static p => p.Name, static p => p.Value, StringComparer.OrdinalIgnoreCase);

        foreach (PropertyInfo property in typeof(TState).GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
            cancellationToken.ThrowIfCancellationRequested();
            if (property.GetIndexParameters().Length != 0) {
                continue;
            }

            MethodInfo? setter = property.SetMethod;
            if (setter is null) {
                continue;
            }

            if (!jsonProperties.TryGetValue(property.Name, out JsonElement valueElement)) {
                continue;
            }

            object? value = valueElement.Deserialize(property.PropertyType, EventStorePayloadSerialization.Options);
            InvokeWithCancellation(setter, state, value, cancellationToken);
        }

        return state;
    }

    private static TState ReplayEventsFromJsonArray<TState>(JsonElement jsonArray, ApplyMethodTable applyMethods,
        CancellationToken cancellationToken)
        where TState : class, new() {
        cancellationToken.ThrowIfCancellationRequested();
        var prepared = new List<(MethodInfo Method, object Event)>();
        foreach (JsonElement eventElement in jsonArray.EnumerateArray()) {
            cancellationToken.ThrowIfCancellationRequested();
            if (eventElement.ValueKind != JsonValueKind.Object) {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Unable to rehydrate aggregate state '{0}'. Historical event entry must be a JSON object but found '{1}'.",
                        typeof(TState).Name,
                        eventElement.ValueKind));
            }

            if (!eventElement.TryGetProperty("eventTypeName", out JsonElement eventTypeElement)
                || eventTypeElement.ValueKind != JsonValueKind.String) {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Unable to rehydrate aggregate state '{0}'. Historical event is missing required string property 'eventTypeName'.",
                        typeof(TState).Name));
            }

            string? eventTypeName = eventTypeElement.GetString();
            if (string.IsNullOrWhiteSpace(eventTypeName)) {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Unable to rehydrate aggregate state '{0}'. Historical event has empty 'eventTypeName'.",
                        typeof(TState).Name));
            }

            prepared.Add(PrepareJsonEventByName<TState>(eventTypeName, eventElement, applyMethods, cancellationToken));
        }

        var state = new TState();
        ApplyPreparedEvents(state, prepared, cancellationToken);
        return state;
    }

    private static TState ReplayEventsFromEnumerable<TState>(System.Collections.IEnumerable events, ApplyMethodTable applyMethods,
        CancellationToken cancellationToken)
        where TState : class, new() {
        cancellationToken.ThrowIfCancellationRequested();
        var prepared = new List<(MethodInfo Method, object Event)>();
        foreach (object? evt in events) {
            cancellationToken.ThrowIfCancellationRequested();
            if (evt is null) {
                continue;
            }

            switch (evt) {
                case EventEnvelope envelope:
                    prepared.Add(PrepareContractEventEnvelope<TState>(envelope, applyMethods, cancellationToken));
                    continue;
                case JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Object:
                    if (!jsonElement.TryGetProperty("eventTypeName", out JsonElement eventTypeElement)
                        || eventTypeElement.ValueKind != JsonValueKind.String) {
                        throw new InvalidOperationException(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Unable to rehydrate aggregate state '{0}'. Historical event is missing required string property 'eventTypeName'.",
                                typeof(TState).Name));
                    }

                    prepared.Add(PrepareJsonEventByName<TState>(eventTypeElement.GetString()!, jsonElement, applyMethods, cancellationToken));
                    continue;
            }

            MethodInfo? applyMethod = ApplyMethodResolver.TryResolve(applyMethods, evt.GetType());
            if (applyMethod is not null) {
                prepared.Add((applyMethod, evt));
                continue;
            }

            throw new MissingApplyMethodException(
                stateType: typeof(TState),
                eventTypeName: evt.GetType().Name);
        }

        var state = new TState();
        ApplyPreparedEvents(state, prepared, cancellationToken);
        return state;
    }

    private static void ApplyPreparedEvents<TState>(TState state,
        IReadOnlyList<(MethodInfo Method, object Event)> prepared, CancellationToken cancellationToken)
        where TState : class, new()
    {
        foreach ((MethodInfo method, object payload) in prepared)
        {
            InvokeWithCancellation(method, state, payload, cancellationToken);
        }
    }

    private static (MethodInfo Method, object Event) PrepareContractEventEnvelope<TState>(
        EventEnvelope envelope,
        ApplyMethodTable applyMethods,
        CancellationToken cancellationToken)
        where TState : class, new() {
        cancellationToken.ThrowIfCancellationRequested();
        RequireSupportedReplayMetadata(envelope.Metadata.MetadataVersion, envelope.Metadata.SerializationFormat,
            envelope.Metadata.EventContractType, envelope.Metadata.PayloadVersion);
        MethodInfo? applyMethod = ApplyMethodResolver.TryResolve(
            applyMethods,
            envelope.Metadata.EventTypeName,
            envelope.Metadata.MessageId,
            envelope.Metadata.AggregateId) ?? throw new MissingApplyMethodException(
                stateType: typeof(TState),
                eventTypeName: envelope.Metadata.EventTypeName,
                messageId: envelope.Metadata.MessageId,
                aggregateId: envelope.Metadata.AggregateId);
        Type eventType = applyMethod.GetParameters()[0].ParameterType;

        try {
            using var payloadDoc = JsonDocument.Parse(envelope.Payload);
            object? deserializedEvent = JsonSerializer.Deserialize(payloadDoc.RootElement, eventType, EventStorePayloadSerialization.Options)
                ?? throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Unable to rehydrate aggregate state '{0}'. Payload for event type '{1}' could not be deserialized to '{2}'.",
                        typeof(TState).Name,
                        envelope.Metadata.EventTypeName,
                        eventType.Name));

            cancellationToken.ThrowIfCancellationRequested();
            return (applyMethod, deserializedEvent);
        }
        catch (JsonException ex) {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Unable to rehydrate aggregate state '{0}'. Event '{1}' could not be deserialized to '{2}'.",
                    typeof(TState).Name,
                    envelope.Metadata.EventTypeName,
                    eventType.Name),
                ex);
        }
    }

    private static (MethodInfo Method, object Event) PrepareJsonEventByName<TState>(
        string eventTypeName,
        JsonElement eventElement,
        ApplyMethodTable applyMethods,
        CancellationToken cancellationToken)
        where TState : class, new() {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateJsonReplayMetadata(eventElement);
        MethodInfo? applyMethod = ApplyMethodResolver.TryResolve(applyMethods, eventTypeName) ?? throw new MissingApplyMethodException(
                stateType: typeof(TState),
                eventTypeName: eventTypeName);
        Type eventType = applyMethod.GetParameters()[0].ParameterType;

        try {
            if (eventElement.TryGetProperty("payload", out JsonElement payloadElement)) {
                object? deserializedEvent;
                if (payloadElement.ValueKind == JsonValueKind.String) {
                    byte[] payloadBytes = payloadElement.GetBytesFromBase64();
                    using var payloadDoc = JsonDocument.Parse(payloadBytes);
                    deserializedEvent = JsonSerializer.Deserialize(payloadDoc.RootElement, eventType, EventStorePayloadSerialization.Options);
                }
                else {
                    deserializedEvent = JsonSerializer.Deserialize(payloadElement, eventType, EventStorePayloadSerialization.Options);
                }

                if (deserializedEvent is null) {
                    throw new InvalidOperationException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Unable to rehydrate aggregate state '{0}'. Payload for event type '{1}' could not be deserialized to '{2}'.",
                            typeof(TState).Name,
                            eventTypeName,
                            eventType.Name));
                }

                cancellationToken.ThrowIfCancellationRequested();
                return (applyMethod, deserializedEvent);
            }
            else {
                object? deserializedEvent = JsonSerializer.Deserialize(eventElement, eventType, EventStorePayloadSerialization.Options)
                    ?? throw new InvalidOperationException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Unable to rehydrate aggregate state '{0}'. Event '{1}' could not be deserialized to '{2}'.",
                            typeof(TState).Name,
                            eventTypeName,
                            eventType.Name));
                cancellationToken.ThrowIfCancellationRequested();
                return (applyMethod, deserializedEvent);
            }
        }
        catch (JsonException ex) {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Unable to rehydrate aggregate state '{0}'. Event '{1}' could not be deserialized to '{2}'.",
                    typeof(TState).Name,
                    eventTypeName,
                    eventType.Name),
                ex);
        }
    }

    private static void RequireSupportedReplayMetadata(int metadataVersion, string? serializationFormat,
        string? eventContractType = null, int? payloadVersion = null)
    {
        if (metadataVersion != 1 || !string.Equals(serializationFormat, "json", StringComparison.OrdinalIgnoreCase)
            || eventContractType is not null || payloadVersion is not null)
        {
            throw new InvalidOperationException("CapabilityMismatch: replay requires supported application event metadata and JSON payloads.");
        }
    }

    private static void InvokeWithCancellation(MethodInfo method, object state, object? argument, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        try {
            _ = method.Invoke(state, [argument]);
        }
        catch (TargetInvocationException error) when (error.InnerException is OperationCanceledException) {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}
