
using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Security;
/// <summary>
/// Domain service tenant isolation tests verifying tenant-scoped config lookup
/// and correct tenant context in invocations.
/// (AC: #2, #7)
/// </summary>
public class DomainServiceIsolationTests {
    private static DomainServiceOptions DefaultOptions => new() {
        ConfigStoreName = "configstore",
        MaxEventsPerResult = 100,
        MaxEventSizeBytes = 1_048_576,
    };

    // --- Task 2.2: AC #7 ---

    [Theory]
    [InlineData("tenant-a", "orders", "v1")]
    [InlineData("tenant-b", "orders", "v1")]
    [InlineData("acme", "inventory", "v2")]
    public async Task DomainServiceResolver_TenantScopedLookup_UsesCorrectConfigKey(string tenantId, string domain, string version) {
        // Arrange
        string expectedConfigKey = $"{tenantId}:{domain}:{version}";
        string? capturedConfigKey = null;

        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Do<IReadOnlyList<string>>(keys => capturedConfigKey = keys.FirstOrDefault()),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetConfigurationResponse(new Dictionary<string, ConfigurationItem>()));

        var resolver = new DomainServiceResolver(daprClient, Options.Create(DefaultOptions), NullLogger<DomainServiceResolver>.Instance);

        // Act
        _ = await resolver.ResolveAsync(tenantId, domain, version);

