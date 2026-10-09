namespace Hexalith.EventStore.Server.Events;
/// <summary>Discriminates only unsigned local range preparation, with no Current or dispatch meaning.</summary>
internal enum DaprLogicalCheckpointRangeKind : byte
{
    /// <summary>Plans the exact current-head count-zero shape without issuing completion.</summary>
    CurrentZero = 0,
    /// <summary>Plans a bounded checkpoint-plus-one tail without reading or applying it.</summary>
    Tail = 1,
}
