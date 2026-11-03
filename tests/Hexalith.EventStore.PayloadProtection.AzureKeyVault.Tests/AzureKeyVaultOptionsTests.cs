// Approved Azure exception fcf861aa3ba5d723ca589a8ac8a2a31c54a972fe96a28ec71db69e560ecc2394 sections 3-7; base V079-V106, V127-V132.
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.PayloadProtection.AzureKeyVault.Tests;
public sealed class AzureKeyVaultOptionsTests
{
    [Theory]
    [InlineData("https://vault-name.vault.azure.net", "https://vault.azure.net/.default")]
    [InlineData("https://vault-name.vault.usgovcloudapi.net", "https://vault.usgovcloudapi.net/.default")]
    [InlineData("https://vault-name.vault.azure.cn", "https://vault.azure.cn/.default")]
    public void V104_AcceptsOnlyMatchedOrdinaryVaultClouds(string uri, string expected) => AzureKeyVaultOptionsValidator.Validate(new() { VaultUri = new(uri), KeyName = "key-name" }).ShouldBe(expected);
    [Theory]
    [InlineData("http://vault-name.vault.azure.net")]
    [InlineData("https://vault-name.managedhsm.azure.net")]
    [InlineData("https://vault-name.vault.azure.net:8443")]
    [InlineData("https://vault-name.vault.azure.net/path")]
    [InlineData("https://vault-name.vault.azure.net?token=canary")]
    [InlineData("https://user@vault-name.vault.azure.net")]
    [InlineData("https://vault-name.vault.azure.net/#fragment")]
    [InlineData("https://vault-name.vault.azure.net.evil.invalid")]
    public void V104_RejectsUnsafeOrigins(string uri) => Should.Throw<InvalidOperationException>(() => AzureKeyVaultOptionsValidator.Validate(new() { VaultUri = new(uri), KeyName = "key" }));
    [Theory]
    [InlineData("ExpectedKeySize", "2048")]
    [InlineData("WrapAlgorithm", "RSA-OAEP")]
    [InlineData("RequestTimeout", "00:00:00")]
    [InlineData("RequestTimeout", "00:00:31")]
    [InlineData("KeyVersionRefreshInterval", "01:00:01")]
    [InlineData("KeyVersionRefreshInterval", "00:00:00")]
    [InlineData("KeyName", "name/version")]
    [InlineData("ManagedIdentityClientId", "not-a-guid")]
    [InlineData("ClientSecret", "secret-canary")]
    [InlineData("TokenEndpoint", "https://evil.invalid")]
    public void V105_RejectsWeakerUnknownOrUnboundedOptions(string field, string value)
    {
        var values = new Dictionary<string, string?>
        {
            ["Azure:VaultUri"] = AdapterFixture.Options.VaultUri.AbsoluteUri,
            ["Azure:KeyName"] = "key"
        };
        values["Azure:" + field] = value;
        var section = new ConfigurationBuilder().AddInMemoryCollection(values).Build().GetSection("Azure");
        Should.Throw<InvalidOperationException>(() => AzureKeyVaultOptionsValidator.Load(section)).Message.ShouldBe("The Azure payload protection profile is invalid.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Development")]
    [InlineData("Dev")]
    [InlineData("Production")]
    [InlineData("custom")]
    public void V080_EveryEnvironmentUsesExactlyManagedIdentity(string? environment)
    {
        _ = environment;
        AzureKeyVaultCredentialFactory.Create(AdapterFixture.Options).ShouldBeOfType<AzureKeyVaultCredentialFactory>();
    }

    [Fact]
    public void V098_RegistrationIsExplicitAndNeverReplacesServerProtection()
    {
        var services = new ServiceCollection();
        services.ShouldBeEmpty();
        var section = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Azure:VaultUri"] = AdapterFixture.Options.VaultUri.AbsoluteUri, ["Azure:KeyName"] = "eventstore-pp" }).Build().GetSection("Azure");
        services.AddAzureKeyVaultPayloadProtectionBackend(section);
        services.Count(descriptor => descriptor.ServiceType == typeof(PayloadProtectionBackend)).ShouldBe(1);
        services.ShouldNotContain(descriptor => descriptor.ServiceType.Name == "IEventPayloadProtectionService");
        services.ShouldNotContain(descriptor => descriptor.ServiceType.Name == "IHostedService");
        Should.Throw<InvalidOperationException>(() => services.AddAzureKeyVaultPayloadProtectionBackend(section));
    }
}
