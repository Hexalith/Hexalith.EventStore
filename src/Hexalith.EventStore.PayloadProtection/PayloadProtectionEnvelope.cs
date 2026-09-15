// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 6, 8, 14, and 15.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Represents one validated binary pdenc-v2 envelope (normative section 6.2).
/// </summary>
/// <param name="KeyReference">The canonical durable DEK reference.</param>
/// <param name="DekVersion">The positive DEK version.</param>
/// <param name="FieldOrdinal">The zero-based protected-field ordinal.</param>
/// <param name="Nonce">The twelve-byte nonce.</param>
/// <param name="Ciphertext">The ciphertext bytes.</param>
/// <param name="Tag">The sixteen-byte authentication tag.</param>
internal sealed record PayloadProtectionEnvelope(
    string KeyReference,
    uint DekVersion,
    uint FieldOrdinal,
    byte[] Nonce,
    byte[] Ciphertext,
    byte[] Tag)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(PayloadProtectionEnvelope);
}
