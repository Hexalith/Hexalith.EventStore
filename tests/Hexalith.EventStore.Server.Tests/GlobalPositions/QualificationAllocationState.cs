namespace Hexalith.EventStore.Server.Tests.GlobalPositions;

/// <summary>A single fixture state record preserving admission and its reserved ceiling.</summary>
/// <param name="Shard">The exact admitted shard identity.</param>
/// <param name="Generation">The positive generation, which cannot silently change.</param>
/// <param name="LastReservedCounter">The last allocated counter, including reservation gaps.</param>
internal sealed record QualificationAllocationState(
    QualificationShard Shard,
    long Generation,
    long LastReservedCounter);
