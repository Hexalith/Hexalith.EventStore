namespace Hexalith.EventStore.Client.Projections;

/// <summary>Signals that aggregate preparation admission or its canonical snapshot lost an optimistic race.</summary>
internal sealed class ReadModelBatchSnapshotConflictException : Exception;
