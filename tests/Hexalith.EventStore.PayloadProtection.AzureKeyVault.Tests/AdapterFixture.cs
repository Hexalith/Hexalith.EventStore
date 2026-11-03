// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 3-7; base V079-V106, V127-V132.
using System.Text.Json;
using System.Text;
using Azure.Security.KeyVault.Keys;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests;
internal static class AdapterFixture
{
    internal const string Operation = "01J00000000000000000000860";
    internal const string Current = "https://hx-pp-unit.vault.azure.net/keys/eventstore-pp/00000000000000000000000000000002";
    internal const string Historical = "https://hx-pp-unit.vault.azure.net/keys/eventstore-pp/00000000000000000000000000000001";
    internal static AzureKeyVaultPayloadProtectionOptions Options => new()
    {
        VaultUri = new("https://hx-pp-unit.vault.azure.net"),
        KeyName = "eventstore-pp"
    };

    internal static AzureKeyVaultOwnedResponse Response(int status, byte[] body, Dictionary<string, string>? headers = null) => new(status, body, headers ?? []);
    internal static byte[] Crypto(string id, byte[] value) => JsonSerializer.SerializeToUtf8Bytes(new { kid = id, value = Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_') });
    internal static byte[] Metadata(string id = Current, string type = "RSA-HSM", int? size = 3072, bool? exportable = false, bool enabled = true, string? hsm = "PlatformTest", string[]? operations = null, string? privateField = null, long? expires = null, long? notBefore = null)
    {
        byte[] modulus = new byte[384];
        modulus[0] = 0x80;
        var key = new Dictionary<string, object?>
        {
            ["kid"] = id,
            ["kty"] = type,
            ["n"] = Convert.ToBase64String(modulus),
            ["e"] = "AQAB",
            ["key_ops"] = operations ?? ["wrapKey", "unwrapKey"]
        };
        if (privateField is not null)
        {
            key[privateField] = "AQ";
        }

        return JsonSerializer.SerializeToUtf8Bytes(new { key, attributes = new Dictionary<string, object?> { ["enabled"] = enabled, ["key_size"] = size, ["exportable"] = exportable, ["hsmPlatform"] = hsm, ["exp"] = expires, ["nbf"] = notBefore }.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value) });
    }

    internal static async ValueTask<KeyVaultKey> KeyAsync(byte[]? body = null)
    {
        var transport = new AdapterTestTransport(message =>
        {
            message.Response = Response(200, body ?? Metadata());
            return ValueTask.CompletedTask;
        });
        return await new AzureKeyVaultMetadataClient(Options, new AdapterTestCredential(), transport).GetAsync(null, CancellationToken.None);
    }

    internal static async ValueTask<byte[]> ReadRequestAsync(Azure.Core.HttpMessage message)
    {
        using var stream = new MemoryStream();
        await message.Request.Content!.WriteToAsync(stream, message.CancellationToken);
        return stream.ToArray();
    }
}
