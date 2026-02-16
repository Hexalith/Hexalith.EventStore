namespace Hexalith.EventStore.Server.Tests.Fixtures;

using System.Net;
using System.Net.Sockets;

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

/// <summary>
/// Integration test fixture that starts Redis and Dapr sidecar containers via Testcontainers.
/// Provides a running Dapr environment with actor support for Tier 2 integration tests.
/// Implements <see cref="IAsyncLifetime"/> for xUnit lifecycle management.
/// </summary>
public sealed class DaprTestContainerFixture : IAsyncLifetime
{
    private const string RedisImage = "redis:7-alpine";
    private const string DaprImage = "daprio/daprd:1.16.6";
    private const string RedisNetworkAlias = "redis-test";
    private const string AppId = "commandapi";
    private const int DaprHttpPort = 3500;
    private const int DaprGrpcPort = 50001;
    private const int RedisPort = 6379;

    private INetwork? _network;
    private IContainer? _redisContainer;
    private IContainer? _daprContainer;
    private WebApplication? _testHost;
    private int _appPort;

    /// <summary>Gets the Dapr HTTP endpoint for test clients.</summary>
    public string DaprHttpEndpoint => $"http://localhost:{_daprContainer?.GetMappedPublicPort(DaprHttpPort)}";

    /// <summary>Gets the Dapr gRPC endpoint for test clients.</summary>
    public string DaprGrpcEndpoint => $"http://localhost:{_daprContainer?.GetMappedPublicPort(DaprGrpcPort)}";

    /// <summary>Gets the fake domain service invoker for configuring test responses.</summary>
    public FakeDomainServiceInvoker DomainServiceInvoker { get; } = new();

    /// <summary>Gets the fake event publisher for test assertions.</summary>
    public FakeEventPublisher EventPublisher { get; } = new();

    /// <summary>Gets the fake dead-letter publisher for test assertions.</summary>
    public FakeDeadLetterPublisher DeadLetterPublisher { get; } = new();

    /// <summary>Gets the in-memory command status store for test assertions.</summary>
    public InMemoryCommandStatusStore CommandStatusStore { get; } = new();

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        _appPort = GetAvailablePort();

        // Create shared Docker network
        _network = new NetworkBuilder()
            .WithName($"dapr-test-{Guid.NewGuid():N}")
            .Build();
        await _network.CreateAsync().ConfigureAwait(false);

