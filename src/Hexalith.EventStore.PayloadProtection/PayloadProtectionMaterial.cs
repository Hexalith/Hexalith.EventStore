// Normative authority: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e; sections 8, 14, and 15.
namespace Hexalith.EventStore.PayloadProtection;

/// <summary>
/// Carries fresh per-payload cryptographic material transferred to the core for one invocation.
/// </summary>
/// <param name="KeyReference">The fresh canonical key reference.</param>
/// <param name="DekVersion">The positive DEK version.</param>
/// <param name="DataEncryptionKey">The mutable 32-byte DEK whose ownership transfers to the core.</param>
internal sealed record PayloadProtectionMaterial(
    string KeyReference,
    uint DekVersion,
    byte[] DataEncryptionKey)
{
    /// <inheritdoc/>
    public override string ToString() => nameof(PayloadProtectionMaterial);
}
