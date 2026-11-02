using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>An ordinary admission rejection deliberately absent from refusal-only serializer profiles.</summary>
public sealed record TerminalRefusalOrdinaryRejected(string Reason) : IRejectionEvent;
