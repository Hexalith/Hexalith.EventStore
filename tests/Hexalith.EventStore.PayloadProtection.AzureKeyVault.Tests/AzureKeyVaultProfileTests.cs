// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 3-7; base V079-V106, V127-V132.
namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests;
public sealed class AzureKeyVaultProfileTests
{
    [Fact]
    public async Task V090_RequiresExplicitCompleteHsmProfile()
    {
        var key = await AdapterFixture.KeyAsync();
        AzureKeyVaultKeyProfile.Validate(AdapterFixture.Options, key, DateTimeOffset.UtcNow, AdapterFixture.Current).Id.ShouldBe(AdapterFixture.Current);
    }

    [Theory]
    [InlineData("RSA")]
    [InlineData("EC")]
    [InlineData("oct-HSM")]
    public async Task V105_RejectsWrongKeyType(string type)
    {
        var key = await AdapterFixture.KeyAsync(AdapterFixture.Metadata(type: type));
        Should.Throw<InvalidOperationException>(() => AzureKeyVaultKeyProfile.Validate(AdapterFixture.Options, key, DateTimeOffset.UtcNow, null));
    }

    [Theory]
    [InlineData(2048)]
    [InlineData(4096)]
    [InlineData(null)]
    public async Task V105_RejectsMissingOrWrongSizeEvidence(int? size)
    {
        var key = await AdapterFixture.KeyAsync(AdapterFixture.Metadata(size: size));
        Should.Throw<InvalidOperationException>(() => AzureKeyVaultKeyProfile.Validate(AdapterFixture.Options, key, DateTimeOffset.UtcNow, null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    public async Task V105_RejectsExportableOrUnknownExportability(bool? exportable)
    {
        var key = await AdapterFixture.KeyAsync(AdapterFixture.Metadata(exportable: exportable));
        Should.Throw<InvalidOperationException>(() => AzureKeyVaultKeyProfile.Validate(AdapterFixture.Options, key, DateTimeOffset.UtcNow, null));
    }

    [Theory]
    [InlineData("d")]
    [InlineData("p")]
    [InlineData("q")]
    [InlineData("dp")]
    [InlineData("dq")]
    [InlineData("qi")]
    [InlineData("k")]
    [InlineData("key_hsm")]
    public async Task V105_RejectsContradictoryPrivateOrSymmetricMaterial(string field)
    {
        var key = await AdapterFixture.KeyAsync(AdapterFixture.Metadata(privateField: field));
        Should.Throw<InvalidOperationException>(() => AzureKeyVaultKeyProfile.Validate(AdapterFixture.Options, key, DateTimeOffset.UtcNow, null));
    }

    [Theory]
    [InlineData("missing-op")]
    [InlineData("extra-op")]
    [InlineData("duplicate-op")]
    [InlineData("disabled")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("missing-hsm")]
    public async Task V084_V090_RejectsInvalidStateAndOperationSet(string mutation)
    {
        var key = await AdapterFixture.KeyAsync(AdapterFixture.Metadata(operations: mutation switch
        {
            "missing-op" => ["wrapKey"],
            "extra-op" => ["wrapKey", "unwrapKey", "sign"],
            "duplicate-op" => ["wrapKey", "wrapKey"],
            _ => null
        }, enabled: mutation != "disabled", hsm: mutation == "missing-hsm" ? null : "PlatformTest", expires: mutation == "expired" ? 1 : null, notBefore: mutation == "future" ? 4102444800 : null));
        Should.Throw<InvalidOperationException>(() => AzureKeyVaultKeyProfile.Validate(AdapterFixture.Options, key, DateTimeOffset.UtcNow, null));
    }

    [Theory]
    [InlineData("https://other-vault.vault.azure.net/keys/eventstore-pp/00000000000000000000000000000002")]
    [InlineData("https://hx-pp-unit.vault.azure.net/keys/other-key/00000000000000000000000000000002")]
    [InlineData(AdapterFixture.Historical)]
    public async Task V091_RejectsReturnedVaultNameOrVersionMismatch(string id)
    {
        var key = await AdapterFixture.KeyAsync(AdapterFixture.Metadata(id));
        Should.Throw<InvalidOperationException>(() => AzureKeyVaultKeyProfile.Validate(AdapterFixture.Options, key, DateTimeOffset.UtcNow, AdapterFixture.Current));
    }
}
