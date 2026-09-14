namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Defines safe low-cardinality operation results.
/// </summary>
internal enum PayloadProtectionDiagnosticResult {
    /// <summary>The operation completed.</summary>
    Success = 1,

    /// <summary>The operation rejected malformed input.</summary>
    Malformed = 2,

    /// <summary>The operation could not authenticate input.</summary>
    AuthenticationFailed = 3,

    /// <summary>The operation was cancelled.</summary>
    Cancelled = 4,

    /// <summary>The key source was unavailable.</summary>
    Unavailable = 5,
}
