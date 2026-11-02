using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>A typed rejection used only by terminal refusal boundary tests.</summary>
public sealed record TerminalRefusalRejected(string Reason) : IRejectionEvent;
