// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 8.4, 12, and 15.
using Hexalith.EventStore.Contracts.Security;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Represents one routed event: complete readable bytes or one bounded unreadable reason, never both.
/// </summary>
/// <param name="SequenceNumber">The aggregate-local event sequence the decision applies to.</param>
/// <param name="Route">The route that produced the decision, or <see cref="CompatibilityReadRoute.Rejected"/>.</param>
/// <param name="PayloadBytes">
/// The readable payload, or <see langword="null"/>. Pass-through routes return the caller's unchanged stored buffer;
/// protected routes return a new buffer owned by the caller.
/// </param>
/// <param name="SerializationFormat">The readable format, or <see langword="null"/>.</param>
/// <param name="Metadata">The readable metadata, or <see langword="null"/> for an unreadable result.</param>
/// <param name="UnreadableReason">The bounded unreadable reason, or <see langword="null"/>.</param>
internal sealed record CompatibilityEventReadResult(
    ulong SequenceNumber,
    CompatibilityReadRoute Route,
    byte[]? PayloadBytes,
    string? SerializationFormat,
    EventStorePayloadProtectionMetadata? Metadata,
    UnreadableProtectedDataReason? UnreadableReason)
{
    /// <summary>Gets a value indicating whether complete readable bytes are available.</summary>
    internal bool IsReadable => UnreadableReason is null && PayloadBytes is not null;

    /// <summary>Gets a value indicating whether the readable bytes are router-owned decrypted plaintext.</summary>
    internal bool OwnsPayload => Route is CompatibilityReadRoute.RegisteredV1 or CompatibilityReadRoute.SharedV2;

    /// <summary>Creates a readable result.</summary>
    internal static CompatibilityEventReadResult Readable(
        ulong sequenceNumber,
        CompatibilityReadRoute route,
        byte[] payloadBytes,
        string serializationFormat,
        EventStorePayloadProtectionMetadata metadata)
        => new(sequenceNumber, route, payloadBytes, serializationFormat, metadata, null);

    /// <summary>Creates an unreadable result that carries no payload, format, or metadata.</summary>
    internal static CompatibilityEventReadResult Unreadable(
        ulong sequenceNumber,
        CompatibilityReadRoute route,
        UnreadableProtectedDataReason reason)
        => new(sequenceNumber, route, null, null, null, reason);

    /// <inheritdoc/>
    public override string ToString() => nameof(CompatibilityEventReadResult);
}
