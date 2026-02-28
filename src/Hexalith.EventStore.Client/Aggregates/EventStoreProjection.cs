
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Hexalith.EventStore.Client.Aggregates;
/// <summary>
/// Abstract base class for event-sourced read-model projections.
/// Provides reflection-based Apply method discovery for typed event handling.
/// Concrete projections declare <c>public void Apply(TEvent e)</c> methods
/// which are automatically discovered and invoked during event replay.
/// </summary>
/// <typeparam name="TReadModel">The read model type that this projection builds.</typeparam>
public abstract class EventStoreProjection<TReadModel>
    where TReadModel : class, new() {
    private static readonly ConcurrentDictionary<Type, Dictionary<string, MethodInfo>> _applyCache = new();

    /// <summary>
    /// Projects events onto a read model by replaying them through typed Apply methods.
    /// </summary>
    /// <param name="events">The events to project, as an enumerable of typed event objects.</param>
    /// <returns>The projected read model with all events applied.</returns>
    public TReadModel Project(System.Collections.IEnumerable events) {
        ArgumentNullException.ThrowIfNull(events);

        Dictionary<string, MethodInfo> applyMethods = GetOrBuildApplyMethods();
        var model = new TReadModel();

        foreach (object? evt in events) {
            if (evt is null) {
                continue;
            }

            string eventTypeName = evt.GetType().Name;
            if (applyMethods.TryGetValue(eventTypeName, out MethodInfo? applyMethod)) {
                applyMethod.Invoke(model, [evt]);
            }
        }

        return model;
    }

    /// <summary>
    /// Projects events from a JSON array onto a read model.
    /// </summary>
    /// <param name="jsonArray">A JSON element containing an array of event objects.</param>
    /// <returns>The projected read model with all events applied.</returns>
    public TReadModel ProjectFromJson(JsonElement jsonArray) {
        if (jsonArray.ValueKind != JsonValueKind.Array) {
            throw new ArgumentException(
                $"Expected JSON array but received {jsonArray.ValueKind}.", nameof(jsonArray));
        }

        Dictionary<string, MethodInfo> applyMethods = GetOrBuildApplyMethods();
        var model = new TReadModel();

        foreach (JsonElement eventElement in jsonArray.EnumerateArray()) {
            if (eventElement.ValueKind != JsonValueKind.Object) {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Unable to project read model '{0}'. Historical event entry must be a JSON object but found '{1}'.",
                        typeof(TReadModel).Name,
                        eventElement.ValueKind));
            }

            if (!eventElement.TryGetProperty("eventTypeName", out JsonElement eventTypeElement)
                || eventTypeElement.ValueKind != JsonValueKind.String) {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Unable to project read model '{0}'. Historical event is missing required string property 'eventTypeName'.",
                        typeof(TReadModel).Name));
            }

            string? eventTypeName = eventTypeElement.GetString();
            if (string.IsNullOrWhiteSpace(eventTypeName)) {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Unable to project read model '{0}'. Historical event has empty 'eventTypeName'.",
                        typeof(TReadModel).Name));
            }

            ApplyEventByName(model, eventTypeName, eventElement, applyMethods);
        }

        return model;
    }

    private static Dictionary<string, MethodInfo> GetOrBuildApplyMethods() =>
        _applyCache.GetOrAdd(typeof(TReadModel), static readModelType => {
            var methods = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);

            foreach (MethodInfo method in readModelType.GetMethods(BindingFlags.Public | BindingFlags.Instance)) {
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
        });

    private static void ApplyEventByName(
        TReadModel model,
        string eventTypeName,
        JsonElement eventElement,
        Dictionary<string, MethodInfo> applyMethods) {
        if (!applyMethods.TryGetValue(eventTypeName, out MethodInfo? applyMethod)) {
            foreach (KeyValuePair<string, MethodInfo> kvp in applyMethods) {
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
                    "Unable to project read model '{0}'. Event type '{1}' has no matching Apply method.",
                    typeof(TReadModel).Name,
                    eventTypeName));
        }

        Type eventType = applyMethod.GetParameters()[0].ParameterType;
        try {
            if (eventElement.TryGetProperty("payload", out JsonElement payloadElement)) {
                object? deserializedEvent = JsonSerializer.Deserialize(payloadElement, eventType);
                if (deserializedEvent is null) {
                    throw new InvalidOperationException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Unable to project read model '{0}'. Payload for event type '{1}' could not be deserialized to '{2}'.",
                            typeof(TReadModel).Name,
                            eventTypeName,
                            eventType.Name));
                }

                applyMethod.Invoke(model, [deserializedEvent]);
            }
            else {
                object? deserializedEvent = JsonSerializer.Deserialize(eventElement, eventType);
                if (deserializedEvent is null) {
                    throw new InvalidOperationException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Unable to project read model '{0}'. Event '{1}' could not be deserialized to '{2}'.",
                            typeof(TReadModel).Name,
                            eventTypeName,
                            eventType.Name));
                }

                applyMethod.Invoke(model, [deserializedEvent]);
            }
        }
        catch (JsonException ex) {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Unable to project read model '{0}'. Event '{1}' could not be deserialized to '{2}'.",
                    typeof(TReadModel).Name,
                    eventTypeName,
                    eventType.Name),
                ex);
        }
    }
}
