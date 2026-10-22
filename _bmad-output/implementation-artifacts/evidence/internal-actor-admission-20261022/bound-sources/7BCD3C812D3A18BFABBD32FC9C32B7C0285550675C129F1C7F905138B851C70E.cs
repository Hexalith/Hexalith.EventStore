namespace Hexalith.EventStore.Client.Projections;

/// <summary>Proves that a sparse generation was selected, rather than abandoned.</summary>
internal sealed record SharedProjectionCopyOnWriteCommit(long Generation);
