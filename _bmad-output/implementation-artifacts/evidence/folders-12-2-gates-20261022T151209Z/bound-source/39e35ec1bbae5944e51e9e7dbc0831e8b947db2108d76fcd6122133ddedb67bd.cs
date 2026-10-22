namespace Hexalith.EventStore.Server.Projections;

/// <summary>One optimistic-concurrency shard of payload-free projection activations.</summary>
/// <param name="Items">The activation items in this shard.</param>
public sealed record ProjectionActivationLedger(IReadOnlyList<ProjectionActivationWorkItem> Items);
