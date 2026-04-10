
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Server.DomainServices;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.DomainServices;

public class DomainServiceResolverTests {
    private readonly DaprClient _daprClient = Substitute.For<DaprClient>();
    private readonly ILogger<DomainServiceResolver> _logger = NullLogger<DomainServiceResolver>.Instance;

    private DomainServiceResolver CreateResolver(DomainServiceOptions? opts = null) =>
        new(_daprClient, Options.Create(opts ?? new DomainServiceOptions()), _logger);

    private DomainServiceResolver CreateResolverWithConfigStore(string configStoreName = "configstore") =>
        CreateResolver(new DomainServiceOptions { ConfigStoreName = configStoreName });

    private void ConfigureConfigStore(string key, string? value) {
        var items = new Dictionary<string, ConfigurationItem>();
        if (value is not null) {
            items[key] = new ConfigurationItem(value, "1", new Dictionary<string, string>());
        }

        var response = new GetConfigurationResponse(items);
        _ = _daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(response);
    }

    // --- Convention-based routing (default behavior, zero config) ---

    [Fact]
    public async Task ResolveAsync_NoConfigStore_ReturnsConventionRouting() {
        // Arrange — default options (ConfigStoreName = null)
        DomainServiceResolver resolver = CreateResolver();

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "orders");

