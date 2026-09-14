namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Identifies an engine-owned buffer category for the bounded zeroing test seam.
/// </summary>
internal enum SensitiveBufferKind {
    /// <summary>A transferred or unwrapped data-encryption key.</summary>
    DataEncryptionKey = 1,

    /// <summary>A selected plaintext JSON value.</summary>
    SelectedPlaintext = 2,

    /// <summary>An authenticated decrypted plaintext value.</summary>
    DecryptedPlaintext = 3,
}
