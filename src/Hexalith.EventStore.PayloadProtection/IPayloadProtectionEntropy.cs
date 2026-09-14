namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Supplies fresh per-attempt material through a bounded test seam; durable reservation remains Story 8.5-owned.
/// </summary>
internal interface IPayloadProtectionEntropy {
    /// <summary>Creates a canonical fresh key reference.</summary>
    string CreateKeyReference();

    /// <summary>Fills a 32-byte destination with fresh CSPRNG output.</summary>
    void FillDataEncryptionKey(Span<byte> destination);
}
