// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 8, 14, and 15.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Identifies an engine-owned buffer category for the bounded zeroing test seam.
/// </summary>
internal enum SensitiveBufferKind
{
    /// <summary>A transferred or unwrapped data-encryption key.</summary>
    DataEncryptionKey = 1,

    /// <summary>A selected plaintext JSON value.</summary>
    SelectedPlaintext = 2,

    /// <summary>An authenticated decrypted plaintext value.</summary>
    DecryptedPlaintext = 3,

    /// <summary>A stable engine-owned copy of caller input.</summary>
    InputSnapshot = 4,

    /// <summary>An authenticated-data staging buffer.</summary>
    AuthenticatedData = 5,

    /// <summary>A mutable protected envelope or wrapper staging buffer.</summary>
    ProtectedOutput = 6,

    /// <summary>An output buffer abandoned because validation or cancellation won.</summary>
    AbandonedOutput = 7,
}
