using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.TestContracts;

/// <summary>A current event used to test discovery of historical chains.</summary>
/// <param name="Value">The value carried by the event.</param>
[EventPayloadVersion(3)]
public sealed record ChainedEvent(int Value) : IEventPayload;
