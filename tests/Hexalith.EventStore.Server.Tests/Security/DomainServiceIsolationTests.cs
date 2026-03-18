
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
/// Story 5.3 review follow-up tests for tenant-scoped domain service lookup and routing behavior.
/// </summary>
public class DomainServiceIsolationTests {
    private static DomainServiceOptions DefaultOptions => new() {
        ConfigStoreName = "configstore",
        MaxEventsPerResult = 100,
        MaxEventSizeBytes = 1_048_576,
    };

    // --- Resolver requests the exact tenant/domain/version config key ---

    [Theory]
    [InlineData("tenant-a", "orders", "v1", "tenant-a:orders:v1")]
    [InlineData("tenant-b", "orders", "v1", "tenant-b:orders:v1")]
    [InlineData("acme", "inventory", "v2", "acme:inventory:v2")]
    [InlineData("tenant-a", "orders", "V1", "tenant-a:orders:v1")]
    public async Task DomainServiceResolver_TenantScopedLookup_UsesCorrectConfigKey(
        string tenantId,
        string domain,
        string version,
        string expectedConfigKey) {
        // Arrange
        IReadOnlyList<string>? capturedKeys = null;

        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Do<IReadOnlyList<string>>(keys => capturedKeys = [.. keys]),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetConfigurationResponse(new Dictionary<string, ConfigurationItem>()));

        var resolver = new DomainServiceResolver(daprClient, Options.Create(DefaultOptions), NullLogger<DomainServiceResolver>.Instance);

        // Act
        _ = await resolver.ResolveAsync(tenantId, domain, version);

