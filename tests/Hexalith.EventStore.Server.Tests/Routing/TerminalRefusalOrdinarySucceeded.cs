using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>An ordinary success event deliberately absent from refusal-only serializer profiles.</summary>
public sealed record TerminalRefusalOrdinarySucceeded(int Value) : IEventPayload;
