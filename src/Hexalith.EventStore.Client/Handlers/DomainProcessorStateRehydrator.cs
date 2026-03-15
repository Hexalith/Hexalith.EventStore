using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Handlers;

internal static class DomainProcessorStateRehydrator
{
    private static readonly ConcurrentDictionary<Type, Dictionary<string, MethodInfo>> ApplyMethodCache = new();
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    internal static Dictionary<string, MethodInfo> DiscoverApplyMethods(Type stateType) =>
        ApplyMethodCache.GetOrAdd(
            stateType,
            static type =>
            {
                var methods = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);

                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!method.Name.Equals("Apply", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != 1 || method.ReturnType != typeof(void))
                    {
                        continue;
                    }

                    string eventTypeName = parameters[0].ParameterType.Name;
                    methods[eventTypeName] = method;
                }

                return methods;
            });

    internal static TState? RehydrateState<TState>(object? currentState, Dictionary<string, MethodInfo> applyMethods)
        where TState : class, new() =>
        currentState switch
        {
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
        json.Deserialize<DomainServiceCurrentState>(WebJsonOptions)
        ?? throw new InvalidOperationException("Unable to deserialize snapshot-aware current state payload.");

    private static bool IsDomainServiceCurrentState(JsonElement json) =>
        json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty("currentSequence", out _)
        && json.TryGetProperty("events", out _);

    private static TState? RehydrateFromDomainServiceCurrentState<TState>(
        DomainServiceCurrentState currentState,
        Dictionary<string, MethodInfo> applyMethods)
        where TState : class, new()
    {
        TState? state = currentState.SnapshotState switch
        {
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

        if (state is null)
        {
            return null;
        }

        ApplyContractEventEnvelopes(state, currentState.Events, applyMethods);
        return state;
    }

    private static TState? RehydrateFromArbitrarySnapshot<TState>(
        object? snapshotState,
        Dictionary<string, MethodInfo> applyMethods)
        where TState : class, new()
    {
        if (snapshotState is null)
        {
            return null;
        }

        JsonElement json = JsonSerializer.SerializeToElement(snapshotState, snapshotState.GetType(), WebJsonOptions);
        return json.ValueKind switch
        {
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
        where TState : class, new()
    {
        var state = new TState();

        var jsonProperties = jsonObject
            .EnumerateObject()
            .ToDictionary(static p => p.Name, static p => p.Value, StringComparer.OrdinalIgnoreCase);

        foreach (PropertyInfo property in typeof(TState).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            MethodInfo? setter = property.SetMethod;
            if (setter is null)
            {
                continue;
            }

            if (!jsonProperties.TryGetValue(property.Name, out JsonElement valueElement))
            {
                continue;
            }

            object? value = valueElement.Deserialize(property.PropertyType, WebJsonOptions);
            _ = setter.Invoke(state, [value]);
        }

        return state;
    }

    private static TState ReplayEventsFromJsonArray<TState>(JsonElement jsonArray, Dictionary<string, MethodInfo> applyMethods)
        where TState : class, new()
    {
        var state = new TState();
        foreach (JsonElement eventElement in jsonArray.EnumerateArray())
        {
            if (eventElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Unable to rehydrate aggregate state '{0}'. Historical event entry must be a JSON object but found '{1}'.",
                        typeof(TState).Name,
                        eventElement.ValueKind));
            }

            if (!eventElement.TryGetProperty("eventTypeName", out JsonElement eventTypeElement)
                || eventTypeElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Unable to rehydrate aggregate state '{0}'. Historical event is missing required string property 'eventTypeName'.",
                        typeof(TState).Name));
            }

            string? eventTypeName = eventTypeElement.GetString();
            if (string.IsNullOrWhiteSpace(eventTypeName))
            {
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

    private static TState ReplayEventsFromEnumerable<TState>(System.Collections.IEnumerable events, Dictionary<string, MethodInfo> applyMethods)
        where TState : class, new()
    {
        var state = new TState();
        foreach (object? evt in events)
        {
            if (evt is null)
            {
                continue;
            }

            switch (evt)
            {
                case EventEnvelope envelope:
                    ApplyContractEventEnvelope(state, envelope, applyMethods);
                    continue;
                case JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Object:
                    if (!jsonElement.TryGetProperty("eventTypeName", out JsonElement eventTypeElement)
                        || eventTypeElement.ValueKind != JsonValueKind.String)
                    {
                        throw new InvalidOperationException(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Unable to rehydrate aggregate state '{0}'. Historical event is missing required string property 'eventTypeName'.",
                                typeof(TState).Name));
                    }

                    ApplyJsonEventByName(state, eventTypeElement.GetString()!, jsonElement, applyMethods);
                    continue;
            }

            string eventTypeName = evt.GetType().Name;
            if (applyMethods.TryGetValue(eventTypeName, out MethodInfo? applyMethod))
            {
                _ = applyMethod.Invoke(state, [evt]);
                continue;
            }

            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Unable to rehydrate aggregate '{0}' from event type '{1}'. No matching Apply method found on state '{2}'.",
                    typeof(TState).DeclaringType?.Name ?? typeof(TState).Name,
                    eventTypeName,
                    typeof(TState).Name));
        }

        return state;
    }

    private static void ApplyContractEventEnvelopes<TState>(
        TState state,
        IReadOnlyList<EventEnvelope> events,
        Dictionary<string, MethodInfo> applyMethods)
        where TState : class, new()
    {
        foreach (EventEnvelope envelope in events)
        {
            ApplyContractEventEnvelope(state, envelope, applyMethods);
        }
    }

    private static void ApplyContractEventEnvelope<TState>(
        TState state,
        EventEnvelope envelope,
        Dictionary<string, MethodInfo> applyMethods)
        where TState : class, new()
    {
        MethodInfo applyMethod = ResolveApplyMethod(envelope.Metadata.EventTypeName, applyMethods, typeof(TState));
        Type eventType = applyMethod.GetParameters()[0].ParameterType;

        try
        {
            using JsonDocument payloadDoc = JsonDocument.Parse(envelope.Payload);
            object? deserializedEvent = JsonSerializer.Deserialize(payloadDoc.RootElement, eventType, WebJsonOptions)
                ?? throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Unable to rehydrate aggregate state '{0}'. Payload for event type '{1}' could not be deserialized to '{2}'.",
                        typeof(TState).Name,
                        envelope.Metadata.EventTypeName,
                        eventType.Name));

            _ = applyMethod.Invoke(state, [deserializedEvent]);
        }
        catch (JsonException ex)
        {
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
        Dictionary<string, MethodInfo> applyMethods)
        where TState : class, new()
    {
        MethodInfo applyMethod = ResolveApplyMethod(eventTypeName, applyMethods, typeof(TState));
        Type eventType = applyMethod.GetParameters()[0].ParameterType;

        try
        {
            if (eventElement.TryGetProperty("payload", out JsonElement payloadElement))
            {
                object? deserializedEvent;
                if (payloadElement.ValueKind == JsonValueKind.String)
                {
                    byte[] payloadBytes = payloadElement.GetBytesFromBase64();
                    using JsonDocument payloadDoc = JsonDocument.Parse(payloadBytes);
                    deserializedEvent = JsonSerializer.Deserialize(payloadDoc.RootElement, eventType, WebJsonOptions);
                }
                else
                {
                    deserializedEvent = JsonSerializer.Deserialize(payloadElement, eventType, WebJsonOptions);
                }

                if (deserializedEvent is null)
                {
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
            else
            {
                object? deserializedEvent = JsonSerializer.Deserialize(eventElement, eventType, WebJsonOptions)
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
        catch (JsonException ex)
        {
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

    private static MethodInfo ResolveApplyMethod(
        string eventTypeName,
        Dictionary<string, MethodInfo> applyMethods,
        Type stateType)
    {
        if (!applyMethods.TryGetValue(eventTypeName, out MethodInfo? applyMethod))
        {
            foreach (KeyValuePair<string, MethodInfo> kvp in applyMethods)
            {
                if (eventTypeName.EndsWith(kvp.Key, StringComparison.Ordinal))
                {
                    applyMethod = kvp.Value;
                    break;
                }
            }
        }

        return applyMethod ?? throw new InvalidOperationException(
            string.Format(
                CultureInfo.InvariantCulture,
                "Unable to rehydrate aggregate state '{0}'. Event type '{1}' has no matching Apply method.",
                stateType.Name,
                eventTypeName));
    }
}