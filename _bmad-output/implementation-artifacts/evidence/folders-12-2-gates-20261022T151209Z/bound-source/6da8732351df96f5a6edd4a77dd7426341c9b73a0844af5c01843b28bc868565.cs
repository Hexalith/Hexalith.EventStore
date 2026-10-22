
using System.Globalization;
using System.Reflection;
using System.Text.Json;

using Hexalith.EventStore.Client.Configuration;
using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Serialization;

using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.Client.Aggregates;
/// <summary>
/// Abstract base class for event-sourced read-model projections.
/// Provides reflection-based Apply method discovery for typed event handling.
/// Concrete projections declare <c>public void Apply(TEvent e)</c> methods
/// which are automatically discovered and invoked during event replay.
/// </summary>
/// <typeparam name="TReadModel">The read model type that this projection builds.</typeparam>
public abstract class EventStoreProjection<TReadModel> : IEventStoreProjection
    where TReadModel : class, new() {
    private string? _domainName;

    /// <summary>
    /// Gets or sets the projection change notifier. Set post-construction by DI registration.
    /// When set, <see cref="Project(System.Collections.IEnumerable)"/> auto-calls <see cref="IProjectionChangeNotifier"/>
    /// after successful projection. When null, a warning is logged (FM-5).
    /// </summary>
    public IProjectionChangeNotifier? Notifier { get; set; }

    /// <summary>
    /// Gets or sets the logger. Set post-construction by DI registration.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Gets or sets the tenant identifier for auto-notify. Set by the caller before projection.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Called once during cascade configuration resolution to allow subclasses to set per-domain options imperatively (Layer 3).
    /// The default implementation is a no-op. Override this method to customize domain resource names.
    /// </summary>
    /// <param name="options">The domain options to configure. Set non-null values to override convention defaults.</param>
    /// <remarks>
    /// This method is called during <c>UseEventStore()</c> cascade resolution, NOT during event projection.
    /// </remarks>
    protected virtual void OnConfiguring(EventStoreDomainOptions options) {
        // No-op by default. Subclasses override to set per-domain options.
    }

    /// <summary>
    /// Internal entry point for the cascade resolver to invoke <see cref="OnConfiguring"/>.
    /// </summary>
    /// <param name="options">The domain options to configure.</param>
    internal void InvokeOnConfiguring(EventStoreDomainOptions options) => OnConfiguring(options);

    /// <summary>
    /// Projects events onto a read model by replaying them through typed Apply methods.
    /// </summary>
    /// <param name="events">The events to project, as an enumerable of typed event objects.</param>
    /// <returns>The projected read model with all events applied.</returns>
    public TReadModel Project(System.Collections.IEnumerable events) =>
        Project(events, CancellationToken.None);

    /// <summary>
    /// Projects events onto a read model by replaying them through typed Apply methods.
    /// </summary>
    /// <param name="events">The events to project, as an enumerable of typed event objects.</param>
    /// <param name="cancellationToken">The token to observe between event applications.</param>
    /// <returns>The projected read model with all events applied.</returns>
    public TReadModel Project(System.Collections.IEnumerable events, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(events);

        ApplyMethodTable applyMethods = GetOrBuildApplyMethods();
        var model = new TReadModel();

        foreach (object? evt in events) {
            cancellationToken.ThrowIfCancellationRequested();

            if (evt is null) {
                continue;
            }

            // Resolving by runtime CLR type keeps this entry point symmetric with the name-based one:
            // it binds exactly where the name-based path would throw on a short-name collision, instead
            // of returning null and silently dropping the event.
            MethodInfo? applyMethod = ApplyMethodResolver.TryResolve(applyMethods, evt.GetType());
            if (applyMethod is not null) {
                _ = applyMethod.Invoke(model, [evt]);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        FireProjectionChangeNotification();

        return model;
    }

    /// <summary>
    /// Projects events from a JSON array onto a read model.
    /// </summary>
    /// <param name="jsonArray">A JSON element containing an array of event objects.</param>
    /// <returns>The projected read model with all events applied.</returns>
    public TReadModel ProjectFromJson(JsonElement jsonArray) =>
        ProjectFromJson(jsonArray, CancellationToken.None);

    /// <summary>
    /// Projects events from a JSON array onto a read model.
    /// </summary>
    /// <param name="jsonArray">A JSON element containing an array of event objects.</param>
    /// <param name="cancellationToken">The token to observe between event applications.</param>
    /// <returns>The projected read model with all events applied.</returns>
    public TReadModel ProjectFromJson(JsonElement jsonArray, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();

        if (jsonArray.ValueKind != JsonValueKind.Array) {
            throw new ArgumentException(
                $"Expected JSON array but received {jsonArray.ValueKind}.", nameof(jsonArray));
        }

        ApplyMethodTable applyMethods = GetOrBuildApplyMethods();
        var model = new TReadModel();

        foreach (JsonElement eventElement in jsonArray.EnumerateArray()) {
            cancellationToken.ThrowIfCancellationRequested();

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

        cancellationToken.ThrowIfCancellationRequested();
        FireProjectionChangeNotification();

        return model;
    }

    private static ApplyMethodTable GetOrBuildApplyMethods()
        => ApplyMethodResolver.GetOrBuildTable(typeof(TReadModel));

    private static void ApplyEventByName(
        TReadModel model,
        string eventTypeName,
        JsonElement eventElement,
        ApplyMethodTable applyMethods) {
        MethodInfo? applyMethod = ApplyMethodResolver.TryResolve(applyMethods, eventTypeName);

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
                object? deserializedEvent = JsonSerializer.Deserialize(payloadElement, eventType, EventStorePayloadSerialization.Options) ?? throw new InvalidOperationException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Unable to project read model '{0}'. Payload for event type '{1}' could not be deserialized to '{2}'.",
                            typeof(TReadModel).Name,
                            eventTypeName,
                            eventType.Name));
                _ = applyMethod.Invoke(model, [deserializedEvent]);
            }
            else {
                object? deserializedEvent = JsonSerializer.Deserialize(eventElement, eventType, EventStorePayloadSerialization.Options) ?? throw new InvalidOperationException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Unable to project read model '{0}'. Event '{1}' could not be deserialized to '{2}'.",
                            typeof(TReadModel).Name,
                            eventTypeName,
                            eventType.Name));
                _ = applyMethod.Invoke(model, [deserializedEvent]);
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

    private string GetDomainName() =>
        _domainName ??= NamingConventionEngine.GetDomainName(GetType());

    private void FireProjectionChangeNotification() {
        if (Notifier is null) {
            Logger?.LogWarning(
                "IProjectionChangeNotifier is not registered for projection '{ProjectionType}'. " +
                "Cache invalidation will not occur. Register via AddEventStoreServer().",
                GetDomainName());
            return;
        }

        if (string.IsNullOrWhiteSpace(TenantId)) {
            return;
        }

        _ = NotifyAsync(GetDomainName(), TenantId);
    }

    private async Task NotifyAsync(string projectionType, string tenantId) {
        try {
            await Notifier!.NotifyProjectionChangedAsync(projectionType, tenantId).ConfigureAwait(false);
        }
        catch (Exception ex) {
            Logger?.LogWarning(
                ex,
                "Projection change notification failed for projection '{ProjectionType}', tenant '{TenantId}'. " +
                "Cache invalidation may be delayed.",
                projectionType,
                tenantId);
        }
    }
}
