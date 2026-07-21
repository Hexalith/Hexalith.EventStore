using System.Net;

using Dapr.Actors.Runtime;
using Dapr.Client;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Projections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;

public class EventStoreServerServiceCollectionExtensionsTests {
    [Fact]
    public void AddEventStoreServerWithoutDaprClientDoesNotActivateCleanupService() {
        using ServiceProvider provider = BuildProvider(registerDaprClient: false);

        IHostedService[] hostedServices = [.. provider.GetServices<IHostedService>()];

        hostedServices.ShouldNotContain(service => service is ActiveRebuildIndexCleanupService);
    }

    [Fact]
    public void AddEventStoreServerWithDaprClientActivatesCleanupService() {
        using ServiceProvider provider = BuildProvider(registerDaprClient: true);

        IHostedService[] hostedServices = [.. provider.GetServices<IHostedService>()];

        hostedServices.ShouldContain(service => service is ActiveRebuildIndexCleanupService);
    }

    [Fact]
    public void AddEventStoreServerRegistersDaprGlobalPositionAllocator() {
        using ServiceProvider provider = BuildProvider(registerDaprClient: false);

        IGlobalPositionAllocator allocator = provider.GetRequiredService<IGlobalPositionAllocator>();

        _ = allocator.ShouldBeOfType<DaprGlobalPositionAllocator>();
    }

    [Fact]
    public void AddEventStoreServerRegistersGlobalPositionActor() {
        using ServiceProvider provider = BuildProvider(registerDaprClient: false);

        ActorRuntime actorRuntime = provider.GetRequiredService<ActorRuntime>();

        actorRuntime.RegisteredActors.ShouldContain(registration =>
            registration.Type.ImplementationType == typeof(GlobalPositionActor)
            && registration.Type.ActorTypeName == GlobalPositionActor.ActorTypeName);
    }

    [Fact]
    public void AddEventStoreServerRegistersDeliveryStateMachineAndReadyHealthGate() {
        using ServiceProvider provider = BuildProvider(registerDaprClient: true);

        provider.GetRequiredService<IProjectionDeliveryStateStore>()
            .ShouldBeOfType<DaprProjectionDeliveryStateStore>();
        provider.GetRequiredService<IProjectionDeliveryIdempotencyCoordinator>()
            .ShouldBeOfType<ProjectionDeliveryIdempotencyCoordinator>();
        provider.GetRequiredService<IProjectionDeliveryReconciler>()
            .ShouldBeOfType<ProjectionDeliveryReconciler>();
        provider.GetRequiredService<IProjectionDeliveryCutover>()
            .ShouldBeOfType<ProjectionDeliveryCutover>();
        provider.GetRequiredService<IOptions<ProjectionDeliveryIdempotencyOptions>>().Value
            .CompletedReceiptLimit.ShouldBe(256);
        HealthCheckRegistration registration = provider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value.Registrations
            .Single(value => value.Name == "projection-delivery-writer-protocol");
        registration.Tags.ShouldContain("ready");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(DomainServiceOptions.MaximumInvocationTimeoutSeconds + 1)]
    public void AddEventStoreServerRejectsInvocationTimeoutOutsideSupportedRange(int timeoutSeconds) {
        using ServiceProvider provider = BuildProvider(
            registerDaprClient: false,
            new Dictionary<string, string?> {
                ["EventStore:DomainServices:InvocationTimeoutSeconds"] = timeoutSeconds.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
            });

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<DomainServiceOptions>>().Value);
    }

    [Fact]
    public async Task AddEventStoreServerRejectsInvalidInvocationTimeoutAtHostStartupAsync() {
        using IHost host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(static configuration => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> {
                    ["EventStore:DomainServices:InvocationTimeoutSeconds"] = "0",
                }))
            .ConfigureServices(static (context, services) =>
                _ = services.AddEventStoreServer(context.Configuration))
            .Build();

        _ = await Should.ThrowAsync<OptionsValidationException>(
            () => host.StartAsync()).ConfigureAwait(true);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(DomainServiceOptions.MaximumInvocationTimeoutSeconds)]
    public void AddEventStoreServerAcceptsInvocationTimeoutBoundaries(int timeoutSeconds) {
        using ServiceProvider provider = BuildProvider(
            registerDaprClient: false,
            new Dictionary<string, string?> {
                ["EventStore:DomainServices:InvocationTimeoutSeconds"]
                    = timeoutSeconds.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
            });

        provider.GetRequiredService<IOptions<DomainServiceOptions>>()
            .Value.InvocationTimeoutSeconds.ShouldBe(timeoutSeconds);
    }

    [Fact]
    public async Task AddEventStoreServerAcceptsOneSecondInvocationTimeoutAtHostStartAsync() {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["EventStore:DomainServices:InvocationTimeoutSeconds"] = "1",
        });
        _ = builder.Services.AddEventStoreServer(builder.Configuration);
        using IHost host = builder.Build();

        await host.StartAsync().ConfigureAwait(true);

        host.Services.GetRequiredService<IOptions<DomainServiceOptions>>()
            .Value.InvocationTimeoutSeconds.ShouldBe(1);
        await host.StopAsync().ConfigureAwait(true);
    }

    [Fact]
    public async Task AddEventStoreServerRejectsInvalidInvocationTimeoutAtHostStartAsync() {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["EventStore:DomainServices:InvocationTimeoutSeconds"] = "0",
        });
        _ = builder.Services.AddEventStoreServer(builder.Configuration);
        using IHost host = builder.Build();

        _ = await Should.ThrowAsync<OptionsValidationException>(
            () => host.StartAsync()).ConfigureAwait(true);
    }

    [Fact]
    public async Task DomainServiceInvocationClientDoesNotInheritDefaultResilienceRetriesAsync() {
        var handler = new CountingFailureHandler();
        var services = new ServiceCollection();
        _ = services.ConfigureHttpClientDefaults(clientBuilder =>
            _ = clientBuilder.AddStandardResilienceHandler(options => {
                options.Retry.Delay = TimeSpan.Zero;
                options.Retry.MaxRetryAttempts = 3;
            }));
        _ = services.AddHttpClient(DaprDomainServiceInvoker.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        _ = services.AddLogging();
        _ = services.AddEventStoreServer(new ConfigurationBuilder().Build());
        using ServiceProvider provider = services.BuildServiceProvider();
        using HttpClient client = provider.GetRequiredService<IHttpClientFactory>()
            .CreateClient(DaprDomainServiceInvoker.HttpClientName);

        using HttpResponseMessage response = await client
            .PostAsync("https://domain-service.test/process", content: null)
            .ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        handler.AttemptCount.ShouldBe(1);
    }

    private static ServiceProvider BuildProvider(
        bool registerDaprClient,
        IEnumerable<KeyValuePair<string, string?>>? configurationValues = null) {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues ?? [])
            .Build();
        var services = new ServiceCollection();
        _ = services.AddLogging();
        if (registerDaprClient) {
            _ = services.AddSingleton(Substitute.For<DaprClient>());
        }

        _ = services.AddEventStoreServer(configuration);
        return services.BuildServiceProvider();
    }

    private sealed class CountingFailureHandler : HttpMessageHandler {
        public int AttemptCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) {
            AttemptCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
    }
}
