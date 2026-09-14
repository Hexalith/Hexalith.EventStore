using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Performs fixed-profile AES-256-GCM operations from normative sections 8.1 and 15.1.
/// </summary>
internal static class PayloadCryptography
{
    /// <summary>
    /// Encrypts one plaintext under the nonce derived from its field ordinal.
    /// </summary>
    internal static PayloadProtectionEnvelope Encrypt(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> aad,
        ReadOnlySpan<byte> dek,
        string keyReference,
        uint dekVersion,
        uint fieldOrdinal)
    {
        if (dek.Length != 32
            || plaintext.Length is 0 or > PayloadProtectionLimits.CiphertextBytes
            || !CanonicalUlid.IsValid(keyReference)
            || dekVersion == 0
            || fieldOrdinal >= PayloadProtectionLimits.ProtectedPaths)
        {
            throw new PayloadProtectionFormatException();
        }

        EnsurePlatformSupport();
        byte[]? nonce = null;
        byte[]? ciphertext = null;
        byte[]? tag = null;
        try
        {
            nonce = new byte[PayloadProtectionLimits.NonceBytes];
            ciphertext = new byte[plaintext.Length];
            tag = new byte[PayloadProtectionLimits.TagBytes];
            BinaryPrimitives.WriteUInt64BigEndian(nonce.AsSpan(4), fieldOrdinal);
            EncryptAesGcm(dek, nonce, plaintext, ciphertext, tag, aad);
            var envelope = new PayloadProtectionEnvelope(keyReference, dekVersion, fieldOrdinal, nonce, ciphertext, tag);
            nonce = null;
            ciphertext = null;
            tag = null;
            return envelope;
        }
        catch (CryptographicException)
        {
            throw new PayloadProtectionCryptographicException();
        }
        finally
        {
            Clear(nonce);
            Clear(ciphertext);
            Clear(tag);
        }
    }

    /// <summary>
    /// Executes the fixed AES-256-GCM primitive used by the envelope engine and independent NIST vectors.
    /// </summary>
    internal static void EncryptAesGcm(
        ReadOnlySpan<byte> dek,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plaintext,
        Span<byte> ciphertext,
        Span<byte> tag,
        ReadOnlySpan<byte> aad)
    {
        if (dek.Length != 32
            || nonce.Length != PayloadProtectionLimits.NonceBytes
            || ciphertext.Length != plaintext.Length
            || tag.Length != PayloadProtectionLimits.TagBytes)
        {
            throw new PayloadProtectionFormatException();
        }

        EnsurePlatformSupport();
        try
        {
            using var cipher = new AesGcm(dek, PayloadProtectionLimits.TagBytes);
            cipher.Encrypt(nonce, plaintext, ciphertext, tag, aad);
        }
        catch (CryptographicException)
        {
            throw new PayloadProtectionCryptographicException();
        }
    }

    /// <summary>
    /// Authenticates and decrypts one envelope, returning an engine-owned mutable destination.
    /// </summary>
    internal static byte[] Decrypt(
        PayloadProtectionEnvelope envelope,
        ReadOnlySpan<byte> aad,
        ReadOnlySpan<byte> dek,
        ISensitiveBufferObserver? observer = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (dek.Length != 32
            || envelope.Nonce is null
            || envelope.Ciphertext is null
            || envelope.Tag is null
            || envelope.Nonce.Length != PayloadProtectionLimits.NonceBytes
            || envelope.Ciphertext.Length is 0 or > PayloadProtectionLimits.CiphertextBytes
            || envelope.Tag.Length != PayloadProtectionLimits.TagBytes)
        {
            throw new PayloadProtectionFormatException();
        }

        EnsurePlatformSupport();
        byte[] plaintext = new byte[envelope.Ciphertext.Length];
        try
        {
            using var cipher = new AesGcm(dek, PayloadProtectionLimits.TagBytes);
            cipher.Decrypt(envelope.Nonce, envelope.Ciphertext, envelope.Tag, plaintext, aad);
            return plaintext;
        }
        catch (AuthenticationTagMismatchException)
        {
            ClearPlaintext(plaintext, observer);
            throw new PayloadProtectionAuthenticationException();
        }
        catch (CryptographicException)
        {
            ClearPlaintext(plaintext, observer);
            throw new PayloadProtectionCryptographicException();
        }
    }

    /// <summary>
    /// Determines whether an authenticated envelope nonce matches its field ordinal.
    /// </summary>
    internal static bool HasExpectedNonce(PayloadProtectionEnvelope envelope)
    {
        Span<byte> expectedNonce = stackalloc byte[PayloadProtectionLimits.NonceBytes];
        expectedNonce.Clear();
        BinaryPrimitives.WriteUInt64BigEndian(expectedNonce[4..], envelope.FieldOrdinal);
        return envelope.Nonce.AsSpan().SequenceEqual(expectedNonce);
    }

    /// <summary>
    /// Rejects a platform without AES-GCM support before any external material boundary is crossed.
    /// </summary>
    internal static void EnsurePlatformSupport()
    {
        if (!AesGcm.IsSupported)
        {
            throw new PayloadProtectionCryptographicException();
        }
    }

    private static void ClearPlaintext(byte[] plaintext, ISensitiveBufferObserver? observer)
    {
        CryptographicOperations.ZeroMemory(plaintext);
        if (observer is null)
        {
            return;
        }

        try
        {
            observer.BufferCleared(SensitiveBufferKind.DecryptedPlaintext, plaintext);
        }
        catch
        {
            // Buffer observation is best effort and cannot alter cleanup or failure classification.
        }
    }

    private static void Clear(byte[]? buffer)
    {
        if (buffer is not null)
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }
}
