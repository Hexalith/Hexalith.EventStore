namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Signals a transport-style batch state-store failure (for example an ambiguous transaction outcome) so
/// the protocol engine reconciles durable state rather than throwing to the caller. Analogous to the DAPR
/// exceptions the real adapter surfaces.
/// </summary>
internal sealed class ReadModelBatchStoreException : Exception {
    /// <summary>Initializes a new instance of the <see cref="ReadModelBatchStoreException"/> class.</summary>
    public ReadModelBatchStoreException() {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    /// <param name="message">The message.</param>
    public ReadModelBatchStoreException(string message)
        : base(message) {
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ReadModelBatchStoreException(string message, Exception innerException)
        : base(message, innerException) {
    }
}
