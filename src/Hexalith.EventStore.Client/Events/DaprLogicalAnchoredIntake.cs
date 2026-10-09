namespace Hexalith.EventStore.Client.Events;
/// <summary>Supplies expected anchored proof fields; actual source/origin and durable predecessor authority remain owner checks.</summary>
/// <param name = "Trust">The explicitly selected separate current prefix trust.</param>
/// <param name = "Selection">The expected strict privately captured selection fields.</param>
/// <param name = "SelectionHash">The exact private selection image digest.</param>
internal sealed record DaprLogicalAnchoredIntake(DaprLogicalAnchoredClaimTrust Trust, DaprLogicalReplayAnchorSelection Selection, ReadOnlyMemory<byte> SelectionHash);
