
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Server.DomainServices;

using Microsoft.Extensions.Configuration;
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveAsync_ConfigStoreNameMissingOrWhitespace_SkipsDaprConfigStore(string? configStoreName) {
        // Arrange
        DomainServiceResolver resolver = CreateResolver(new DomainServiceOptions { ConfigStoreName = configStoreName });

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "orders");

        // Assert
        _ = result.ShouldNotBeNull();
        result.AppId.ShouldBe("orders");
        _ = await _daprClient.DidNotReceive().GetConfiguration(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_ConfigStoreLookupCanceled_PropagatesCancellation() {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _ = _daprClient.GetConfiguration(
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromCanceled<GetConfigurationResponse>(cancellation.Token));
        DomainServiceResolver resolver = CreateResolverWithConfigStore();

        _ = await Should.ThrowAsync<OperationCanceledException>(() =>
            resolver.ResolveAsync(
                "tenant-a",
                "orders",
                cancellationToken: cancellation.Token));
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
    public async Task ResolveAsync_ColonExactRegistration_TakesPrecedenceOverEveryFallback() {
        // Arrange
        var configRegistration = new DomainServiceRegistration("config-svc", "process", "tenant-a", "party", "v1");
        ConfigureConfigStore("tenant-a:party:v1", JsonSerializer.Serialize(configRegistration));
        var opts = new DomainServiceOptions {
            ConfigStoreName = "configstore",
            Registrations = new Dictionary<string, DomainServiceRegistration> {
                ["tenant-a:party:v1"] = new("colon-exact-svc", "process", "tenant-a", "party", "v1"),
                ["tenant-a|party|v1"] = new("pipe-exact-svc", "process", "tenant-a", "party", "v1"),
                ["*|party|v1"] = new("pipe-wildcard-svc", "process", "*", "party", "v1"),
                ["wildcard_party_v1"] = new("sanitized-wildcard-svc", "process", "*", "party", "v1"),
            },
        };
        DomainServiceResolver resolver = CreateResolver(opts);

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "party");

        // Assert
        _ = result.ShouldNotBeNull();
        result.AppId.ShouldBe("colon-exact-svc");
        _ = await _daprClient.DidNotReceive().GetConfiguration(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_PipeExactRegistration_TakesPrecedenceOverWildcard() {
        // Arrange
        var opts = new DomainServiceOptions {
            Registrations = new Dictionary<string, DomainServiceRegistration> {
                ["tenant-a|party|v1"] = new("pipe-exact-svc", "process", "tenant-a", "party", "v1"),
                ["*|party|v1"] = new("pipe-wildcard-svc", "process", "*", "party", "v1"),
                ["wildcard_party_v1"] = new("sanitized-wildcard-svc", "process", "*", "party", "v1"),
            },
        };
        DomainServiceResolver resolver = CreateResolver(opts);

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "party");

        // Assert
        _ = result.ShouldNotBeNull();
        result.AppId.ShouldBe("pipe-exact-svc");
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

    [Fact]
    public async Task ResolveAsync_SanitizedWildcardRegistration_MatchesAnyTenant() {
        // Arrange — Kubernetes-valid sanitized wildcard key shape (Hexalith.Parties story 9.3 AC1).
        // ConfigMap data keys and Pod env names reject '*' and '|', so manifest-emitted dictionary
        // keys use the sanitized form "wildcard_<domain>_<version>". Resolver must match this in
        // addition to the legacy pipe-form "*|domain|version".
        var opts = new DomainServiceOptions {
            Registrations = new Dictionary<string, DomainServiceRegistration> {
                ["wildcard_party_v1"] = new("parties", "process", "*", "party", "v1"),
            },
        };
        DomainServiceResolver resolver = CreateResolver(opts);

        // Act
        DomainServiceRegistration? a = await resolver.ResolveAsync("tenant-a", "party");
        DomainServiceRegistration? b = await resolver.ResolveAsync("tenant-b", "party");

        // Assert — every tenant resolves to the registered AppId; TenantId is rewritten to the caller.
        _ = a.ShouldNotBeNull();
        a.AppId.ShouldBe("parties");
        a.MethodName.ShouldBe("process");
        a.TenantId.ShouldBe("tenant-a");

        _ = b.ShouldNotBeNull();
        b.AppId.ShouldBe("parties");
        b.TenantId.ShouldBe("tenant-b");
    }

    [Fact]
    public async Task ResolveAsync_SanitizedWildcard_RespectsVersionInKey() {
        // Arrange — sanitized wildcard exists only for v2; v1 caller must fall through to convention.
        var opts = new DomainServiceOptions {
            Registrations = new Dictionary<string, DomainServiceRegistration> {
                ["wildcard_counter_v2"] = new("sample-v2", "process", "*", "counter", "v2"),
            },
        };
        DomainServiceResolver resolver = CreateResolver(opts);

        // Act
        DomainServiceRegistration? v1 = await resolver.ResolveAsync("tenant-a", "counter", "v1");
        DomainServiceRegistration? v2 = await resolver.ResolveAsync("tenant-a", "counter", "v2");

        // Assert
        _ = v1.ShouldNotBeNull();
        v1.AppId.ShouldBe("counter"); // convention fallback
        _ = v2.ShouldNotBeNull();
        v2.AppId.ShouldBe("sample-v2"); // sanitized wildcard match
    }

    [Fact]
    public async Task ResolveAsync_PipeWildcardTakesPrecedenceOverSanitizedWildcard() {
        // Arrange — both pipe and sanitized forms exist; pipe form must win (lookup order preserved).
        var opts = new DomainServiceOptions {
            Registrations = new Dictionary<string, DomainServiceRegistration> {
                ["*|party|v1"] = new("legacy-svc", "process", "*", "party", "v1"),
                ["wildcard_party_v1"] = new("sanitized-svc", "process", "*", "party", "v1"),
            },
        };
        DomainServiceResolver resolver = CreateResolver(opts);

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "party");

        // Assert — pipe wildcard wins because it is checked first.
        _ = result.ShouldNotBeNull();
        result.AppId.ShouldBe("legacy-svc");
    }

    [Fact]
    public async Task ResolveAsync_StaticRegistrationTakesPrecedenceOverConfigStore() {
        // Arrange
        var configRegistration = new DomainServiceRegistration("config-party-svc", "process", "tenant-a", "party", "v1");
        ConfigureConfigStore("tenant-a:party:v1", JsonSerializer.Serialize(configRegistration));
        var opts = new DomainServiceOptions {
            ConfigStoreName = "configstore",
            Registrations = new Dictionary<string, DomainServiceRegistration> {
                ["*|party|v1"] = new("static-party-svc", "process", "*", "party", "v1"),
            },
        };
        DomainServiceResolver resolver = CreateResolver(opts);

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "party");

        // Assert
        _ = result.ShouldNotBeNull();
        result.AppId.ShouldBe("static-party-svc");
        _ = await _daprClient.DidNotReceive().GetConfiguration(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DomainServiceOptions_BindsPipeAndSanitizedWildcardRegistrationsFromConfiguration() {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["EventStore:DomainServices:Registrations:*|party|v1:AppId"] = "parties",
                ["EventStore:DomainServices:Registrations:*|party|v1:MethodName"] = "process",
                ["EventStore:DomainServices:Registrations:*|party|v1:TenantId"] = "*",
                ["EventStore:DomainServices:Registrations:*|party|v1:Domain"] = "party",
                ["EventStore:DomainServices:Registrations:*|party|v1:Version"] = "v1",
                ["EventStore:DomainServices:Registrations:wildcard_counter_v1:AppId"] = "sample",
                ["EventStore:DomainServices:Registrations:wildcard_counter_v1:MethodName"] = "process",
                ["EventStore:DomainServices:Registrations:wildcard_counter_v1:TenantId"] = "*",
                ["EventStore:DomainServices:Registrations:wildcard_counter_v1:Domain"] = "counter",
                ["EventStore:DomainServices:Registrations:wildcard_counter_v1:Version"] = "v1",
            })
            .Build();
        var options = new DomainServiceOptions();

        // Act
        configuration.GetSection("EventStore:DomainServices").Bind(options);

        // Assert
        options.Registrations.Keys.ShouldContain("*|party|v1");
        options.Registrations.Keys.ShouldContain("wildcard_counter_v1");
        options.Registrations["*|party|v1"].AppId.ShouldBe("parties");
        options.Registrations["wildcard_counter_v1"].AppId.ShouldBe("sample");
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
    [InlineData("v1_2")]
    [InlineData("v01x")]
    public async Task ResolveAsync_InvalidVersionFormat_ThrowsArgumentException(string invalidVersion) {
        // Arrange
        DomainServiceResolver resolver = CreateResolver();

        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentException>(() => resolver.ResolveAsync("tenant", "domain", invalidVersion));
    }

    [Fact]
    public async Task ResolveAsync_DomainWithUnderscore_CannotReachSanitizedWildcardLookup() {
        // Arrange
        var opts = new DomainServiceOptions {
            Registrations = new Dictionary<string, DomainServiceRegistration> {
                ["wildcard_domain_with_underscore_v1"] = new("unsafe-svc", "process", "*", "domain_with_underscore", "v1"),
            },
        };
        DomainServiceResolver resolver = CreateResolver(opts);

        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentException>(() => resolver.ResolveAsync("tenant", "domain_with_underscore"));
    }

    [Fact]
    public async Task ResolveAsync_DomainLongerThanAggregateIdentityLimit_ThrowsArgumentException() {
        // Arrange
        DomainServiceResolver resolver = CreateResolver();
        string tooLongDomain = new('a', 65);

        // Act & Assert
        _ = await Should.ThrowAsync<ArgumentException>(() => resolver.ResolveAsync("tenant", tooLongDomain));
    }

    [Theory]
    [InlineData("domain-with-hyphen")]
    [InlineData("counter")]
    [InlineData("party")]
    public void AggregateIdentity_DomainValidationDocumentsSanitizedWildcardInvariant(string domain) {
        // Arrange / Act
        var identity = new AggregateIdentity("tenant-a", domain, "aggregate-1");

        // Assert
        identity.Domain.ShouldBe(domain);
    }

    [Fact]
    public void AggregateIdentity_DomainValidationRejectsUnderscoreForSanitizedWildcardSafety() {
        // Act & Assert
        _ = Should.Throw<ArgumentException>(() => new AggregateIdentity("tenant-a", "domain_with_underscore", "aggregate-1"));
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
