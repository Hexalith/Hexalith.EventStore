// Approved on-premises supplement: 43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3; sections 1-7.
// On-premises authority: 835f96ca5b4253f0598712a40773df468f02bd48f518eb0313794addfdc9b180; approved supplement sections 2, 4-5.
using System.Security.Cryptography;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>A bounded neutral request. The request owns its mutable byte array and must be disposed after I/O completes.</summary>
/// <param name="ProtocolVersion">The exact wire version, one.</param>
/// <param name="Operation">The closed operation.</param>
/// <param name="OperationId">The canonical ULID stable mutation identity.</param>
/// <param name="RequestId">The canonical ULID identity of this exact request attempt.</param>
/// <param name="InstallationId">The installed canonical identity.</param>
/// <param name="ProfileSha256">The qualified installed profile digest.</param>
/// <param name="ProviderKeyVersionId">The opaque exact version, required for every key operation.</param>
/// <param name="Bytes">Owned 32-byte DEK or 384-byte RSA ciphertext; absent on Profile and Lookup.</param>
public sealed record PayloadKeyBackendRequest(int ProtocolVersion, PayloadKeyBackendOperation Operation,
    string OperationId, string RequestId, string InstallationId, string ProfileSha256,
    string? ProviderKeyVersionId = null, byte[]? Bytes = null) : IDisposable
{
    /// <summary>Clears owned request material after actual transport completion.</summary>
    public void Dispose()
    {
        if (Bytes is not null) { CryptographicOperations.ZeroMemory(Bytes); }
    }

    /// <summary>Returns a safe type name without request bytes or identities.</summary>
    public override string ToString() => nameof(PayloadKeyBackendRequest);
}
