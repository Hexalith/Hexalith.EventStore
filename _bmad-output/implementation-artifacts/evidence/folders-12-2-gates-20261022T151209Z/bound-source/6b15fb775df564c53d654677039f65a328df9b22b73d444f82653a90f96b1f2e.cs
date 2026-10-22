using System.Text.Json.Serialization;

using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Client.Tests.Aggregates;

/// <summary>Exercises cancellation triggered inside a domain payload converter.</summary>
[JsonConverter(typeof(CancellationReplayEventConverter))]
internal sealed class CancellationReplayEvent : IEventPayload;
