namespace Hexalith.EventStore.Contracts.Security;

/// <summary>
/// Story 22.7c — the canonical decision-result status consumed by every EventStore-owned read,
/// publish, replay, rebuild, snapshot-load, backup-admission, admin, CLI, and MCP surface. The
/// status either says "Readable" or carries the safe reason category callers must surface.
/// </summary>
public enum ProtectedDataReadabilityStatus {
    /// <summary>The protected data is readable; safe to forward to domain/publish/read consumers.</summary>
    Readable = 0,

    /// <summary>The data is unreadable. Refer to the underlying 22.7b reason category for the precise cause.</summary>
    Unreadable = 1,

    /// <summary>The protection metadata is malformed and cannot be parsed.</summary>
    MalformedMetadata = 2,

    /// <summary>The protection metadata version is unknown to this EventStore release.</summary>
    UnknownVersion = 3,

    /// <summary>The data is provider-opaque; EventStore cannot interpret it without the registered provider.</summary>
    ProviderOpaque = 4,

    /// <summary>The protection provider is temporarily unavailable. Caller may retry with backoff.</summary>
    ProviderUnavailable = 5,

    /// <summary>
    /// A restored-backup admission must hold this row until additional evidence confirms safety.
    /// Behaves as a transient unreadable state at runtime.
    /// </summary>
    DeferredValidation = 6,

    /// <summary>
    /// A restored backup conflicts with an irreversible crypto-shredding decision. Affected data
    /// must not be served.
    /// </summary>
    RestoreConflict = 7,

    /// <summary>
    /// The row has been placed in quarantine pending operator review. Affected data must not be
    /// served.
    /// </summary>
    QuarantineRequired = 8,

    /// <summary>A pending crypto-shredding workflow or restore conflict requires explicit operator action.</summary>
    OperatorDecisionRequired = 9,
}
