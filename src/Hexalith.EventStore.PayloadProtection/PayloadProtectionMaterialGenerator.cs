// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 8, 14, and 15.
using System.Security.Cryptography;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Generates wholly fresh material after each observable key-reference collision (normative section 8.2, V046-V048).
/// </summary>
/// <param name="entropy">The entropy source, or <see langword="null"/> to use the production CSPRNG implementation.</param>
/// <param name="observer">An optional test-only cleared-buffer observer.</param>
internal sealed class PayloadProtectionMaterialGenerator(
    IPayloadProtectionEntropy? entropy = null,
    ISensitiveBufferObserver? observer = null)
{
    private const int _maximumAttempts = 16;
    private readonly IPayloadProtectionEntropy _entropy = entropy ?? new CryptographicPayloadProtectionEntropy();

    /// <summary>
    /// Generates material whose reference is accepted by a local/durable collision predicate.
    /// </summary>
    internal PayloadProtectionMaterial Generate(Func<string, bool> isAvailable, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(isAvailable);
        for (int attempt = 0; attempt < _maximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string keyReference;
            try
            {
                keyReference = _entropy.CreateKeyReference();
            }
            catch
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }

            cancellationToken.ThrowIfCancellationRequested();
            byte[] dek = new byte[32];
            try
            {
                _entropy.FillDataEncryptionKey(dek);
                cancellationToken.ThrowIfCancellationRequested();
                if (!CanonicalUlid.IsValid(keyReference))
                {
                    throw new PayloadProtectionFormatException();
                }

                bool available = isAvailable(keyReference);
                cancellationToken.ThrowIfCancellationRequested();
                if (available)
                {
                    return new PayloadProtectionMaterial(keyReference, 1, dek);
                }
            }
            catch
            {
                Clear(dek);
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }

            Clear(dek);
        }

        throw new PayloadProtectionCryptographicException();
    }

    private void Clear(byte[] buffer)
    {
        CryptographicOperations.ZeroMemory(buffer);
        if (observer is null)
        {
            return;
        }

        try
        {
            observer.BufferCleared(SensitiveBufferKind.DataEncryptionKey, buffer);
        }
        catch
        {
            // Cleanup observers are best effort and cannot alter material-generation outcomes.
        }
    }
}
