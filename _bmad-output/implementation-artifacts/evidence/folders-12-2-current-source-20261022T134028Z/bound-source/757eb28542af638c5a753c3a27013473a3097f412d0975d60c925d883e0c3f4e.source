using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Event whose live properties differ from the admitted custom JSON.</summary>
public sealed class ParityEvent : IEventPayload
{
    /// <summary>Gets or sets the live amount.</summary>
    public int Amount { get; set; }

    /// <summary>Gets or sets a value intentionally absent from the canonical event.</summary>
    public int Defaulted { get; set; }

    /// <summary>Gets or sets an ignored live value.</summary>
    [JsonIgnore]
    public int Ignored { get; set; }
}
