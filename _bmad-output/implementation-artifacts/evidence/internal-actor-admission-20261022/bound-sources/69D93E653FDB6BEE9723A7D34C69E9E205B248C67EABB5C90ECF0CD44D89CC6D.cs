namespace Hexalith.EventStore.Contracts.Processes;

/// <summary>A deterministic command emitted by a pure process translator.</summary>
/// <param name="Kind">A registered versioned effect kind.</param>
/// <param name="TargetDomain">The target domain.</param>
/// <param name="TargetAggregate">The target aggregate.</param>
/// <param name="Ordinal">A stable catalog ordinal, independent of traversal order.</param>
/// <param name="CommandType">The target command type.</param>
/// <param name="CommandPayload">The serialized command bytes. The runner copies these before submitting.</param>
public sealed record ProcessEffect(
    string Kind,
    string TargetDomain,
    string TargetAggregate,
    long Ordinal,
    string CommandType,
    byte[] CommandPayload);
