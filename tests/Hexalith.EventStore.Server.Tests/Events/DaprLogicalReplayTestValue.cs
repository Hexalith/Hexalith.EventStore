namespace Hexalith.EventStore.Server.Tests.Events;

/// <summary>Is the explicitly allow-listed current event type for local logical source tests.</summary>
internal sealed class DaprLogicalReplayTestValue
{
    /// <summary>Gets the semantic increment emitted by the exact current deserializer.</summary>
    internal int Delta { get; init; } = 1;
}
