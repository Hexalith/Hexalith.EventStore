namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// The kind of a single read-model batch operation.
/// </summary>
public enum ReadModelBatchOperationKind {
    /// <summary>Persist a serialized value at the logical key.</summary>
    Write,

    /// <summary>Remove the value at the logical key.</summary>
    Delete,
}
