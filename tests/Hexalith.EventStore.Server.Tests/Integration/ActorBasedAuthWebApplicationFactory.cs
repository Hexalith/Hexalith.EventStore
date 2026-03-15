extern alias commandapi;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors.Authorization;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using CommandApiProgram = commandapi::Program;

namespace Hexalith.EventStore.Server.Tests.Integration;

/// <summary>
/// Shared <see cref="WebApplicationFactory{TEntryPoint}"/> for Tier 2 integration tests
/// that exercise actor-based authorization flows and 503 failure modes.
/// Configures actor validator names and mocks <see cref="IActorProxyFactory"/>
/// so no real DAPR sidecar is needed.
/// </summary>
public class ActorBasedAuthWebApplicationFactory : WebApplicationFactory<CommandApiProgram> {
    /// <summary>Gets the fake tenant validator actor for per-test configuration.</summary>
    public FakeTenantValidatorActor FakeTenantActor { get; } = new();

    /// <summary>Gets the fake RBAC validator actor for per-test configuration.</summary>
    public FakeRbacValidatorActor FakeRbacActor { get; } = new();

    /// <summary>Gets the mock actor proxy factory.</summary>
    public IActorProxyFactory MockProxyFactory { get; } = Substitute.For<IActorProxyFactory>();

    /// <summary>Resets fake actor state between tests to avoid cross-test leakage.</summary>
    public void ResetActors() {
        FakeTenantActor.Reset();
        FakeRbacActor.Reset();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.UseEnvironment("Development");

        _ = builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["EventStore:Authorization:TenantValidatorActorName"] = "TestTenantValidatorActor",
            ["EventStore:Authorization:RbacValidatorActorName"] = "TestRbacValidatorActor",
        }));

        _ = builder.ConfigureTestServices(services => {
            // Remove existing IActorProxyFactory registration (from AddDaprClient)
            // and replace with our mock that returns configurable fake actors
            ServiceDescriptor? existingFactory = services.FirstOrDefault(
                d => d.ServiceType == typeof(IActorProxyFactory));
            if (existingFactory is not null) {
                _ = services.Remove(existingFactory);
            }

            // Configure mock to return fake actors
            _ = MockProxyFactory
                .CreateActorProxy<ITenantValidatorActor>(Arg.Any<ActorId>(), Arg.Any<string>())
                .Returns(FakeTenantActor);

            _ = MockProxyFactory
                .CreateActorProxy<IRbacValidatorActor>(Arg.Any<ActorId>(), Arg.Any<string>())
                .Returns(FakeRbacActor);

            _ = services.AddSingleton(MockProxyFactory);
        });
    }
}
