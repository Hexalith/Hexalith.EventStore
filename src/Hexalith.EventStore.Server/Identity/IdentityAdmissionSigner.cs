using System.Security.Cryptography;
using System.Text.Json;

using Hexalith.EventStore.Client.Security;
using Hexalith.EventStore.Contracts.Security;

using Microsoft.Extensions.Options;

namespace Hexalith.EventStore.Server.Identity;

/// <summary>Signs exact-operation evidence with a gateway-owned asymmetric key.</summary>
public sealed class IdentityAdmissionSigner(IOptionsMonitor<IdentityAdmissionSigningOptions> options) : IIdentityAdmissionSigner
{
    /// <inheritdoc/>
    public string Sign(IdentityAdmissionEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(options.CurrentValue.PrivateKeyPem
            ?? throw new InvalidOperationException("Identity signing is unavailable."));
        if (rsa.KeySize < 2048)
        {
            throw new InvalidOperationException("Identity signing is unavailable.");
        }

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(evidence);
        return Convert.ToBase64String(bytes) + "." + Convert.ToBase64String(
            rsa.SignData(IdentityAdmissionProof.Encode(bytes), HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }
}
