// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 3-7; base V079-V106, V127-V132.
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests;
public sealed class AzureKeyVaultWireTests
{
    [Theory]
    [InlineData(32)]
    [InlineData(384)]
    public void V091_EmitsExactlyAlgAndValueWithoutTrailingBytes(int length)
    {
        byte[] input = RandomNumberGenerator.GetBytes(length);
        byte[] encoded = AzureKeyVaultWireCodec.Encode(input);
        using var json = JsonDocument.Parse(encoded);
        json.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(["alg", "value"]);
        json.RootElement.GetProperty("alg").GetString().ShouldBe("RSA-OAEP-256");
        encoded[^1].ShouldBe((byte)'}');
        AzureKeyVaultWireCodec.Decode(AdapterFixture.Crypto(AdapterFixture.Current, input), AdapterFixture.Current, length).ShouldBe(input);
        CryptographicOperations.ZeroMemory(input);
        CryptographicOperations.ZeroMemory(encoded);
    }

    [Theory]
    [InlineData("wrong-kid")]
    [InlineData("wrong-alg")]
    [InlineData("wrong-length")]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("trailing")]
    [InlineData("padding")]
    [InlineData("escaped")]
    public void V091_RejectsMalformedOrUnboundCryptoResponse(string mutation)
    {
        string value = Convert.ToBase64String(new byte[32]).TrimEnd('=');
        string json = "{\"kid\":\"" + AdapterFixture.Current + "\",\"value\":\"" + value + "\"}";
        json = mutation switch
        {
            "wrong-kid" => json.Replace(AdapterFixture.Current, AdapterFixture.Historical),
            "wrong-alg" => json[..^1] + ",\"alg\":\"RSA1_5\"}",
            "wrong-length" => json.Replace(value, "AA"),
            "unknown" => json[..^1] + ",\"unknown\":\"secret-canary\"}",
            "duplicate" => json[..^1] + ",\"value\":\"" + value + "\"}",
            "trailing" => json + "{}",
            "padding" => json.Replace(value, value + "="),
            "escaped" => json.Replace("value", "val\\u0075e"),
            _ => json,
        };
        Should.Throw<Exception>(() => AzureKeyVaultWireCodec.Decode(Encoding.UTF8.GetBytes(json), AdapterFixture.Current, 32));
    }

    [Fact]
    public async Task V078_OwnedRequestIsClearedOnlyAfterDisposal()
    {
        byte[] bytes = AzureKeyVaultWireCodec.Encode(new byte[32]);
        var content = new AzureKeyVaultOwnedRequestContent(bytes);
        using var stream = new MemoryStream();
        await content.WriteToAsync(stream, CancellationToken.None);
        bytes.ShouldContain((byte)'R');
        content.Dispose();
        bytes.ShouldAllBe(value => value == 0);
        content.Dispose();
    }
}
