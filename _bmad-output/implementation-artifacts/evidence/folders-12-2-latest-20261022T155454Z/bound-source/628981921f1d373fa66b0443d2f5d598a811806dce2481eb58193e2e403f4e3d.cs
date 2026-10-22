namespace Hexalith.EventStore.Client.Projections;

/// <summary>
/// Internal signal that a concurrent same-identity batch advanced the shared marker past the prepared state
/// while this run was preparing it. It is caught by <see cref="ReadModelBatchProtocol"/> and resolved through
/// bounded same-identity reconciliation to a structured result; it is never surfaced to callers. A concurrent
/// or overlapping delivery of the same batch identity is an expected recovery outcome, not a programming
/// error, so it must not collapse into a thrown <see cref="System.InvalidOperationException"/>.
/// </summary>
internal sealed class ReadModelBatchRaceException : Exception {
    /// <summary>Initializes a new instance of the <see cref="ReadModelBatchRaceException"/> class.</summary>
    public ReadModelBatchRaceException()
        : base("Concurrent same-identity batch prepare race; resolved through reconciliation.") {
    }
}
