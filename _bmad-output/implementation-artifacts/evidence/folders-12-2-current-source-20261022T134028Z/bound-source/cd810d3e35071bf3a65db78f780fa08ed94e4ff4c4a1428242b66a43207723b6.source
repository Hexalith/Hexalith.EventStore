using System.Text.Json.Serialization;

namespace Hexalith.EventStore.Client.Tests.Handlers;

/// <summary>Invokes a controlled payload-converter mutation during command reconstruction.</summary>
/// <param name="Amount">The amount read from the admitted private payload.</param>
[JsonConverter(typeof(CommandReplayDeserializationEventConverter))]
internal sealed record CommandReplayDeserializationEvent(int Amount);
