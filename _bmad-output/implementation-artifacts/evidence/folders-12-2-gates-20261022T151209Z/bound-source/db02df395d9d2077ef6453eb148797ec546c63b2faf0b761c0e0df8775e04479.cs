namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Represents transformed payload bytes after optional protection or unprotection together with
/// the protection metadata returned by the provider. <see cref="Metadata"/> and
/// <see cref="PayloadBytes"/> stay coupled across persist, publish, replay, and rebuild paths.
/// </summary>
/// <param name="PayloadBytes">The transformed payload bytes.</param>
/// <param name="SerializationFormat">The serialization format associated with the transformed payload.</param>
/// <param name="Metadata">Provider-neutral protection metadata describing how <paramref name="PayloadBytes"/> was produced.</param>
public sealed record PayloadProtectionResult(
    byte[] PayloadBytes,
    string SerializationFormat,
    EventStorePayloadProtectionMetadata Metadata) {
    /// <summary>
    /// Backwards-compatible constructor: callers that have not migrated to typed metadata still
    /// produce a well-formed result by emitting <see cref="PayloadProtectionState.Unprotected"/>
    /// metadata at the current schema version.
    /// </summary>
    /// <param name="payloadBytes">The transformed payload bytes.</param>
    /// <param name="serializationFormat">The serialization format associated with the transformed payload.</param>
    public PayloadProtectionResult(byte[] payloadBytes, string serializationFormat)
        : this(payloadBytes, serializationFormat, EventStorePayloadProtectionMetadata.Unprotected()) {
    }
}
