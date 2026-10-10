using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Tests.Events;

/// <summary>An invalid declaration used to prove startup refusal.</summary>
[EventPayloadVersion(1025)]
public sealed record InvalidHighVersionTestEvent : IEventPayload;
