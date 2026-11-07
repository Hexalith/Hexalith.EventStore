// Approved on-premises supplement: 43fc3400f9e01662eac5ad945407711d0d24a744685e92cef203c2162106e2d3; sections 1-7.
// On-premises authority: 835f96ca5b4253f0598712a40773df468f02bd48f518eb0313794addfdc9b180; approved supplement sections 2, 4-5.
using System.Security.Cryptography;

namespace Hexalith.EventStore.Contracts.Security;

/// <summary>A response bound to one exact encoded request. Disposal clears untransferred response material.</summary>
/// <param name="ProtocolVersion">The exact wire version, one.</param>
/// <param name="Operation">The original closed operation.</param>
/// <param name="OperationId">The original canonical stable operation identity.</param>
/// <param name="RequestId">The original canonical request identity.</param>
/// <param name="InstallationId">The exact installed identity.</param>
/// <param name="ProfileSha256">The qualified installed profile digest.</param>
/// <param name="RequestSha256">SHA-256 of the original exact encoded request bytes.</param>
/// <param name="Outcome">The closed result; transport receipt alone never supplies success.</param>
/// <param name="ProviderKeyVersionId">The exact opaque provider version for a successful key operation.</param>
/// <param name="Bytes">Owned operation output, never present for Profile or Lookup.</param>
/// <param name="Profile">The fresh profile on successful Profile only.</param>
/// <param name="RetryAfterMilliseconds">An optional nonnegative retry delay capped at two seconds.</param>
public sealed record PayloadKeyBackendResponse(int ProtocolVersion, PayloadKeyBackendOperation Operation,
    string OperationId, string RequestId, string InstallationId, string ProfileSha256, string RequestSha256,
    PayloadKeyBackendOutcome Outcome, string? ProviderKeyVersionId = null, byte[]? Bytes = null,
    PayloadKeyBackendProfile? Profile = null, int? RetryAfterMilliseconds = null) : IDisposable
{
    /// <summary>Clears owned output bytes after consumption or refusal.</summary>
    public void Dispose()
    {
        if (Bytes is not null) { CryptographicOperations.ZeroMemory(Bytes); }
    }

    /// <summary>Returns a safe type name without output bytes or provider identities.</summary>
    public override string ToString() => nameof(PayloadKeyBackendResponse);
}
