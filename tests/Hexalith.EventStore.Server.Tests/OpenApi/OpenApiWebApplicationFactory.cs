extern alias commandapi;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using CommandApiProgram = commandapi::Program;

namespace Hexalith.EventStore.Server.Tests.OpenApi;

/// <summary>
/// Minimal <see cref="WebApplicationFactory{TEntryPoint}"/> for OpenAPI tests.
/// Mocks DAPR dependencies so no sidecar is needed. OpenAPI spec is generated
/// from code metadata and does not require live DAPR calls.
/// </summary>
public class OpenApiWebApplicationFactory : WebApplicationFactory<CommandApiProgram> {
    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        ArgumentNullException.ThrowIfNull(builder);
        _ = builder.UseEnvironment("Development");

        _ = builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["EventStore:OpenApi:Enabled"] = "true",
        }));

        _ = builder.ConfigureTestServices(services => {
            // Replace IActorProxyFactory with a mock (avoids DaprClient startup)
            ServiceDescriptor? existingFactory = services.FirstOrDefault(
                d => d.ServiceType == typeof(IActorProxyFactory));
            if (existingFactory is not null) {
                _ = services.Remove(existingFactory);
            }

            IActorProxyFactory mockProxyFactory = Substitute.For<IActorProxyFactory>();

            IAggregateActor fakeAggregateActor = Substitute.For<IAggregateActor>();
            _ = fakeAggregateActor
                .ProcessCommandAsync(Arg.Any<Contracts.Commands.CommandEnvelope>())
                .Returns(new CommandProcessingResult(true, CorrelationId: "test-corr"));
            _ = mockProxyFactory
                .CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>())
                .Returns(fakeAggregateActor);

            _ = services.AddSingleton(mockProxyFactory);

            // Remove Dapr health checks that require sidecar
            TestServiceOverrides.RemoveDaprHealthChecks(services);
        });
    }
}
