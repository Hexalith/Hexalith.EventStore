namespace Hexalith.EventStore.AppHost.Tests.Configuration;

using Dapr.Client;

using Hexalith.EventStore.Aspire;
using Hexalith.EventStore.Server.DomainServices;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public sealed class EventStoreDomainServiceConfigurationTests
{
    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    public async Task RootEventStoreConfiguration_ConfiguresTenantsDomainServices(string fileName)
    {
        using DaprClient daprClient = new DaprClientBuilder().Build();
        DomainServiceOptions options = LoadDomainServiceOptions(fileName);

        options.Registrations.ShouldContainKey("system|tenants|v1");
        DomainServiceRegistration configuredRegistration = options.Registrations["system|tenants|v1"];
        configuredRegistration.AppId.ShouldBe("tenants");
        configuredRegistration.MethodName.ShouldBe("process");
        configuredRegistration.TenantId.ShouldBe("system");
        configuredRegistration.Domain.ShouldBe("tenants");
        configuredRegistration.Version.ShouldBe("v1");

        DomainServiceResolver resolver = CreateResolver(options, daprClient);

        await AssertResolvesAsync(
            resolver,
            tenantId: "system",
            domain: "tenants",
            appId: "tenants",
            expectedTenantId: "system").ConfigureAwait(true);
        await AssertResolvesAsync(
            resolver,
            tenantId: "system",
            domain: "global-administrators",
            appId: "tenants",
            expectedTenantId: "system").ConfigureAwait(true);
    }

    [Fact]
    public async Task RootEventStoreDevelopmentConfiguration_ConfiguresSampleDomainServices()
    {
        using DaprClient daprClient = new DaprClientBuilder().Build();
        DomainServiceResolver resolver = CreateResolver(
            LoadDomainServiceOptions("appsettings.Development.json"),
            daprClient);

        await AssertResolvesAsync(
            resolver,
            tenantId: "tenant-a",
            domain: "counter",
            appId: "sample",
            expectedTenantId: "tenant-a").ConfigureAwait(true);
        await AssertResolvesAsync(
            resolver,
            tenantId: "tenant-b",
            domain: "greeting",
            appId: "sample",
            expectedTenantId: "tenant-b").ConfigureAwait(true);
    }

    [Fact]
    public void RootEventStoreDevelopmentConfigurationContainsOnlyHostedSampleDomains()
    {
        DomainServiceOptions options = LoadDomainServiceOptions(
            "appsettings.json",
            "appsettings.Development.json");

        options.Registrations.Values
            .Where(static registration => string.Equals(registration.AppId, "sample", StringComparison.Ordinal))
            .Select(static registration => registration.Domain)
            .Order(StringComparer.Ordinal)
            .ShouldBe(["counter", "greeting"]);
    }

    private static DomainServiceResolver CreateResolver(DomainServiceOptions options, DaprClient daprClient)
        => new(
            daprClient,
            Options.Create(options),
            NullLogger<DomainServiceResolver>.Instance);

    private static DomainServiceOptions LoadDomainServiceOptions(params string[] fileNames)
    {
        DomainServiceOptions options = new();
        LoadEventStoreSettings(fileNames)
            .GetSection("EventStore:DomainServices")
            .Bind(options);
        return options;
    }

    private static IConfigurationRoot LoadEventStoreSettings(params string[] fileNames)
    {
        string eventStoreRoot = Path.Combine(
            RepositoryProjectPaths.GetRepositoryRoot(),
            "src",
            "Hexalith.EventStore");
        var builder = new ConfigurationBuilder();

        foreach (string fileName in fileNames)
        {
            string path = Path.Combine(eventStoreRoot, fileName);
            File.Exists(path).ShouldBeTrue($"Expected EventStore configuration file at {path}.");
            _ = builder.AddJsonFile(path, optional: false);
        }

        return builder.Build();
    }

    private static async Task AssertResolvesAsync(
        DomainServiceResolver resolver,
        string tenantId,
        string domain,
        string appId,
        string expectedTenantId)
    {
        DomainServiceRegistration? registration = await resolver
            .ResolveAsync(tenantId, domain)
            .ConfigureAwait(false);

        registration.ShouldNotBeNull();
        registration.AppId.ShouldBe(appId);
        registration.MethodName.ShouldBe("process");
        registration.TenantId.ShouldBe(expectedTenantId);
        registration.Domain.ShouldBe(domain);
        registration.Version.ShouldBe("v1");
    }
}
