using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Throws an invalid operation while deserializing a stored event.</summary>
internal sealed class ConstructorInvalidOperationEvent : IEventPayload
{
    /// <summary>Initializes the event and rejects its value.</summary>
    /// <param name="value">The stored value.</param>
    [JsonConstructor]
    public ConstructorInvalidOperationEvent(string value) => throw new InvalidOperationException("secret");

    /// <summary>Gets the event value.</summary>
    public string Value { get; } = "";
}
