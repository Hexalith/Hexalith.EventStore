#if SELECTED
using Hexalith.EventStore.Contracts.Events;

namespace PublishedConsumer;

/// <summary>The current event schema admitted by the registered evolution path.</summary>
/// <param name="Value">The current-schema increment amount.</param>
[EventPayloadVersion(2)]
public sealed record EvolvedIncremented(int Value) : IEventPayload;
#endif
