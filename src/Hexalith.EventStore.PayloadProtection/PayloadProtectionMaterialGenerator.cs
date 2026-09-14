using System.Security.Cryptography;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Generates wholly fresh material after each observable key-reference collision (normative section 8.2, V046-V048).
/// </summary>
internal sealed class PayloadProtectionMaterialGenerator(IPayloadProtectionEntropy entropy, ISensitiveBufferObserver? observer = null)
{
    private const int _maximumAttempts = 16;

    /// <summary>
    /// Generates material whose reference is accepted by a local/durable collision predicate.
    /// </summary>
    internal PayloadProtectionMaterial Generate(Func<string, bool> isAvailable, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(isAvailable);
        for (int attempt = 0; attempt < _maximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string keyReference = entropy.CreateKeyReference();
            byte[] dek = new byte[32];
            try
            {
                entropy.FillDataEncryptionKey(dek);
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

        throw new PayloadProtectionFormatException();
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