        // Assert — convention: AppId = domain, MethodName = "process"
        _ = result.ShouldNotBeNull();
        result.AppId.ShouldBe("orders");
        result.MethodName.ShouldBe("process");
        result.TenantId.ShouldBe("tenant-a");
        result.Domain.ShouldBe("orders");
    }

    [Fact]
    public async Task ResolveAsync_NoConfigStore_NeverCallsDaprConfigStore() {
        // Arrange — default options (ConfigStoreName = null)
        DomainServiceResolver resolver = CreateResolver();

        // Act
        _ = await resolver.ResolveAsync("tenant-a", "orders");

        // Assert — DaprClient.GetConfiguration should never be called
        _ = await _daprClient.DidNotReceive().GetConfiguration(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_ConventionRouting_UsesVersionFromParameter() {
        // Arrange
        DomainServiceResolver resolver = CreateResolver();

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "payments", "v3");

        // Assert
        _ = result.ShouldNotBeNull();
        result.AppId.ShouldBe("payments");
        result.MethodName.ShouldBe("process");
        result.Version.ShouldBe("v3");
    }

    [Fact]
    public async Task ResolveAsync_StaticRegistration_OverridesConvention() {
        // Arrange — static registration maps "orders" to a different AppId
        var opts = new DomainServiceOptions {
            Registrations = new Dictionary<string, DomainServiceRegistration> {
                ["tenant-a:orders:v1"] = new("custom-orders-svc", "custom-process", "tenant-a", "orders", "v1"),
            },
        };
        DomainServiceResolver resolver = CreateResolver(opts);

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "orders");

        // Assert — static registration wins over convention
        _ = result.ShouldNotBeNull();
        result.AppId.ShouldBe("custom-orders-svc");
        result.MethodName.ShouldBe("custom-process");
    }

    // --- Wildcard tenant static registrations ---

    [Fact]
    public async Task ResolveAsync_WildcardTenantRegistration_MatchesAnyTenant() {
        // Arrange — single wildcard registration covers tenant-a, tenant-b, system, ...
        var opts = new DomainServiceOptions {
            Registrations = new Dictionary<string, DomainServiceRegistration> {
                ["*|counter|v1"] = new("sample", "process", "*", "counter", "v1"),
            },
        };
        DomainServiceResolver resolver = CreateResolver(opts);

        // Act
        DomainServiceRegistration? a = await resolver.ResolveAsync("tenant-a", "counter");
        DomainServiceRegistration? b = await resolver.ResolveAsync("tenant-b", "counter");
        DomainServiceRegistration? sys = await resolver.ResolveAsync("system", "counter");

        // Assert — every tenant resolves to the same AppId, but TenantId is rewritten to the actual caller
        _ = a.ShouldNotBeNull();
        a.AppId.ShouldBe("sample");
        a.MethodName.ShouldBe("process");
        a.TenantId.ShouldBe("tenant-a");

        _ = b.ShouldNotBeNull();
        b.AppId.ShouldBe("sample");
        b.TenantId.ShouldBe("tenant-b");

        _ = sys.ShouldNotBeNull();
        sys.AppId.ShouldBe("sample");
        sys.TenantId.ShouldBe("system");
    }

    [Fact]
    public async Task ResolveAsync_ExactRegistration_TakesPrecedenceOverWildcard() {
        // Arrange — both an exact and a wildcard registration exist for the same domain
        var opts = new DomainServiceOptions {
            Registrations = new Dictionary<string, DomainServiceRegistration> {
                ["*|counter|v1"] = new("sample", "process", "*", "counter", "v1"),
                ["tenant-a|counter|v1"] = new("special-tenant-a-svc", "custom-process", "tenant-a", "counter", "v1"),
            },
        };
        DomainServiceResolver resolver = CreateResolver(opts);

        // Act
        DomainServiceRegistration? a = await resolver.ResolveAsync("tenant-a", "counter");
        DomainServiceRegistration? b = await resolver.ResolveAsync("tenant-b", "counter");

        // Assert — tenant-a uses the exact override, tenant-b falls through to wildcard
        _ = a.ShouldNotBeNull();
        a.AppId.ShouldBe("special-tenant-a-svc");
        a.MethodName.ShouldBe("custom-process");

        _ = b.ShouldNotBeNull();
        b.AppId.ShouldBe("sample");
        b.MethodName.ShouldBe("process");
        b.TenantId.ShouldBe("tenant-b");
    }

    [Fact]
    public async Task ResolveAsync_NoWildcardForDomain_FallsThroughToConvention() {
        // Arrange — wildcard exists for "counter" but caller asks for "orders"
        var opts = new DomainServiceOptions {
            Registrations = new Dictionary<string, DomainServiceRegistration> {
                ["*|counter|v1"] = new("sample", "process", "*", "counter", "v1"),
            },
        };
        DomainServiceResolver resolver = CreateResolver(opts);

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-b", "orders");

        // Assert — convention fallback: AppId = domain
        _ = result.ShouldNotBeNull();
        result.AppId.ShouldBe("orders");
        result.MethodName.ShouldBe("process");
        result.TenantId.ShouldBe("tenant-b");
    }

    [Fact]
    public async Task ResolveAsync_WildcardRegistration_RespectsVersionInKey() {
        // Arrange — wildcard registration only exists for v2, caller asks for v1
        var opts = new DomainServiceOptions {
            Registrations = new Dictionary<string, DomainServiceRegistration> {
                ["*|counter|v2"] = new("sample-v2", "process", "*", "counter", "v2"),
            },
        };
        DomainServiceResolver resolver = CreateResolver(opts);

        // Act
        DomainServiceRegistration? v1 = await resolver.ResolveAsync("tenant-a", "counter", "v1");
        DomainServiceRegistration? v2 = await resolver.ResolveAsync("tenant-a", "counter", "v2");

        // Assert — v1 falls through to convention, v2 hits the wildcard
        _ = v1.ShouldNotBeNull();
        v1.AppId.ShouldBe("counter"); // convention
        _ = v2.ShouldNotBeNull();
        v2.AppId.ShouldBe("sample-v2"); // wildcard
    }

    // --- Config store routing (opt-in via ConfigStoreName) ---

    [Fact]
    public async Task ResolveAsync_ConfigStoreConfigured_RegisteredService_ReturnsRegistration() {
        // Arrange
        var registration = new DomainServiceRegistration("orders-svc", "process-command", "tenant-a", "orders", "v1");
        string json = JsonSerializer.Serialize(registration);
        ConfigureConfigStore("tenant-a:orders:v1", json);
        DomainServiceResolver resolver = CreateResolverWithConfigStore();

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "orders");

        // Assert
        _ = result.ShouldNotBeNull();
        result.AppId.ShouldBe("orders-svc");
        result.MethodName.ShouldBe("process-command");
    }

    [Fact]
    public async Task ResolveAsync_ConfigStoreConfigured_UnregisteredService_ReturnsConventionRouting() {
        // Arrange — config store returns no match
        ConfigureConfigStore("tenant-a:orders:v1", null);
        DomainServiceResolver resolver = CreateResolverWithConfigStore();

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "orders");

        // Assert — falls through to convention
        _ = result.ShouldNotBeNull();
        result.AppId.ShouldBe("orders");
        result.MethodName.ShouldBe("process");
    }

    [Fact]
    public async Task ResolveAsync_ConfigStoreConfigured_UsesCorrectConfigKeyWithVersion() {
        // Arrange
        ConfigureConfigStore("my-tenant:my-domain:v1", null);
        DomainServiceResolver resolver = CreateResolverWithConfigStore();

        // Act
        _ = await resolver.ResolveAsync("my-tenant", "my-domain");

        // Assert
        _ = await _daprClient.Received(1).GetConfiguration(
            "configstore",
            Arg.Is<IReadOnlyList<string>>(keys => keys.Contains("my-tenant:my-domain:v1")),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_ConfigStoreConfigured_ExplicitVersion_UsesVersionInKey() {
        // Arrange
        var registration = new DomainServiceRegistration("orders-svc-v2", "process-command", "tenant-a", "orders", "v2");
        string json = JsonSerializer.Serialize(registration);
        ConfigureConfigStore("tenant-a:orders:v2", json);
        DomainServiceResolver resolver = CreateResolverWithConfigStore();

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "orders", "v2");

        // Assert
        _ = result.ShouldNotBeNull();
        result.AppId.ShouldBe("orders-svc-v2");
        _ = await _daprClient.Received(1).GetConfiguration(
            "configstore",
            Arg.Is<IReadOnlyList<string>>(keys => keys.Contains("tenant-a:orders:v2")),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    // --- Input validation ---

    [Fact]
    public async Task ResolveAsync_NullTenantId_ThrowsArgumentException() {
        // Arrange
        DomainServiceResolver resolver = CreateResolver();

        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentException>(() => resolver.ResolveAsync(null!, "domain"));
    }

    [Fact]
    public async Task ResolveAsync_NullDomain_ThrowsArgumentException() {
        // Arrange
        DomainServiceResolver resolver = CreateResolver();

        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentException>(() => resolver.ResolveAsync("tenant", null!));
    }

    [Fact]
    public async Task ResolveAsync_NullVersion_ThrowsArgumentException() {
        // Arrange
        DomainServiceResolver resolver = CreateResolver();

        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentException>(() => resolver.ResolveAsync("tenant", "domain", null!));
    }

    // --- Config store error handling (only when config store is enabled) ---

    [Fact]
    public async Task ResolveAsync_ConfigStoreConfigured_EmptyConfigValue_ReturnsConventionRouting() {
        // Arrange
        var items = new Dictionary<string, ConfigurationItem> {
            ["tenant-a:orders:v1"] = new ConfigurationItem("  ", "1", new Dictionary<string, string>()),
        };
        var response = new GetConfigurationResponse(items);
        _ = _daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(response);
        DomainServiceResolver resolver = CreateResolverWithConfigStore();

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "orders");

        // Assert — falls through to convention
        _ = result.ShouldNotBeNull();
        result.AppId.ShouldBe("orders");
        result.MethodName.ShouldBe("process");
    }

    [Fact]
    public async Task ResolveAsync_ConfigStoreConfigured_MalformedJson_ThrowsDomainServiceException() {
        // Arrange - corrupted config store entry should throw, not silently return null
        ConfigureConfigStore("tenant-a:orders:v1", "NOT-VALID-JSON{{{");
        DomainServiceResolver resolver = CreateResolverWithConfigStore();

        // Act & Assert
        DomainServiceException ex = await Should.ThrowAsync<DomainServiceException>(() => resolver.ResolveAsync("tenant-a", "orders"));
        ex.Message.ShouldContain("corrupted configuration");
        ex.Message.ShouldContain("tenant-a");
        _ = ex.InnerException.ShouldBeOfType<System.Text.Json.JsonException>();
    }

    [Fact]
    public async Task ResolveAsync_ConfigStoreConfigured_JsonNull_ReturnsConventionRouting() {
        // Arrange
        ConfigureConfigStore("tenant-a:orders:v1", "null");
        DomainServiceResolver resolver = CreateResolverWithConfigStore();

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "orders");

        // Assert — falls through to convention
        _ = result.ShouldNotBeNull();
        result.AppId.ShouldBe("orders");
        result.MethodName.ShouldBe("process");
    }

    // --- Multi-tenant multi-domain routing (with config store) ---

    [Fact]
    public async Task ResolveAsync_ConfigStoreConfigured_MultipleTenantsSameDomain_RoutesToCorrectService() {
        // Arrange - two tenants, same domain, different service endpoints
        var regTenantA = new DomainServiceRegistration("orders-tenant-a", "process-command", "tenant-a", "orders", "v1");
        var regTenantB = new DomainServiceRegistration("orders-tenant-b", "process-command", "tenant-b", "orders", "v1");

        _ = _daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(keys => keys.Contains("tenant-a:orders:v1")),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetConfigurationResponse(new Dictionary<string, ConfigurationItem> {
                ["tenant-a:orders:v1"] = new(JsonSerializer.Serialize(regTenantA), "1", new Dictionary<string, string>()),
            }));

        _ = _daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(keys => keys.Contains("tenant-b:orders:v1")),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetConfigurationResponse(new Dictionary<string, ConfigurationItem> {
                ["tenant-b:orders:v1"] = new(JsonSerializer.Serialize(regTenantB), "1", new Dictionary<string, string>()),
            }));

        DomainServiceResolver resolver = CreateResolverWithConfigStore();

        // Act
        DomainServiceRegistration? resultA = await resolver.ResolveAsync("tenant-a", "orders");
        DomainServiceRegistration? resultB = await resolver.ResolveAsync("tenant-b", "orders");

        // Assert
        _ = resultA.ShouldNotBeNull();
        resultA.AppId.ShouldBe("orders-tenant-a");
        _ = resultB.ShouldNotBeNull();
        resultB.AppId.ShouldBe("orders-tenant-b");
    }

    [Fact]
    public async Task ResolveAsync_ConfigStoreConfigured_SameTenantMultipleDomains_RoutesToCorrectService() {
        // Arrange - same tenant, two domains, different service endpoints
        var regOrders = new DomainServiceRegistration("orders-svc", "process-command", "tenant-a", "orders", "v1");
        var regInventory = new DomainServiceRegistration("inventory-svc", "process-command", "tenant-a", "inventory", "v1");

        _ = _daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(keys => keys.Contains("tenant-a:orders:v1")),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetConfigurationResponse(new Dictionary<string, ConfigurationItem> {
                ["tenant-a:orders:v1"] = new(JsonSerializer.Serialize(regOrders), "1", new Dictionary<string, string>()),
            }));

        _ = _daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<string>>(keys => keys.Contains("tenant-a:inventory:v1")),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetConfigurationResponse(new Dictionary<string, ConfigurationItem> {
                ["tenant-a:inventory:v1"] = new(JsonSerializer.Serialize(regInventory), "1", new Dictionary<string, string>()),
            }));

        DomainServiceResolver resolver = CreateResolverWithConfigStore();

        // Act
        DomainServiceRegistration? resultOrders = await resolver.ResolveAsync("tenant-a", "orders");
        DomainServiceRegistration? resultInventory = await resolver.ResolveAsync("tenant-a", "inventory");

        // Assert
        _ = resultOrders.ShouldNotBeNull();
        resultOrders.AppId.ShouldBe("orders-svc");
        _ = resultInventory.ShouldNotBeNull();
        resultInventory.AppId.ShouldBe("inventory-svc");
    }

    // --- No-caching verification (with config store) ---

    [Fact]
    public async Task ResolveAsync_ConfigStoreConfigured_NoCaching_GetConfigurationCalledEveryInvocation() {
        // Arrange
        var registration = new DomainServiceRegistration("orders-svc", "process-command", "tenant-a", "orders", "v1");
        string json = JsonSerializer.Serialize(registration);
        ConfigureConfigStore("tenant-a:orders:v1", json);
        DomainServiceResolver resolver = CreateResolverWithConfigStore();

        // Act - invoke twice with same tenant+domain
        _ = await resolver.ResolveAsync("tenant-a", "orders");
        _ = await resolver.ResolveAsync("tenant-a", "orders");

        // Assert - GetConfiguration must have been called TWICE, not cached
        _ = await _daprClient.Received(2).GetConfiguration(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    // --- Version format validation ---

    [Theory]
    [InlineData("version1")]
    [InlineData("v1a")]
    [InlineData("v1:evil")]
    [InlineData("v")]
    [InlineData("1")]
    public async Task ResolveAsync_InvalidVersionFormat_ThrowsArgumentException(string invalidVersion) {
        // Arrange
        DomainServiceResolver resolver = CreateResolver();

        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentException>(() => resolver.ResolveAsync("tenant", "domain", invalidVersion));
    }

    [Fact]
    public async Task ResolveAsync_UppercaseVersion_NormalizesToLowercase() {
        // Arrange
        var registration = new DomainServiceRegistration("svc-v2", "process-command", "tenant-a", "orders", "v2");
        string json = JsonSerializer.Serialize(registration);
        ConfigureConfigStore("tenant-a:orders:v2", json);
        DomainServiceResolver resolver = CreateResolverWithConfigStore();

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "orders", "V2");

        // Assert
        _ = result.ShouldNotBeNull();
        result.AppId.ShouldBe("svc-v2");
        _ = await _daprClient.Received(1).GetConfiguration(
            "configstore",
            Arg.Is<IReadOnlyList<string>>(keys => keys.Contains("tenant-a:orders:v2")),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_ConfigStoreConfigured_NoCache_EachCallQueriesConfigStore() {
        // Arrange - first call returns no match (convention), second returns registration
        DomainServiceResolver resolver = CreateResolverWithConfigStore();

        _ = _daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(
                new GetConfigurationResponse(new Dictionary<string, ConfigurationItem>()),
                new GetConfigurationResponse(new Dictionary<string, ConfigurationItem> {
                    ["new-tenant:orders:v1"] = new(
                        JsonSerializer.Serialize(new DomainServiceRegistration("new-svc", "process", "new-tenant", "orders", "v1")),
                        "1",
                        new Dictionary<string, string>()),
                }));

        // Act
        DomainServiceRegistration? result1 = await resolver.ResolveAsync("new-tenant", "orders");
        DomainServiceRegistration? result2 = await resolver.ResolveAsync("new-tenant", "orders");

        // Assert — first call falls through to convention, second finds config store entry
        _ = result1.ShouldNotBeNull();
        result1.AppId.ShouldBe("orders"); // convention
        _ = result2.ShouldNotBeNull();
        result2.AppId.ShouldBe("new-svc"); // config store
    }
}