        // Assert
        _ = capturedKeys.ShouldNotBeNull();
        capturedKeys.Count.ShouldBe(1);
        capturedKeys[0].ShouldBe(expectedConfigKey);
    }

    // --- Different tenants resolve different registrations ---

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

    // --- Invoker passes tenant, domain, and default version to the resolver ---

    [Fact]
    public async Task DaprDomainServiceInvoker_PassesTenantDomainAndDefaultVersionToResolver() {
        // Arrange -- verify the invoker passes the command routing context through unchanged.
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        _ = resolver.ResolveAsync("tenant-a", "orders", "v1", Arg.Any<CancellationToken>())
            .Returns((DomainServiceRegistration?)null);

        DaprClient daprClient = Substitute.For<DaprClient>();
        var invoker = new DaprDomainServiceInvoker(daprClient, resolver, Options.Create(DefaultOptions), NullLogger<DaprDomainServiceInvoker>.Instance);
        var command = new CommandEnvelope("msg-iso-1", "tenant-a", "orders", "order-001", "CreateOrder", [1],
            Guid.NewGuid().ToString(), null, "system", null);

        // Act & Assert -- resolver returns null so exception expected
        _ = await Should.ThrowAsync<DomainServiceNotFoundException>(
            () => invoker.InvokeAsync(command, null));

        // Verify resolver was called with the command's tenant, domain, and default version.
        _ = await resolver.Received(1).ResolveAsync("tenant-a", "orders", "v1", Arg.Any<CancellationToken>());
    }

    // --- Tenant+domain routing stays scoped to the configured combination ---

    [Fact]
    public async Task FakeDomainServiceInvoker_TenantAndDomainResponses_RouteToExactConfiguredCombination() {
        // Arrange
        var fakeInvoker = new FakeDomainServiceInvoker();
        var tenantAOrders = DomainResult.NoOp();
        var tenantAInventory = DomainResult.Success([new TestEvent()]);
        var tenantBOrders = DomainResult.Rejection([new TestRejectionEvent()]);

        fakeInvoker.SetupResponse("tenant-a", "orders", tenantAOrders);
        fakeInvoker.SetupResponse("tenant-a", "inventory", tenantAInventory);
        fakeInvoker.SetupResponse("tenant-b", "orders", tenantBOrders);

        var commandA = new CommandEnvelope("msg-iso-2", "tenant-a", "orders", "order-001", "CreateOrder", [1],
            Guid.NewGuid().ToString(), null, "system", null);
        var commandB = new CommandEnvelope("msg-iso-3", "tenant-a", "inventory", "item-001", "AdjustInventory", [1],
            Guid.NewGuid().ToString(), null, "system", null);
        var commandC = new CommandEnvelope("msg-iso-4", "tenant-b", "orders", "order-001", "CreateOrder", [1],
            Guid.NewGuid().ToString(), null, "system", null);

        // Act
        DomainResult actualA = await fakeInvoker.InvokeAsync(commandA, null);
        DomainResult actualB = await fakeInvoker.InvokeAsync(commandB, null);
        DomainResult actualC = await fakeInvoker.InvokeAsync(commandC, null);

        // Assert
        actualA.IsNoOp.ShouldBeTrue();
        actualB.IsSuccess.ShouldBeTrue();
        actualC.IsRejection.ShouldBeTrue();
        fakeInvoker.Invocations.Count.ShouldBe(3);
        fakeInvoker.Invocations[0].TenantId.ShouldBe("tenant-a");
        fakeInvoker.Invocations[0].Domain.ShouldBe("orders");
        fakeInvoker.Invocations[1].TenantId.ShouldBe("tenant-a");
        fakeInvoker.Invocations[1].Domain.ShouldBe("inventory");
        fakeInvoker.Invocations[2].TenantId.ShouldBe("tenant-b");
        fakeInvoker.Invocations[2].Domain.ShouldBe("orders");
    }

    [Fact]
    public async Task FakeDomainServiceInvoker_UnconfiguredTenantAndDomain_ThrowsInvalidOperationException() {
        // Arrange
        var fakeInvoker = new FakeDomainServiceInvoker();
        fakeInvoker.SetupResponse("tenant-a", "orders", DomainResult.NoOp());

        var unconfiguredCommand = new CommandEnvelope(
            "msg-iso-unconfigured",
            "tenant-b",
            "inventory",
            "item-404",
            "AdjustInventory",
            [1],
            Guid.NewGuid().ToString(),
            null,
            "system",
            null);

        // Act / Assert
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => fakeInvoker.InvokeAsync(unconfiguredCommand, null));
        exception.Message.ShouldContain("No response configured");
    }

    // --- Empty config response means no registration was found ---

    [Fact]
    public async Task DomainServiceResolver_EmptyConfigResponse_ReturnsNull() {
        // Arrange -- config store responds successfully but has no matching registration.
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
    public async Task DomainServiceResolver_ConfigStoreUnavailable_PropagatesException() {
        // Arrange -- config store is unavailable, which differs from a clean miss.
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GetConfigurationResponse>(new InvalidOperationException("config store unavailable")));

        var resolver = new DomainServiceResolver(daprClient, Options.Create(DefaultOptions), NullLogger<DomainServiceResolver>.Instance);

        // Act / Assert
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => resolver.ResolveAsync("tenant-a", "orders", "v1"));
        exception.Message.ShouldContain("config store unavailable");
    }

    [Fact]
    public async Task DomainServiceResolver_MalformedRegistration_ThrowsDomainServiceException() {
        // Arrange
        const string configKey = "tenant-a:orders:v1";
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetConfigurationResponse(
                new Dictionary<string, ConfigurationItem> {
                    [configKey] = new("{ not-json }", "1", new Dictionary<string, string>()),
                }));

        var resolver = new DomainServiceResolver(daprClient, Options.Create(DefaultOptions), NullLogger<DomainServiceResolver>.Instance);

        // Act / Assert
        DomainServiceException exception = await Should.ThrowAsync<DomainServiceException>(
            () => resolver.ResolveAsync("tenant-a", "orders", "v1"));
        exception.Message.ShouldContain("tenant 'tenant-a'");
        exception.Message.ShouldContain("domain 'orders'");
        exception.Message.ShouldContain(configKey);
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

    // --- Repeated tenant lookups request only the exact keyed registration each time ---

    [Fact]
    public async Task DomainServiceResolver_SameDomainDifferentTenants_QueriesDifferentConfigKeys() {
        // Arrange
        var capturedKeyRequests = new List<string[]>();
        DaprClient daprClient = Substitute.For<DaprClient>();
        _ = daprClient.GetConfiguration(
            Arg.Any<string>(),
            Arg.Do<IReadOnlyList<string>>(keys => {
                lock (capturedKeyRequests) {
                    capturedKeyRequests.Add([.. keys]);
                }
            }),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(new GetConfigurationResponse(new Dictionary<string, ConfigurationItem>()));

        var resolver = new DomainServiceResolver(daprClient, Options.Create(DefaultOptions), NullLogger<DomainServiceResolver>.Instance);

        // Act
        _ = await resolver.ResolveAsync("tenant-a", "orders", "v1");
        _ = await resolver.ResolveAsync("tenant-b", "orders", "v1");

        // Assert
        capturedKeyRequests.Count.ShouldBe(2);
        capturedKeyRequests[0].ShouldBe(["tenant-a:orders:v1"]);
        capturedKeyRequests[1].ShouldBe(["tenant-b:orders:v1"]);
    }

    // --- IDomainServiceInvoker does not accept metadata parameters ---

    [Fact]
    public void DomainServiceInvoker_InvokeAsync_DoesNotAcceptMetadataParameters() {
        // Guard test: if someone adds metadata parameters to InvokeAsync, domain services
        // could influence event metadata, breaking SEC-1 ownership guarantee.
        System.Reflection.MethodInfo method = typeof(IDomainServiceInvoker)
            .GetMethod(nameof(IDomainServiceInvoker.InvokeAsync))!;

        method.ShouldNotBeNull();
        System.Reflection.ParameterInfo[] parameters = method.GetParameters();

        // Expected: (CommandEnvelope command, object? currentState, CancellationToken cancellationToken)
        parameters.Length.ShouldBe(3, "InvokeAsync should only accept command, state, and cancellation token");
        parameters[0].ParameterType.ShouldBe(typeof(CommandEnvelope));
        parameters[1].ParameterType.ShouldBe(typeof(object));
        parameters[2].ParameterType.ShouldBe(typeof(CancellationToken));
    }

    private sealed record TestEvent : Hexalith.EventStore.Contracts.Events.IEventPayload;

    private sealed record TestRejectionEvent : Hexalith.EventStore.Contracts.Events.IRejectionEvent;
}
