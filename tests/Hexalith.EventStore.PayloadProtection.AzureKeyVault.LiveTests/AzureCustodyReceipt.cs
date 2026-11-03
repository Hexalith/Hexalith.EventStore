// Approved Azure exception sections 5 and 7; operator evidence, not inferred from key metadata or a passing probe.
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests;
/// <summary>Content-bound observation supplied by the separately authorized isolated-environment operator.</summary>
internal sealed record AzureCustodyReceipt(string SourceSha, string ResourceIdSha256, string IdentityIdSha256, string NetworkIdSha256, string Region, string ResourceType, string Sku, bool ProviderGenerated, bool NonExportable, string KeyType, int KeySize, bool PurgeProtection, int RetentionDays, bool PrivateEndpoint, bool PrivateDnsVerified, string PublicNetworkAccess, bool TrustedServiceBypass, string RuntimeRole, bool KeyScopedRuntimeRole, bool SeparateProvisioningIdentity, string DiagnosticSinkSha256, DateTimeOffset ObservedAtUtc)
{
    internal static void Validate(byte[] bytes, AzureLiveAuthority authority)
    {
        var receipt = JsonSerializer.Deserialize<AzureCustodyReceipt>(bytes, new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true }) ?? throw new InvalidOperationException("The custody receipt is missing.");
        if (receipt.SourceSha != authority.SourceSha || receipt.ResourceIdSha256 != authority.ResourceIdSha256 || receipt.IdentityIdSha256 != authority.IdentityIdSha256 || receipt.NetworkIdSha256 != authority.NetworkIdSha256 || receipt.Region != authority.Region || receipt.ResourceType != "Microsoft.KeyVault/vaults" || receipt.Sku != "Premium" || !receipt.ProviderGenerated || !receipt.NonExportable || receipt.KeyType != "RSA-HSM" || receipt.KeySize != 3072 || !receipt.PurgeProtection || receipt.RetentionDays != 90 || !receipt.PrivateEndpoint || !receipt.PrivateDnsVerified || receipt.PublicNetworkAccess != "Disabled" || receipt.TrustedServiceBypass || receipt.RuntimeRole != "Key Vault Crypto Service Encryption User" || !receipt.KeyScopedRuntimeRole || !receipt.SeparateProvisioningIdentity || receipt.DiagnosticSinkSha256.Length != 64 || receipt.DiagnosticSinkSha256.Any(c => !Uri.IsHexDigit(c)) || receipt.ObservedAtUtc > DateTimeOffset.UtcNow || receipt.ObservedAtUtc < DateTimeOffset.UtcNow.AddDays(-1))
        {
            throw new InvalidOperationException("The isolated custody receipt does not meet the approved profile.");
        }
    }

    public override string ToString() => nameof(AzureCustodyReceipt);
}
