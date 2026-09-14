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
