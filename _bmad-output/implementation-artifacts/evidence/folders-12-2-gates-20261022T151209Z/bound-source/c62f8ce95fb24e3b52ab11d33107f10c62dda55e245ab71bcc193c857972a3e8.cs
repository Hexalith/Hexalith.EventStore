namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Stable kebab-case reason codes for <see cref="UnreadableProtectedDataReason"/>. The codes are
/// the only public/operator-visible representation of the taxonomy; the enum names themselves are
/// not part of the wire contract.
/// </summary>
public static class UnreadableProtectedDataReasonCodes {
    /// <summary>Stable code for <see cref="UnreadableProtectedDataReason.MissingKey"/>.</summary>
    public const string MissingKey = "missing-key";

    /// <summary>Stable code for <see cref="UnreadableProtectedDataReason.KeyInvalidatedOrDeleted"/>.</summary>
    public const string KeyInvalidatedOrDeleted = "key-invalidated";

    /// <summary>Stable code for <see cref="UnreadableProtectedDataReason.ProviderUnavailable"/>.</summary>
    public const string ProviderUnavailable = "provider-unavailable";

    /// <summary>Stable code for <see cref="UnreadableProtectedDataReason.ProviderDenied"/>.</summary>
    public const string ProviderDenied = "provider-denied";

    /// <summary>Stable code for <see cref="UnreadableProtectedDataReason.ConsistencyMismatch"/>.</summary>
    public const string ConsistencyMismatch = "consistency-mismatch";

    /// <summary>Stable code for <see cref="UnreadableProtectedDataReason.MalformedMetadata"/>.</summary>
    public const string MalformedMetadata = "malformed-metadata";

    /// <summary>Stable code for <see cref="UnreadableProtectedDataReason.UnknownMetadataVersion"/>.</summary>
    public const string UnknownMetadataVersion = "unknown-metadata-version";

    /// <summary>Stable code for <see cref="UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation"/>.</summary>
    public const string ProviderOpaqueUnsupportedOperation = "provider-opaque-unsupported";

    /// <summary>Stable code for <see cref="UnreadableProtectedDataReason.BytesMetadataMismatch"/>.</summary>
    public const string BytesMetadataMismatch = "bytes-metadata-mismatch";

    /// <summary>Maps an <see cref="UnreadableProtectedDataReason"/> to its stable wire code.</summary>
    /// <param name="reason">The reason category.</param>
    /// <returns>The kebab-case wire code.</returns>
    /// <exception cref="ArgumentOutOfRangeException">When the reason is outside the defined enum range.</exception>
    public static string From(UnreadableProtectedDataReason reason) => reason switch {
        UnreadableProtectedDataReason.MissingKey => MissingKey,
        UnreadableProtectedDataReason.KeyInvalidatedOrDeleted => KeyInvalidatedOrDeleted,
        UnreadableProtectedDataReason.ProviderUnavailable => ProviderUnavailable,
        UnreadableProtectedDataReason.ProviderDenied => ProviderDenied,
        UnreadableProtectedDataReason.ConsistencyMismatch => ConsistencyMismatch,
        UnreadableProtectedDataReason.MalformedMetadata => MalformedMetadata,
        UnreadableProtectedDataReason.UnknownMetadataVersion => UnknownMetadataVersion,
        UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation => ProviderOpaqueUnsupportedOperation,
        UnreadableProtectedDataReason.BytesMetadataMismatch => BytesMetadataMismatch,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown UnreadableProtectedDataReason value."),
    };

    /// <summary>
    /// Returns <see langword="true"/> when the reason category may resolve on its own after a retry
    /// with backoff (ST0: only <see cref="UnreadableProtectedDataReason.ProviderUnavailable"/> is
    /// retryable by default).
    /// </summary>
    /// <param name="reason">The reason category.</param>
    /// <returns><see langword="true"/> when transient.</returns>
    public static bool IsRetryable(UnreadableProtectedDataReason reason) => reason switch {
        UnreadableProtectedDataReason.ProviderUnavailable => true,
        UnreadableProtectedDataReason.MissingKey
        or UnreadableProtectedDataReason.KeyInvalidatedOrDeleted
        or UnreadableProtectedDataReason.ProviderDenied
        or UnreadableProtectedDataReason.ConsistencyMismatch
        or UnreadableProtectedDataReason.MalformedMetadata
        or UnreadableProtectedDataReason.UnknownMetadataVersion
        or UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation
        or UnreadableProtectedDataReason.BytesMetadataMismatch => false,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown UnreadableProtectedDataReason value."),
    };

    /// <summary>
    /// Returns <see langword="true"/> when the reason category is permanent (irrecoverable without
    /// operator action that changes the protection metadata, the storage record, or EventStore
    /// itself). Permanent reasons must never advance projection rebuild checkpoints.
    /// </summary>
    /// <param name="reason">The reason category.</param>
    /// <returns><see langword="true"/> when permanent.</returns>
    public static bool IsPermanent(UnreadableProtectedDataReason reason) => reason switch {
        UnreadableProtectedDataReason.KeyInvalidatedOrDeleted => true,
        UnreadableProtectedDataReason.ConsistencyMismatch => true,
        UnreadableProtectedDataReason.MalformedMetadata => true,
        UnreadableProtectedDataReason.ProviderOpaqueUnsupportedOperation => true,
        UnreadableProtectedDataReason.BytesMetadataMismatch => true,
        UnreadableProtectedDataReason.MissingKey
        or UnreadableProtectedDataReason.ProviderUnavailable
        or UnreadableProtectedDataReason.ProviderDenied
        or UnreadableProtectedDataReason.UnknownMetadataVersion => false,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown UnreadableProtectedDataReason value."),
    };
}