        // Start Redis container
        _redisContainer = new ContainerBuilder(RedisImage)
            .WithNetwork(_network)
            .WithNetworkAliases(RedisNetworkAlias)
            .WithPortBinding(RedisPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
            .Build();
        await _redisContainer.StartAsync().ConfigureAwait(false);

        // Create temporary component YAML files for the Dapr sidecar
        string componentsDir = CreateComponentFiles();

        // Start the test ASP.NET host with actor registration
        await StartTestHostAsync().ConfigureAwait(false);

        // Start Dapr sidecar container
        _daprContainer = new ContainerBuilder(DaprImage)
            .WithNetwork(_network)
            .WithPortBinding(DaprHttpPort, true)
            .WithPortBinding(DaprGrpcPort, true)
            .WithResourceMapping(componentsDir, "/components")
            .WithCommand(
                "./daprd",
                "--app-id", AppId,
                "--app-port", _appPort.ToString(),
                "--app-protocol", "http",
                "--dapr-http-port", DaprHttpPort.ToString(),
                "--dapr-grpc-port", DaprGrpcPort.ToString(),
                "--resources-path", "/components",
                "--log-level", "info",
                "--app-channel-address", "host.docker.internal")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPath("/v1.0/healthz")
                    .ForPort((ushort)DaprHttpPort)))
            .Build();
        await _daprContainer.StartAsync().ConfigureAwait(false);

        // Wait for Dapr to be healthy
        await WaitForDaprHealthAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        if (_testHost is not null)
        {
            await _testHost.StopAsync().ConfigureAwait(false);
            await _testHost.DisposeAsync().ConfigureAwait(false);
        }

        if (_daprContainer is not null)
        {
            await _daprContainer.StopAsync().ConfigureAwait(false);
            await _daprContainer.DisposeAsync().ConfigureAwait(false);
        }

        if (_redisContainer is not null)
        {
            await _redisContainer.StopAsync().ConfigureAwait(false);
            await _redisContainer.DisposeAsync().ConfigureAwait(false);
        }

        if (_network is not null)
        {
            await _network.DeleteAsync().ConfigureAwait(false);
            await _network.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task StartTestHostAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        // Configure Kestrel to listen on the allocated port
        builder.WebHost.UseUrls($"http://0.0.0.0:{_appPort}");

        // Register EventStore server services
        builder.Services.AddEventStoreServer(builder.Configuration);

        // Override with fakes for integration testing
        builder.Services.AddSingleton<IDomainServiceInvoker>(DomainServiceInvoker);
        builder.Services.AddSingleton<IEventPublisher>(EventPublisher);
        builder.Services.AddSingleton<IDeadLetterPublisher>(DeadLetterPublisher);
        builder.Services.AddSingleton<ICommandStatusStore>(CommandStatusStore);

        // Configure snapshot options (default 100 events)
        builder.Services.Configure<SnapshotOptions>(o =>
        {
            o.DomainIntervals["counter"] = 100;
        });

        _testHost = builder.Build();

        // Map actor endpoints so Dapr sidecar can activate actors
        _testHost.MapActorsHandlers();

        await _testHost.StartAsync().ConfigureAwait(false);
    }

    private static string CreateComponentFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"dapr-components-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        // State store component pointing to Redis container via network alias
        string stateStoreYaml = $$"""
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: statestore
            spec:
              type: state.redis
              version: v1
              metadata:
                - name: redisHost
                  value: "{{RedisNetworkAlias}}:{{RedisPort}}"
                - name: redisPassword
                  value: ""
                - name: actorStateStore
                  value: "true"
            scopes:
              - commandapi
            """;

        // Pub/sub component (minimal for integration tests)
        string pubSubYaml = $$"""
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: pubsub
            spec:
              type: pubsub.redis
              version: v1
              metadata:
                - name: redisHost
                  value: "{{RedisNetworkAlias}}:{{RedisPort}}"
                - name: redisPassword
                  value: ""
                - name: enableDeadLetter
                  value: "true"
            scopes:
              - commandapi
            """;

        File.WriteAllText(Path.Combine(tempDir, "statestore.yaml"), stateStoreYaml);
        File.WriteAllText(Path.Combine(tempDir, "pubsub.yaml"), pubSubYaml);

        return tempDir;
    }

    private async Task WaitForDaprHealthAsync()
    {
        using var httpClient = new HttpClient();
        string healthUrl = $"{DaprHttpEndpoint}/v1.0/healthz";

        for (int i = 0; i < 30; i++)
        {
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(healthUrl).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Sidecar not ready yet
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Dapr sidecar did not become healthy within 30 seconds");
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    /// <summary>
    /// Configures the domain service invoker with Counter domain responses for integration tests.
    /// </summary>
    public void SetupCounterDomain()
    {
        DomainServiceInvoker.SetupResponse(
            "IncrementCounter",
            DomainResult.Success(new IEventPayload[] { new Hexalith.EventStore.Sample.Counter.Events.CounterIncremented() }));

        DomainServiceInvoker.SetupResponse(
            "DecrementCounter",
            DomainResult.Success(new IEventPayload[] { new Hexalith.EventStore.Sample.Counter.Events.CounterDecremented() }));

        DomainServiceInvoker.SetupResponse(
            "ResetCounter",
            DomainResult.Success(new IEventPayload[] { new Hexalith.EventStore.Sample.Counter.Events.CounterReset() }));
    }
}
