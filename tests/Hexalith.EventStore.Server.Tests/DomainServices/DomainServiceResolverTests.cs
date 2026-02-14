namespace Hexalith.EventStore.Server.Tests.DomainServices;

using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Server.DomainServices;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

public class DomainServiceResolverTests
{
    private readonly DaprClient _daprClient = Substitute.For<DaprClient>();
    private readonly IOptions<DomainServiceOptions> _options = Options.Create(new DomainServiceOptions());
    private readonly ILogger<DomainServiceResolver> _logger = NullLogger<DomainServiceResolver>.Instance;

    private DomainServiceResolver CreateResolver() => new(_daprClient, _options, _logger);

    private void ConfigureConfigStore(string key, string? value)
    {
        var items = new Dictionary<string, ConfigurationItem>();
        if (value is not null)
        {
            items[key] = new ConfigurationItem(value, "1", new Dictionary<string, string>());
        }

        var response = new GetConfigurationResponse(items);
        _daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(response);
    }

    private void ConfigureConfigStoreMultiple(Dictionary<string, string> registrations)
    {
        var items = new Dictionary<string, ConfigurationItem>();
        foreach (var kvp in registrations)
        {
            items[kvp.Key] = new ConfigurationItem(kvp.Value, "1", new Dictionary<string, string>());
        }

        var response = new GetConfigurationResponse(items);
        _daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(keys => keys.Any(k => items.ContainsKey(k))),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(response);
    }

    [Fact]
    public async Task ResolveAsync_RegisteredService_ReturnsRegistration()
    {
        // Arrange
        var registration = new DomainServiceRegistration("orders-svc", "process-command", "tenant-a", "orders", "v1");
        string json = JsonSerializer.Serialize(registration);
        ConfigureConfigStore("tenant-a:orders:v1", json);
        var resolver = CreateResolver();

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "orders");

