// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 8, 14, and 15.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Supplies fresh per-attempt material through a bounded test seam; durable reservation remains Story 8.5-owned.
/// </summary>
internal interface IPayloadProtectionEntropy
{
    /// <summary>Creates a canonical fresh key reference.</summary>
    string CreateKeyReference();

    /// <summary>Fills a 32-byte destination with fresh CSPRNG output.</summary>
    void FillDataEncryptionKey(Span<byte> destination);
}
