using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.FoldedSnapshotFixture;

/// <summary>A canonical order fixture event.</summary>
/// <param name="Operation">The persisted operation.</param>
/// <param name="Key">The item key.</param>
/// <param name="Value">The item value or name.</param>
public sealed record FoldedOrderChanged(string Operation, string Key, string Value) : IEventPayload;
