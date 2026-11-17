namespace Hexalith.EventStore.FoldedSnapshotFixture;

/// <summary>A fixture order mutation; it grants no real module qualification.</summary>
/// <param name="Operation">The order operation.</param>
/// <param name="Key">The item key.</param>
/// <param name="Value">The item value or name.</param>
public sealed record FoldedOrderCommand(string Operation, string Key, string Value);
