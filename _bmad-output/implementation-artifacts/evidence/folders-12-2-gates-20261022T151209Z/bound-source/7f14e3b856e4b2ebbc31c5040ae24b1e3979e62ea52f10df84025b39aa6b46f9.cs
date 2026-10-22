using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Binds a protected write to the exact reserved key record that may be activated after persistence.
/// Implements Story 8.1 sections 9 and 10.3 under normative digest
/// <c>de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e</c>.
/// This context is call-scoped and must never be stored in payload bytes or protection metadata.
/// </summary>
/// <param name="Identity">The aggregate identity bound to the reservation.</param>
/// <param name="Occurrence">The exact event or snapshot storage occurrence.</param>
/// <param name="OperationId">The canonical uppercase ULID identifying the create operation.</param>
/// <param name="KeyReference">The canonical uppercase ULID identifying the durable DEK record.</param>
/// <param name="DekVersion">The positive DEK version.</param>
/// <param name="LifecycleEpoch">The strongly observed lifecycle epoch bound to the reservation.</param>
public sealed record PayloadProtectionCompletionContext(
    AggregateIdentity Identity,
    PayloadProtectionOccurrenceContext Occurrence,
    string OperationId,
    string KeyReference,
    uint DekVersion,
    ulong LifecycleEpoch) {
    /// <summary>Returns a bounded diagnostic name without identity, occurrence, operation, or key details.</summary>
    /// <returns>The contract type name.</returns>
    public override string ToString() => nameof(PayloadProtectionCompletionContext);
}
