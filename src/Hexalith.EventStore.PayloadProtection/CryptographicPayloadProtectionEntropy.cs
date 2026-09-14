using System.Security.Cryptography;
using Hexalith.Commons.UniqueIds;

namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Supplies production CSPRNG bytes and canonical ULID references for a single protection attempt.
/// </summary>
internal sealed class CryptographicPayloadProtectionEntropy : IPayloadProtectionEntropy {
    /// <inheritdoc/>
    public string CreateKeyReference() => UniqueIdHelper.GenerateSortableUniqueStringId();

    /// <inheritdoc/>
    public void FillDataEncryptionKey(Span<byte> destination) => RandomNumberGenerator.Fill(destination);
}
