using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Throws an argument error while deserializing a stored event.</summary>
internal sealed class ConstructorArgumentEvent : IEventPayload
{
    /// <summary>Initializes the event and rejects its value.</summary>
    /// <param name="value">The stored value.</param>
    [JsonConstructor]
    public ConstructorArgumentEvent(string value) => throw new ArgumentException("secret", nameof(value));

    /// <summary>Gets the event value.</summary>
    public string Value { get; } = "";
}
