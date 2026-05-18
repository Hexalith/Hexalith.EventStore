using System;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Typed result returned by metadata-aware event unprotect entry points. A readable outcome carries
/// the unprotected bytes plus the provider metadata that produced them; an unreadable outcome
/// carries only the safe reason category and metadata (never payload bytes or provider-private
/// detail).
/// </summary>
/// <param name="PayloadBytes">The unprotected payload bytes when <see cref="IsReadable"/>; otherwise <see langword="null"/>.</param>
/// <param name="SerializationFormat">The serialization format when <see cref="IsReadable"/>; otherwise <see langword="null"/>.</param>
/// <param name="Metadata">Provider-neutral protection metadata associated with the outcome.</param>
/// <param name="UnreadableReason">When the outcome is unreadable, the safe reason category; otherwise <see langword="null"/>.</param>
public sealed record PayloadUnprotectionOutcome(
    byte[]? PayloadBytes,
    string? SerializationFormat,
    EventStorePayloadProtectionMetadata Metadata,
    UnreadableProtectedDataReason? UnreadableReason) {
    /// <summary>Gets a value indicating whether the outcome carries readable bytes.</summary>
    public bool IsReadable => UnreadableReason is null;

    /// <summary>Gets a value indicating whether the outcome is unreadable.</summary>
    public bool IsUnreadable => UnreadableReason is not null;

    /// <summary>
    /// Creates a readable outcome. Validates that bytes and format are present.
    /// </summary>
    /// <param name="payloadBytes">The unprotected payload bytes.</param>
    /// <param name="serializationFormat">The serialization format of <paramref name="payloadBytes"/>.</param>
    /// <param name="metadata">Provider-neutral protection metadata.</param>
    /// <returns>A readable outcome record.</returns>
    public static PayloadUnprotectionOutcome Readable(
        byte[] payloadBytes,
        string serializationFormat,
        EventStorePayloadProtectionMetadata metadata) {
        ArgumentNullException.ThrowIfNull(payloadBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(serializationFormat);
        ArgumentNullException.ThrowIfNull(metadata);
        return new PayloadUnprotectionOutcome(payloadBytes, serializationFormat, metadata, UnreadableReason: null);
    }

    /// <summary>
    /// Creates an unreadable outcome. When <paramref name="metadata"/> is not supplied a
    /// <see cref="PayloadProtectionState.ProviderOpaque"/> record with the reason code is used.
    /// </summary>
    /// <param name="reason">The unreadable reason category.</param>
    /// <param name="metadata">Optional metadata recorded alongside the unreadable outcome.</param>
    /// <returns>An unreadable outcome record.</returns>
    public static PayloadUnprotectionOutcome Unreadable(
        UnreadableProtectedDataReason reason,
        EventStorePayloadProtectionMetadata? metadata = null)
        => new(
            PayloadBytes: null,
            SerializationFormat: null,
            Metadata: metadata ?? EventStorePayloadProtectionMetadata.ProviderOpaque(UnreadableProtectedDataReasonCodes.From(reason)),
            UnreadableReason: reason);

    /// <summary>
    /// Promotes an existing readable <see cref="PayloadProtectionResult"/> to a typed outcome.
    /// </summary>
    /// <param name="result">The protection result returned by the provider.</param>
    /// <returns>A readable outcome record.</returns>
    public static PayloadUnprotectionOutcome FromResult(PayloadProtectionResult result) {
        ArgumentNullException.ThrowIfNull(result);
        return Readable(result.PayloadBytes, result.SerializationFormat, result.Metadata);
    }
}
