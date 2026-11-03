// Authority: fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394, sections 3-7; Core correction 360da00f91d3134054e4ec3f18c0b96921a36911a89d5935e7d59409634ca07e; base sections 5, 11, 15.
using Azure.Core.Pipeline;
using Azure.Core;
using Azure.Security.KeyVault.Keys;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault;
/// <summary>Limits the SDK metadata surface to GetKey at the configured name and explicit stable API version.</summary>
internal sealed class AzureKeyVaultMetadataClient
{
    private readonly Func<string?, CancellationToken, ValueTask<KeyVaultKey>> _get;
    /// <summary>Creates the runtime metadata client with resource verification enabled and no nested retries.</summary>
    internal AzureKeyVaultMetadataClient(AzureKeyVaultPayloadProtectionOptions options, TokenCredential credential, HttpPipelineTransport transport)
    {
        var clientOptions = new KeyClientOptions(KeyClientOptions.ServiceVersion.V2025_07_01)
        {
            DisableChallengeResourceVerification = false
        };
        AzureKeyVaultCredentialFactory.Configure(clientOptions, transport);
        var client = new KeyClient(options.VaultUri, AzureKeyVaultCredentialFactory.Bound(credential), clientOptions);
        _get = async (version, token) =>
        {
            Azure.Response<KeyVaultKey> response = await client.GetKeyAsync(options.KeyName, version, token).ConfigureAwait(false);
            using Azure.Response owned = response.GetRawResponse();
            return response.Value;
        };
    }

    /// <summary>Creates an internal unit-test seam unavailable from runtime configuration.</summary>
    internal AzureKeyVaultMetadataClient(Func<string?, CancellationToken, ValueTask<KeyVaultKey>> get) => _get = get;
    /// <summary>Reads only the selected alias or an exact version.</summary>
    internal ValueTask<KeyVaultKey> GetAsync(string? version, CancellationToken token) => _get(version, token);
}
