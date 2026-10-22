namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Authenticated storage occurrence supplied by EventStore for one event or snapshot operation.
/// Implements Story 8.1 sections 7 and 9 under normative digest
/// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
/// This call-scoped value is never persisted inside a payload or protection metadata.
/// </summary>
/// <param name="RecordSequence">The aggregate-local event sequence or persisted snapshot sequence.</param>
/// <param name="PayloadKind">The exact event or snapshot payload kind.</param>
/// <param name="PayloadTypeId">The persisted event type or registered stable snapshot type identifier.</param>
public sealed record PayloadProtectionOccurrenceContext(
    ulong RecordSequence,
    PayloadProtectionPayloadKind PayloadKind,
    string PayloadTypeId) {
    /// <summary>Returns a bounded diagnostic name without storage occurrence details.</summary>
    /// <returns>The contract type name.</returns>
    public override string ToString() => nameof(PayloadProtectionOccurrenceContext);
}
