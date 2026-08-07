using System.Globalization;
using System.Reflection;
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

    internal static TState? RehydrateState<TState>(object? currentState, ApplyMethodTable applyMethods)
        where TState : class, new() =>
        currentState switch {
            null => null,
            TState typed => typed,
            DomainServiceCurrentState state => RehydrateFromDomainServiceCurrentState<TState>(state, applyMethods),
            JsonElement je when IsDomainServiceCurrentState(je) =>
                RehydrateFromDomainServiceCurrentState<TState>(DeserializeDomainServiceCurrentState(je), applyMethods),
            JsonElement je when je.ValueKind == JsonValueKind.Object => RehydrateFromJsonObject<TState>(je),
            JsonElement je when je.ValueKind == JsonValueKind.Array => ReplayEventsFromJsonArray<TState>(je, applyMethods),
            JsonElement je when je.ValueKind == JsonValueKind.Null => null,
            System.Collections.IEnumerable events when currentState is not string => ReplayEventsFromEnumerable<TState>(events, applyMethods),
            _ => throw new InvalidOperationException(
                $"Expected state type '{typeof(TState).Name}' but received '{currentState.GetType().Name}'."),
        };

    private static DomainServiceCurrentState DeserializeDomainServiceCurrentState(JsonElement json) =>
        json.Deserialize<DomainServiceCurrentState>(EventStorePayloadSerialization.Options)
        ?? throw new InvalidOperationException("Unable to deserialize snapshot-aware current state payload.");

    private static bool IsDomainServiceCurrentState(JsonElement json) =>
        json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty("currentSequence", out _)
        && json.TryGetProperty("events", out _);

    private static TState? RehydrateFromDomainServiceCurrentState<TState>(
        DomainServiceCurrentState currentState,
        ApplyMethodTable applyMethods)
        where TState : class, new() {
        TState? state = currentState.SnapshotState switch {
            null when currentState.Events.Count == 0 => null,
            null => new TState(),
            TState typed => typed,
            DomainServiceCurrentState nestedState => RehydrateFromDomainServiceCurrentState<TState>(nestedState, applyMethods),
            JsonElement je when IsDomainServiceCurrentState(je) =>
                RehydrateFromDomainServiceCurrentState<TState>(DeserializeDomainServiceCurrentState(je), applyMethods),
            JsonElement je when je.ValueKind == JsonValueKind.Object => RehydrateFromJsonObject<TState>(je),
            JsonElement je when je.ValueKind == JsonValueKind.Array => ReplayEventsFromJsonArray<TState>(je, applyMethods),
            JsonElement je when je.ValueKind == JsonValueKind.Null && currentState.Events.Count == 0 => null,
            JsonElement je when je.ValueKind == JsonValueKind.Null => new TState(),
            System.Collections.IEnumerable events when currentState.SnapshotState is not string => ReplayEventsFromEnumerable<TState>(events, applyMethods),
            _ => RehydrateFromArbitrarySnapshot<TState>(currentState.SnapshotState, applyMethods),
        };

        if (state is null) {
            return null;
        }

        ApplyContractEventEnvelopes(state, currentState.Events, applyMethods);
        return state;
    }

    private static TState? RehydrateFromArbitrarySnapshot<TState>(
        object? snapshotState,
        ApplyMethodTable applyMethods)
        where TState : class, new() {
        if (snapshotState is null) {
            return null;
        }

        JsonElement json = JsonSerializer.SerializeToElement(snapshotState, snapshotState.GetType(), EventStorePayloadSerialization.Options);
        return json.ValueKind switch {
            JsonValueKind.Object when IsDomainServiceCurrentState(json) =>
                RehydrateFromDomainServiceCurrentState<TState>(DeserializeDomainServiceCurrentState(json), applyMethods),
            JsonValueKind.Object => RehydrateFromJsonObject<TState>(json),
            JsonValueKind.Array => ReplayEventsFromJsonArray<TState>(json, applyMethods),
            JsonValueKind.Null => null,
            _ => throw new InvalidOperationException(
                $"Expected state type '{typeof(TState).Name}' but received '{snapshotState.GetType().Name}'."),
        };
    }

    private static TState RehydrateFromJsonObject<TState>(JsonElement jsonObject)
        where TState : class, new() {
        var state = new TState();

        var jsonProperties = jsonObject
            .EnumerateObject()
            .ToDictionary(static p => p.Name, static p => p.Value, StringComparer.OrdinalIgnoreCase);

        foreach (PropertyInfo property in typeof(TState).GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
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
            _ = setter.Invoke(state, [value]);
        }

        return state;
    }

    private static TState ReplayEventsFromJsonArray<TState>(JsonElement jsonArray, ApplyMethodTable applyMethods)
        where TState : class, new() {
        var state = new TState();
        foreach (JsonElement eventElement in jsonArray.EnumerateArray()) {
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

            ApplyJsonEventByName(state, eventTypeName, eventElement, applyMethods);
        }

        return state;
    }

    private static TState ReplayEventsFromEnumerable<TState>(System.Collections.IEnumerable events, ApplyMethodTable applyMethods)
        where TState : class, new() {
        var state = new TState();
        foreach (object? evt in events) {
            if (evt is null) {
                continue;
            }

            switch (evt) {
                case EventEnvelope envelope:
                    ApplyContractEventEnvelope(state, envelope, applyMethods);
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

                    ApplyJsonEventByName(state, eventTypeElement.GetString()!, jsonElement, applyMethods);
                    continue;
            }

            MethodInfo? applyMethod = ApplyMethodResolver.TryResolve(applyMethods, evt.GetType());
            if (applyMethod is not null) {
                _ = applyMethod.Invoke(state, [evt]);
                continue;
            }

            throw new MissingApplyMethodException(
                stateType: typeof(TState),
                eventTypeName: evt.GetType().Name);
        }

        return state;
    }

    private static void ApplyContractEventEnvelopes<TState>(
        TState state,
        IReadOnlyList<EventEnvelope> events,
        ApplyMethodTable applyMethods)
        where TState : class, new() {
        foreach (EventEnvelope envelope in events) {
            ApplyContractEventEnvelope(state, envelope, applyMethods);
        }
    }

    private static void ApplyContractEventEnvelope<TState>(
        TState state,
        EventEnvelope envelope,
        ApplyMethodTable applyMethods)
        where TState : class, new() {
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

            _ = applyMethod.Invoke(state, [deserializedEvent]);
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

    private static void ApplyJsonEventByName<TState>(
        TState state,
        string eventTypeName,
        JsonElement eventElement,
        ApplyMethodTable applyMethods)
        where TState : class, new() {
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

                _ = applyMethod.Invoke(state, [deserializedEvent]);
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
                _ = applyMethod.Invoke(state, [deserializedEvent]);
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
}
