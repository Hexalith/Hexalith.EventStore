using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Performs fixed-profile AES-256-GCM operations from normative sections 8.1 and 15.1.
/// </summary>
internal static class PayloadCryptography {
    /// <summary>
    /// Encrypts one plaintext under the nonce derived from its field ordinal.
    /// </summary>
    internal static PayloadProtectionEnvelope Encrypt(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> aad,
        ReadOnlySpan<byte> dek,
        string keyReference,
        uint dekVersion,
        uint fieldOrdinal) {
        if (!AesGcm.IsSupported
            || dek.Length != 32
            || plaintext.Length is 0 or > PayloadProtectionLimits.CiphertextBytes
            || fieldOrdinal >= PayloadProtectionLimits.ProtectedPaths) {
            throw new PayloadProtectionFormatException();
        }

        byte[] nonce = new byte[PayloadProtectionLimits.NonceBytes];
        BinaryPrimitives.WriteUInt64BigEndian(nonce.AsSpan(4), fieldOrdinal);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[PayloadProtectionLimits.TagBytes];
        using var cipher = new AesGcm(dek, PayloadProtectionLimits.TagBytes);
        cipher.Encrypt(nonce, plaintext, ciphertext, tag, aad);
        return new PayloadProtectionEnvelope(keyReference, dekVersion, fieldOrdinal, nonce, ciphertext, tag);
    }

    /// <summary>
    /// Authenticates and decrypts one envelope, returning an engine-owned mutable destination.
    /// </summary>
    internal static byte[] Decrypt(
        PayloadProtectionEnvelope envelope,
        ReadOnlySpan<byte> aad,
        ReadOnlySpan<byte> dek,
        ISensitiveBufferObserver? observer = null) {
        ArgumentNullException.ThrowIfNull(envelope);
        if (!AesGcm.IsSupported || dek.Length != 32) {
            throw new PayloadProtectionFormatException();
        }

        byte[] plaintext = new byte[envelope.Ciphertext.Length];
        try {
            using var cipher = new AesGcm(dek, PayloadProtectionLimits.TagBytes);
            cipher.Decrypt(envelope.Nonce, envelope.Ciphertext, envelope.Tag, plaintext, aad);
            return plaintext;
        }
        catch (AuthenticationTagMismatchException) {
            CryptographicOperations.ZeroMemory(plaintext);
            observer?.BufferCleared(SensitiveBufferKind.DecryptedPlaintext, plaintext);
            throw new PayloadProtectionAuthenticationException();
        }
        catch (CryptographicException) {
            CryptographicOperations.ZeroMemory(plaintext);
            observer?.BufferCleared(SensitiveBufferKind.DecryptedPlaintext, plaintext);
            throw new PayloadProtectionAuthenticationException();
        }
    }
}
