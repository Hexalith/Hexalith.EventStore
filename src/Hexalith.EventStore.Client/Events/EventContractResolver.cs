using System.Collections.Concurrent;

using Hexalith.EventStore.Client.Conventions;
using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Events;

/// <summary>
/// Resolves and validates event contract metadata from <see cref="IEventContract"/> implementations.
/// Results are cached per type using a thread-safe <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public static class EventContractResolver {
    private static readonly ConcurrentDictionary<Type, EventContractMetadata> _cache = new();

    /// <summary>
    /// Resolves and validates the event contract metadata for the specified event type.
    /// Reads static abstract members, validates all fields against kebab-case rules,
    /// and caches the result.
    /// </summary>
    /// <typeparam name="TEvent">The event type implementing <see cref="IEventContract"/>.</typeparam>
    /// <returns>The validated <see cref="EventContractMetadata"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a static member returns null.</exception>
    /// <exception cref="ArgumentException">Thrown when a static member value is invalid.</exception>
    public static EventContractMetadata Resolve<TEvent>()
        where TEvent : IEventContract => _cache.GetOrAdd(typeof(TEvent), static _ => {
            string eventType = TEvent.EventType;
            string domain = TEvent.Domain;

            NamingConventionEngine.ValidateKebabCase(eventType, "EventType");
            NamingConventionEngine.ValidateKebabCase(domain, "Domain");
            RejectColon(eventType, "EventType");
            RejectColon(domain, "Domain");

            return new EventContractMetadata(eventType, domain);
        });

    /// <summary>
    /// Clears the resolver cache. Intended for test isolation.
    /// </summary>
    internal static void ClearCache() => _cache.Clear();

    private static void RejectColon(string value, string parameterName) {
        if (value.Contains(':')) {
            throw new ArgumentException(
                $"{parameterName} '{value}' cannot contain colons (reserved as actor ID separator).",
                parameterName);
        }
    }
}
