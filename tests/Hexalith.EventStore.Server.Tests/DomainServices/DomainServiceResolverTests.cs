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

    [Fact]
    public async Task ResolveAsync_RegisteredService_ReturnsRegistration()
    {
        // Arrange
        var registration = new DomainServiceRegistration("orders-svc", "process-command", "tenant-a", "orders", null);
        string json = JsonSerializer.Serialize(registration);
        ConfigureConfigStore("tenant-a:orders:service", json);
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
        ConfigureConfigStore("tenant-a:orders:service", null);
        var resolver = CreateResolver();

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "orders");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_UsesCorrectConfigKey()
    {
        // Arrange
        ConfigureConfigStore("my-tenant:my-domain:service", null);
        var resolver = CreateResolver();

        // Act
        await resolver.ResolveAsync("my-tenant", "my-domain");

        // Assert
        await _daprClient.Received(1).GetConfiguration(
            "configstore",
            Arg.Is<IReadOnlyList<string>>(keys => keys.Contains("my-tenant:my-domain:service")),
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
    public async Task ResolveAsync_EmptyConfigValue_ReturnsNull()
    {
        // Arrange
        var items = new Dictionary<string, ConfigurationItem>
        {
            ["tenant-a:orders:service"] = new ConfigurationItem("  ", "1", new Dictionary<string, string>()),
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
    public async Task ResolveAsync_MalformedJson_ReturnsNull()
    {
        // Arrange
        ConfigureConfigStore("tenant-a:orders:service", "NOT-VALID-JSON{{{");
        var resolver = CreateResolver();

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "orders");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_JsonNull_ReturnsNull()
    {
        // Arrange
        ConfigureConfigStore("tenant-a:orders:service", "null");
        var resolver = CreateResolver();

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "orders");

        // Assert
        result.ShouldBeNull();
    }
}
