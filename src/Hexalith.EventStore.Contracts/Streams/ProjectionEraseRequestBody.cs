namespace Hexalith.EventStore.Contracts.Streams;

/// <summary>
/// EventStore-side projection erase request body. Carries only logical identifiers — the owning
/// domain, aggregate, the logical read-model slot identifiers, and a stable operation identifier.
/// Store names, physical keys, and ETags are resolved canonically by the platform and are
/// structurally excluded from this contract.
/// </summary>
/// <param name="Domain">The owning domain.</param>
/// <param name="AggregateId">The owning aggregate identifier.</param>
/// <param name="Slots">The logical, aggregate-owned read-model slot identifiers to erase.</param>
/// <param name="OperationId">The stable erase operation identifier (idempotency/resume key).</param>
public sealed record ProjectionEraseRequestBody(
    string Domain,
    string AggregateId,
    IReadOnlyList<string> Slots,
    string OperationId);