        // Assert
        capturedConfigKey.ShouldBe(expectedConfigKey);
    }

    // --- Task 2.3: AC #7 ---

    [Fact]
    public async Task DomainServiceResolver_DifferentTenants_ResolveDifferentRegistrations() {
        // Arrange
        var regA = new DomainServiceRegistration("service-a", "process", "tenant-a", "orders", "v1");
        var regB = new DomainServiceRegistration("service-b", "process", "tenant-b", "orders", "v1");

        var configItems = new Dictionary<string, ConfigurationItem> {
            ["tenant-a:orders:v1"] = new(JsonSerializer.Serialize(regA), "1", new Dictionary<string, string>()),
            ["tenant-b:orders:v1"] = new(JsonSerializer.Serialize(regB), "1", new Dictionary<string, string>()),
        };

        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo => {
                IReadOnlyList<string> keys = callInfo.ArgAt<IReadOnlyList<string>>(1);
                string key = keys[0];
                Dictionary<string, ConfigurationItem> items = configItems.ContainsKey(key)
                    ? new Dictionary<string, ConfigurationItem> { [key] = configItems[key] }
                    : [];
                return new GetConfigurationResponse(items);
            });

        var resolver = new DomainServiceResolver(daprClient, Options.Create(DefaultOptions), NullLogger<DomainServiceResolver>.Instance);

        // Act
        DomainServiceRegistration? resultA = await resolver.ResolveAsync("tenant-a", "orders", "v1");
        DomainServiceRegistration? resultB = await resolver.ResolveAsync("tenant-b", "orders", "v1");

        // Assert
        _ = resultA.ShouldNotBeNull();
        _ = resultB.ShouldNotBeNull();
        resultA.AppId.ShouldBe("service-a");
        resultB.AppId.ShouldBe("service-b");
        resultA.AppId.ShouldNotBe(resultB.AppId);
    }

    // --- Task 2.4: AC #2 ---

    [Fact]
    public async Task DaprDomainServiceInvoker_PassesTenantContextToResolver() {
        // Arrange -- verify the invoker passes the correct tenant to the resolver
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        _ = resolver.ResolveAsync("tenant-a", "orders", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DomainServiceRegistration?)null);

        DaprClient daprClient = Substitute.For<DaprClient>();
        var invoker = new DaprDomainServiceInvoker(daprClient, resolver, Options.Create(DefaultOptions), NullLogger<DaprDomainServiceInvoker>.Instance);
        var command = new CommandEnvelope("msg-iso-1", "tenant-a", "orders", "order-001", "CreateOrder", [1],
            Guid.NewGuid().ToString(), null, "system", null);

        // Act & Assert -- resolver returns null so exception expected
        _ = await Should.ThrowAsync<DomainServiceNotFoundException>(
            () => invoker.InvokeAsync(command, null));

        // Verify resolver was called with the command's tenant
        _ = await resolver.Received(1).ResolveAsync("tenant-a", "orders", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // --- Task 2.5: AC #2 ---

    [Fact]
    public async Task FakeDomainServiceInvoker_TenantDomainResponses_RoutedCorrectly() {
        // Arrange
        var fakeInvoker = new FakeDomainServiceInvoker();
        var resultA = DomainResult.NoOp();
        var resultB = DomainResult.Success([new TestEvent()]);

        fakeInvoker.SetupResponse("tenant-a", "orders", resultA);
        fakeInvoker.SetupResponse("tenant-b", "orders", resultB);

        var commandA = new CommandEnvelope("msg-iso-2", "tenant-a", "orders", "order-001", "CreateOrder", [1],
            Guid.NewGuid().ToString(), null, "system", null);
        var commandB = new CommandEnvelope("msg-iso-3", "tenant-b", "orders", "order-001", "CreateOrder", [1],
            Guid.NewGuid().ToString(), null, "system", null);

        // Act
        DomainResult actualA = await fakeInvoker.InvokeAsync(commandA, null);
        DomainResult actualB = await fakeInvoker.InvokeAsync(commandB, null);

        // Assert
        actualA.IsNoOp.ShouldBeTrue();
        actualB.IsSuccess.ShouldBeTrue();
        fakeInvoker.Invocations.Count.ShouldBe(2);
        fakeInvoker.Invocations[0].TenantId.ShouldBe("tenant-a");
        fakeInvoker.Invocations[1].TenantId.ShouldBe("tenant-b");
    }

    // --- Task 2.6: GAP-F2 ---

    [Fact]
    public async Task DomainServiceResolver_ConfigStoreUnavailable_ReturnsNull() {
        // Arrange -- config store returns empty items for a valid tenant+domain
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetConfigurationResponse(new Dictionary<string, ConfigurationItem>()));

        var resolver = new DomainServiceResolver(daprClient, Options.Create(DefaultOptions), NullLogger<DomainServiceResolver>.Instance);

        // Act
        DomainServiceRegistration? result = await resolver.ResolveAsync("tenant-a", "orders", "v1");

        // Assert -- returns null (no silent fallback to shared/default service)
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DaprDomainServiceInvoker_ResolverReturnsNull_ThrowsDomainServiceNotFoundException() {
        // Arrange
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        _ = resolver.ResolveAsync("tenant-a", "orders", "v1", Arg.Any<CancellationToken>())
            .Returns((DomainServiceRegistration?)null);

        DaprClient daprClient = Substitute.For<DaprClient>();
        var invoker = new DaprDomainServiceInvoker(daprClient, resolver, Options.Create(DefaultOptions), NullLogger<DaprDomainServiceInvoker>.Instance);

        var command = new CommandEnvelope("msg-iso-4", "tenant-a", "orders", "order-001", "CreateOrder", [1],
            Guid.NewGuid().ToString(), null, "system", null);

        // Act & Assert
        _ = await Should.ThrowAsync<DomainServiceNotFoundException>(
            () => invoker.InvokeAsync(command, null));
    }

    // --- Task 2.7: AC #7, GAP-PM1 ---

    [Fact]
    public async Task DomainServiceResolver_SameDomainDifferentTenants_QueriesDifferentConfigKeys() {
        // Arrange
        var capturedKeys = new List<string>();
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Do<IReadOnlyList<string>>(keys => { lock (capturedKeys) { capturedKeys.AddRange(keys); } }),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetConfigurationResponse(new Dictionary<string, ConfigurationItem>()));

        var resolver = new DomainServiceResolver(daprClient, Options.Create(DefaultOptions), NullLogger<DomainServiceResolver>.Instance);

        // Act
        _ = await resolver.ResolveAsync("tenant-a", "orders", "v1");
        _ = await resolver.ResolveAsync("tenant-b", "orders", "v1");

        // Assert
        capturedKeys.Count.ShouldBe(2);
        capturedKeys.ShouldContain("tenant-a:orders:v1");
        capturedKeys.ShouldContain("tenant-b:orders:v1");
        capturedKeys[0].ShouldNotBe(capturedKeys[1]);
    }

    private sealed record TestEvent : Hexalith.EventStore.Contracts.Events.IEventPayload;
}