        // Assert
        result.ShouldNotBeNull();
        result.AppId.ShouldBe("orders-svc");
        result.MethodName.ShouldBe("process-command");
    }

    [Fact]
    public async Task ResolveAsync_UnregisteredService_ReturnsNull()
    {
        // Arrange
        ConfigureConfigStore("tenant-a:orders:v1", null);
        var resolver = CreateResolver();

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "orders");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_UsesCorrectConfigKeyWithVersion()
    {
        // Arrange
        ConfigureConfigStore("my-tenant:my-domain:v1", null);
        var resolver = CreateResolver();

        // Act
        await resolver.ResolveAsync("my-tenant", "my-domain");

        // Assert
        await _daprClient.Received(1).GetConfiguration(
            "configstore",
            Arg.Is<IReadOnlyList<string>>(keys => keys.Contains("my-tenant:my-domain:v1")),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_ExplicitVersion_UsesVersionInKey()
    {
        // Arrange
        var registration = new DomainServiceRegistration("orders-svc-v2", "process-command", "tenant-a", "orders", "v2");
        string json = JsonSerializer.Serialize(registration);
        ConfigureConfigStore("tenant-a:orders:v2", json);
        var resolver = CreateResolver();

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "orders", "v2");

        // Assert
        result.ShouldNotBeNull();
        result.AppId.ShouldBe("orders-svc-v2");
        await _daprClient.Received(1).GetConfiguration(
            "configstore",
            Arg.Is<IReadOnlyList<string>>(keys => keys.Contains("tenant-a:orders:v2")),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_NullTenantId_ThrowsArgumentException()
    {
        // Arrange
        var resolver = CreateResolver();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(() => resolver.ResolveAsync(null!, "domain"));
    }

    [Fact]
    public async Task ResolveAsync_NullDomain_ThrowsArgumentException()
    {
        // Arrange
        var resolver = CreateResolver();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(() => resolver.ResolveAsync("tenant", null!));
    }

    [Fact]
    public async Task ResolveAsync_NullVersion_ThrowsArgumentException()
    {
        // Arrange
        var resolver = CreateResolver();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(() => resolver.ResolveAsync("tenant", "domain", null!));
    }

    [Fact]
    public async Task ResolveAsync_EmptyConfigValue_ReturnsNull()
    {
        // Arrange
        var items = new Dictionary<string, ConfigurationItem>
        {
            ["tenant-a:orders:v1"] = new ConfigurationItem("  ", "1", new Dictionary<string, string>()),
        };
        var response = new GetConfigurationResponse(items);
        _daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(response);
        var resolver = CreateResolver();

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "orders");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_MalformedJson_ThrowsDomainServiceException()
    {
        // Arrange - corrupted config store entry should throw, not silently return null
        ConfigureConfigStore("tenant-a:orders:v1", "NOT-VALID-JSON{{{");
        var resolver = CreateResolver();

        // Act & Assert
        var ex = await Should.ThrowAsync<DomainServiceException>(() => resolver.ResolveAsync("tenant-a", "orders"));
        ex.Message.ShouldContain("corrupted configuration");
        ex.Message.ShouldContain("tenant-a");
        ex.InnerException.ShouldBeOfType<System.Text.Json.JsonException>();
    }

    [Fact]
    public async Task ResolveAsync_JsonNull_ReturnsNull()
    {
        // Arrange
        ConfigureConfigStore("tenant-a:orders:v1", "null");
        var resolver = CreateResolver();

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "orders");

        // Assert
        result.ShouldBeNull();
    }

    // --- Task 1.1: Multi-tenant multi-domain routing ---

    [Fact]
    public async Task ResolveAsync_MultipleTenantsSameDomain_RoutesToCorrectService()
    {
        // Arrange - two tenants, same domain, different service endpoints
        var regTenantA = new DomainServiceRegistration("orders-tenant-a", "process-command", "tenant-a", "orders", "v1");
        var regTenantB = new DomainServiceRegistration("orders-tenant-b", "process-command", "tenant-b", "orders", "v1");

        // Configure separate responses per key
        _daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(keys => keys.Contains("tenant-a:orders:v1")),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetConfigurationResponse(new Dictionary<string, ConfigurationItem>
            {
                ["tenant-a:orders:v1"] = new(JsonSerializer.Serialize(regTenantA), "1", new Dictionary<string, string>()),
            }));

        _daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(keys => keys.Contains("tenant-b:orders:v1")),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetConfigurationResponse(new Dictionary<string, ConfigurationItem>
            {
                ["tenant-b:orders:v1"] = new(JsonSerializer.Serialize(regTenantB), "1", new Dictionary<string, string>()),
            }));

        var resolver = CreateResolver();

        // Act
        DomainServiceRegistration? resultA = await resolver.ResolveAsync("tenant-a", "orders");
        DomainServiceRegistration? resultB = await resolver.ResolveAsync("tenant-b", "orders");

        // Assert
        resultA.ShouldNotBeNull();
        resultA.AppId.ShouldBe("orders-tenant-a");
        resultB.ShouldNotBeNull();
        resultB.AppId.ShouldBe("orders-tenant-b");
    }

    [Fact]
    public async Task ResolveAsync_SameTenantMultipleDomains_RoutesToCorrectService()
    {
        // Arrange - same tenant, two domains, different service endpoints
        var regOrders = new DomainServiceRegistration("orders-svc", "process-command", "tenant-a", "orders", "v1");
        var regInventory = new DomainServiceRegistration("inventory-svc", "process-command", "tenant-a", "inventory", "v1");

        _daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(keys => keys.Contains("tenant-a:orders:v1")),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetConfigurationResponse(new Dictionary<string, ConfigurationItem>
            {
                ["tenant-a:orders:v1"] = new(JsonSerializer.Serialize(regOrders), "1", new Dictionary<string, string>()),
            }));

        _daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(keys => keys.Contains("tenant-a:inventory:v1")),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetConfigurationResponse(new Dictionary<string, ConfigurationItem>
            {
                ["tenant-a:inventory:v1"] = new(JsonSerializer.Serialize(regInventory), "1", new Dictionary<string, string>()),
            }));

        var resolver = CreateResolver();

        // Act
        DomainServiceRegistration? resultOrders = await resolver.ResolveAsync("tenant-a", "orders");
        DomainServiceRegistration? resultInventory = await resolver.ResolveAsync("tenant-a", "inventory");

        // Assert
        resultOrders.ShouldNotBeNull();
        resultOrders.AppId.ShouldBe("orders-svc");
        resultInventory.ShouldNotBeNull();
        resultInventory.AppId.ShouldBe("inventory-svc");
    }

    // --- Task 1.2 / Task 6: No caching verification (ADR-1) ---

    [Fact]
    public async Task ResolveAsync_NoCaching_GetConfigurationCalledEveryInvocation()
    {
        // Arrange - Task 6.1: verify GetConfiguration called TWICE (not cached)
        var registration = new DomainServiceRegistration("orders-svc", "process-command", "tenant-a", "orders", "v1");
        string json = JsonSerializer.Serialize(registration);
        ConfigureConfigStore("tenant-a:orders:v1", json);
        var resolver = CreateResolver();

        // Act - invoke twice with same tenant+domain
        await resolver.ResolveAsync("tenant-a", "orders");
        await resolver.ResolveAsync("tenant-a", "orders");

        // Assert - GetConfiguration must have been called TWICE, not cached
        await _daprClient.Received(2).GetConfiguration(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    // --- Task 8: Version format validation and normalization at resolver level ---

    [Theory]
    [InlineData("version1")]
    [InlineData("v1a")]
    [InlineData("v1:evil")]
    [InlineData("v")]
    [InlineData("1")]
    public async Task ResolveAsync_InvalidVersionFormat_ThrowsArgumentException(string invalidVersion)
    {
        // Arrange - Task 8.6: invalid version formats rejected at resolver
        var resolver = CreateResolver();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(() => resolver.ResolveAsync("tenant", "domain", invalidVersion));
    }

    [Fact]
    public async Task ResolveAsync_UppercaseVersion_NormalizesToLowercase()
    {
        // Arrange - Task 8.7: "V2" normalized to "v2" at resolver level
        var registration = new DomainServiceRegistration("svc-v2", "process-command", "tenant-a", "orders", "v2");
        string json = JsonSerializer.Serialize(registration);
        ConfigureConfigStore("tenant-a:orders:v2", json);
        var resolver = CreateResolver();

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "orders", "V2");

        // Assert
        result.ShouldNotBeNull();
        result.AppId.ShouldBe("svc-v2");
        // Verify the config store was queried with lowercase key
        await _daprClient.Received(1).GetConfiguration(
            "configstore",
            Arg.Is<IReadOnlyList<string>>(keys => keys.Contains("tenant-a:orders:v2")),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_NoCache_EachCallQueriesConfigStore()
    {
        // Arrange - first call returns null, second returns registration (simulating dynamic addition)
        var resolver = CreateResolver();

        _daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(
                new GetConfigurationResponse(new Dictionary<string, ConfigurationItem>()),
                new GetConfigurationResponse(new Dictionary<string, ConfigurationItem>
                {
                    ["new-tenant:orders:v1"] = new(
                        JsonSerializer.Serialize(new DomainServiceRegistration("new-svc", "process", "new-tenant", "orders", "v1")),
                        "1",
                        new Dictionary<string, string>()),
                }));

        // Act
        DomainServiceRegistration? result1 = await resolver.ResolveAsync("new-tenant", "orders");
        DomainServiceRegistration? result2 = await resolver.ResolveAsync("new-tenant", "orders");

        // Assert
        result1.ShouldBeNull();
        result2.ShouldNotBeNull();
        result2.AppId.ShouldBe("new-svc");
    }
}
