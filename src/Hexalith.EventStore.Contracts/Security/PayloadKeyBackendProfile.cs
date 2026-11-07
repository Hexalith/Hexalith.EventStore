// Approved on-premises supplement: 43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3; sections 1-7.
// On-premises authority: 835f96ca5b4253f0598712a40773df468f02bd48f518eb0313794addfdc9b180; approved supplement sections 2, 4-5.
namespace Hexalith.EventStore.Contracts.Security;

/// <summary>A fresh software RSA-3072 observation, never an authority grant or readiness certificate.</summary>
/// <param name="KeyType">The exact software key type, rsa-3072.</param>
/// <param name="WrapAlgorithm">The exact OAEP SHA-256/MGF1-SHA-256 algorithm, RSA-OAEP-256.</param>
/// <param name="Exportable">Whether native key export is enabled.</param>
/// <param name="AllowPlaintextBackup">Whether native plaintext backup is enabled.</param>
/// <param name="Imported">Whether the key was imported.</param>
/// <param name="Derived">Whether derivation is enabled.</param>
/// <param name="Convergent">Whether convergent encryption is enabled.</param>
/// <param name="CurrentVersionId">The opaque installation/key/version identity; never a caller-selected provider path.</param>
/// <param name="SupportedOperations">The exact supported neutral operation set.</param>
public sealed record PayloadKeyBackendProfile(string KeyType, string WrapAlgorithm, bool Exportable,
    bool AllowPlaintextBackup, bool Imported, bool Derived, bool Convergent, string CurrentVersionId,
    PayloadKeyBackendOperation[] SupportedOperations)
{
    /// <summary>Returns a safe type name without provider identities.</summary>
    public override string ToString() => nameof(PayloadKeyBackendProfile);
}
