namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Provider-neutral classification for protected payload or snapshot state that EventStore cannot
/// safely return to a consumer. Stories 22.7c and 22.7d own key lifecycle workflow, restored-backup
/// governance, and broader operational redaction; this enum lets EventStore react deterministically
/// when the registered protection service or metadata validator reports unreadable data.
/// </summary>
public enum UnreadableProtectedDataReason {
    /// <summary>
    /// The protection metadata identifies a scheme but the provider reports the required key is not
    /// available for the requested operation. Typically operator-actionable (register the key).
    /// </summary>
    MissingKey = 0,

    /// <summary>
    /// The protection key has been explicitly invalidated, deleted, or revoked. Data is permanently
    /// unreadable; governance / restored-backup remediation belongs to Story 22.7c.
    /// </summary>
    KeyInvalidatedOrDeleted = 1,

    /// <summary>
    /// The protection provider is temporarily unavailable (transient infrastructure failure). The
    /// caller may retry with backoff.
    /// </summary>
    ProviderUnavailable = 2,

    /// <summary>
    /// The protection provider refused the operation (authorization, policy, or quota). Retry is
    /// only valid after the policy state changes; the caller must not parse provider exception text.
    /// </summary>
    ProviderDenied = 3,

    /// <summary>
    /// The provider observed an inconsistency between the stored bytes and the metadata returned at
    /// protect time (for example, restored-backup provenance produced a record the current provider
    /// cannot verify). Restored-backup remediation belongs to Story 22.7c.
    /// </summary>
    ConsistencyMismatch = 4,

    /// <summary>
    /// The persisted protection metadata could not be parsed (JSON syntax, schema violation, or
    /// secret-shaped fields rejected by the carrier). Already mapped to
    /// <see cref="PayloadProtectionState.ProviderOpaque"/> by Story 22.7a; surfaced here as an
    /// explicit unreadable category for callers.
    /// </summary>
    MalformedMetadata = 5,

    /// <summary>
    /// The persisted protection metadata declares a schema version newer than this EventStore
    /// release supports. Typically resolved by upgrading EventStore.
    /// </summary>
    UnknownMetadataVersion = 6,

    /// <summary>
    /// EventStore encountered <see cref="PayloadProtectionState.ProviderOpaque"/> metadata on a
    /// surface that requires plaintext (for example, command-time rehydration or DAPR publication).
    /// EventStore must not attempt to interpret opaque records outside the registered protection
    /// service.
    /// </summary>
    ProviderOpaqueUnsupportedOperation = 7,

    /// <summary>
    /// The stored bytes and metadata state disagree in a way EventStore can prove (for example, the
    /// metadata claims <see cref="PayloadProtectionState.Unprotected"/> but the provider reports the
    /// bytes are cipher-shaped, or vice versa).
    /// </summary>
    BytesMetadataMismatch = 8,
}
