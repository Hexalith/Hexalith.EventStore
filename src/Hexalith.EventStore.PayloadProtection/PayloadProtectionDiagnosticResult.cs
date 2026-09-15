// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 10, 14, and 15.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Defines safe low-cardinality operation results.
/// </summary>
internal enum PayloadProtectionDiagnosticResult
{
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

    /// <summary>The requested key was missing.</summary>
    MissingKey = 6,

    /// <summary>The supplied key material or protected record was inconsistent.</summary>
    ConsistencyMismatch = 7,

    /// <summary>The cryptographic platform or operation failed safely.</summary>
    CryptographicFailure = 8,
}
