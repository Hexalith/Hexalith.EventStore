using Hexalith.EventStore.Contracts.Events;

namespace Hexalith.EventStore.FoldedSnapshotFixture;

/// <summary>A persisted order rejection that leaves business state unchanged.</summary>
/// <param name="Reason">The bounded fixture reason.</param>
public sealed record FoldedOrderRejected(string Reason) : IRejectionEvent;
