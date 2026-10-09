// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 12 and 13.
using Hexalith.EventStore.Contracts.Identity;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Carries one stored event exactly as persisted, before any compatibility decision (normative section 12.1).
/// </summary>
/// <param name="Identity">The authenticated aggregate identity of the storage scope.</param>
/// <param name="SequenceNumber">The aggregate-local event sequence from the storage position.</param>
/// <param name="EventTypeName">The persisted event type name.</param>
/// <param name="PayloadBytes">The stored payload bytes. They are caller-owned and never mutated by the router.</param>
/// <param name="SerializationFormat">The stored serialization format.</param>
/// <param name="ProtectionCarrier">
/// The raw stored <c>eventstore.protection</c> carrier text, or <see langword="null"/> when the record has none.
/// </param>
internal sealed record CompatibilityEventRecord(
    AggregateIdentity Identity,
    ulong SequenceNumber,
    string EventTypeName,
    byte[] PayloadBytes,
    string SerializationFormat,
    string? ProtectionCarrier)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(CompatibilityEventRecord);
}
