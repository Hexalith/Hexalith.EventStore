
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;

namespace Hexalith.EventStore.Client.Aggregates;
/// <summary>
/// Abstract base class for event-sourced aggregates. Provides reflection-based command dispatch
/// and state rehydration so that concrete aggregates only declare typed Handle and Apply methods.
/// </summary>
/// <typeparam name="TState">The aggregate state type. Must be a reference type with a parameterless constructor.</typeparam>
public abstract class EventStoreAggregate<TState> : IDomainProcessor
    where TState : class, new() {
    private static readonly ConcurrentDictionary<Type, AggregateMetadata> _metadataCache = new();

    /// <inheritdoc/>
    public async Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState) {
        ArgumentNullException.ThrowIfNull(command);

        AggregateMetadata metadata = GetOrBuildMetadata();
        TState? state = RehydrateState(currentState, metadata);
        return await DispatchCommand(command, state, metadata).ConfigureAwait(false);
    }

    private AggregateMetadata GetOrBuildMetadata() =>
        _metadataCache.GetOrAdd(GetType(), static aggregateType => {
            Dictionary<string, HandleMethodInfo> handleMethods = DiscoverHandleMethods(aggregateType);
            Dictionary<string, MethodInfo> applyMethods = DiscoverApplyMethods(typeof(TState));
            return new AggregateMetadata(handleMethods, applyMethods);
        });

    private static Dictionary<string, HandleMethodInfo> DiscoverHandleMethods(Type aggregateType) {
        var methods = new Dictionary<string, HandleMethodInfo>(StringComparer.Ordinal);

        foreach (MethodInfo method in aggregateType.GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)) {
            if (!method.Name.Equals("Handle", StringComparison.Ordinal)) {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 2) {
                continue;
            }

            Type commandType = parameters[0].ParameterType;
            Type stateParamType = parameters[1].ParameterType;

            // Verify second parameter is TState? (nullable TState)
            Type expectedStateType = typeof(TState);
            if (stateParamType != expectedStateType) {
                continue;
            }

            bool isAsync = method.ReturnType == typeof(Task<DomainResult>);
            bool isSync = method.ReturnType == typeof(DomainResult);
            if (!isAsync && !isSync) {
                continue;
            }

            string commandTypeName = commandType.Name;
            methods[commandTypeName] = new HandleMethodInfo(method, commandType, isAsync, method.IsStatic);
        }

        return methods;
    }

    private static Dictionary<string, MethodInfo> DiscoverApplyMethods(Type stateType) {
        var methods = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);

        foreach (MethodInfo method in stateType.GetMethods(BindingFlags.Public | BindingFlags.Instance)) {
            if (!method.Name.Equals("Apply", StringComparison.Ordinal)) {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 1) {
                continue;
            }

            if (method.ReturnType != typeof(void)) {
                continue;
            }

            string eventTypeName = parameters[0].ParameterType.Name;
            methods[eventTypeName] = method;
        }

        return methods;
    }

    private static TState? RehydrateState(object? currentState, AggregateMetadata metadata) =>
        currentState switch {
            null => null,
            TState typed => typed,
            JsonElement je when je.ValueKind == JsonValueKind.Object =>
                RehydrateFromJsonObject(je),
            JsonElement je when je.ValueKind == JsonValueKind.Array =>
                ReplayEventsFromJsonArray(je, metadata),
            JsonElement je when je.ValueKind == JsonValueKind.Null => null,
            System.Collections.IEnumerable events when currentState is not string =>
                ReplayEventsFromEnumerable(events, metadata),
            _ => throw new InvalidOperationException(
                $"Expected state type '{typeof(TState).Name}' but received '{currentState.GetType().Name}'."),
        };

    private static TState RehydrateFromJsonObject(JsonElement jsonObject) {
        var state = new TState();

        Dictionary<string, JsonElement> jsonProperties = jsonObject
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

            object? value = valueElement.Deserialize(property.PropertyType);
            setter.Invoke(state, [value]);
        }

        return state;
    }

    private static TState ReplayEventsFromJsonArray(JsonElement jsonArray, AggregateMetadata metadata) {
        var state = new TState();
        foreach (JsonElement eventElement in jsonArray.EnumerateArray()) {
            if (eventElement.ValueKind != JsonValueKind.Object) {
                continue;
            }

            if (!eventElement.TryGetProperty("eventTypeName", out JsonElement eventTypeElement)
                || eventTypeElement.ValueKind != JsonValueKind.String) {
                continue;
            }

            string? eventTypeName = eventTypeElement.GetString();
            if (string.IsNullOrWhiteSpace(eventTypeName)) {
                continue;
            }

            ApplyEventByName(state, eventTypeName, eventElement, metadata);
        }

        return state;
    }

    private static TState ReplayEventsFromEnumerable(System.Collections.IEnumerable events, AggregateMetadata metadata) {
        var state = new TState();
        foreach (object? evt in events) {
            if (evt is null) {
                continue;
            }

            string eventTypeName = evt.GetType().Name;
            if (metadata.ApplyMethods.TryGetValue(eventTypeName, out MethodInfo? applyMethod)) {
                applyMethod.Invoke(state, [evt]);
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

    private static void ApplyEventByName(TState state, string eventTypeName, JsonElement eventElement, AggregateMetadata metadata) {
        // Try exact match first, then suffix match for fully qualified names
        if (!metadata.ApplyMethods.TryGetValue(eventTypeName, out MethodInfo? applyMethod)) {
            foreach (KeyValuePair<string, MethodInfo> kvp in metadata.ApplyMethods) {
                if (eventTypeName.EndsWith(kvp.Key, StringComparison.Ordinal)) {
                    applyMethod = kvp.Value;
                    break;
                }
            }
        }

        if (applyMethod is null) {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Unable to rehydrate aggregate state '{0}'. Event type '{1}' has no matching Apply method.",
                    typeof(TState).Name,
                    eventTypeName));
        }

        Type eventType = applyMethod.GetParameters()[0].ParameterType;
        if (eventElement.TryGetProperty("payload", out JsonElement payloadElement)) {
            object? deserializedEvent = JsonSerializer.Deserialize(payloadElement, eventType);
            if (deserializedEvent is not null) {
                applyMethod.Invoke(state, [deserializedEvent]);
            }
        }
        else {
            object? deserializedEvent = JsonSerializer.Deserialize(eventElement, eventType);
            if (deserializedEvent is not null) {
                applyMethod.Invoke(state, [deserializedEvent]);
            }
        }
    }

    private async Task<DomainResult> DispatchCommand(CommandEnvelope command, TState? state, AggregateMetadata metadata) {
        if (!metadata.HandleMethods.TryGetValue(command.CommandType, out HandleMethodInfo? handleInfo)) {
            throw new InvalidOperationException(
                $"No Handle method found for command type '{command.CommandType}' on aggregate '{GetType().Name}'.");
        }

        object commandPayload = command.Payload.Length == 0
            ? throw new InvalidOperationException(
                $"Command '{command.CommandType}' has an empty payload. Expected valid JSON for {handleInfo.CommandType.Name}.")
            : JsonSerializer.Deserialize(command.Payload, handleInfo.CommandType)
              ?? throw new InvalidOperationException(
                  $"Failed to deserialize payload for command '{command.CommandType}' to {handleInfo.CommandType.Name}.");

        object? result = handleInfo.Method.Invoke(handleInfo.IsStatic ? null : this, [commandPayload, state]);
        return result switch {
            Task<DomainResult> asyncResult => await asyncResult.ConfigureAwait(false),
            DomainResult syncResult => syncResult,
            _ => throw new InvalidOperationException(
                $"Handle method for '{command.CommandType}' returned unexpected type '{result?.GetType().Name ?? "null"}'."),
        };
    }

    private sealed record AggregateMetadata(
        Dictionary<string, HandleMethodInfo> HandleMethods,
        Dictionary<string, MethodInfo> ApplyMethods);

    private sealed record HandleMethodInfo(
        MethodInfo Method,
        Type CommandType,
        bool IsAsync,
        bool IsStatic);
}
