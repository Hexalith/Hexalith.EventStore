namespace Hexalith.EventStore.Server.Tests.Fixtures;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

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

/// <summary>
/// Integration test fixture that starts a local <c>daprd</c> process,
/// reusing the existing Dapr infrastructure (Redis, placement, scheduler) from <c>dapr init</c>.
/// Provides a running Dapr environment with actor support for Tier 2 integration tests.
/// Implements <see cref="IAsyncLifetime"/> for xUnit lifecycle management.
/// </summary>
public sealed class DaprTestContainerFixture : IAsyncLifetime {
    private const string AppId = "commandapi";
    private const int PlacementPort = 6050;
    private const int SchedulerPort = 6060;
    private const int RedisPort = 6379;

    private Process? _daprProcess;
    private WebApplication? _testHost;
    private int _appPort;
    private int _daprHttpPort;
    private int _daprGrpcPort;
    private string? _componentsDir;

    /// <summary>Gets the Dapr HTTP endpoint for test clients.</summary>
    public string DaprHttpEndpoint => $"http://localhost:{_daprHttpPort}";

    /// <summary>Gets the Dapr gRPC endpoint for test clients.</summary>
    public string DaprGrpcEndpoint => $"http://localhost:{_daprGrpcPort}";

    /// <summary>Gets the fake domain service invoker for configuring test responses.</summary>
    public FakeDomainServiceInvoker DomainServiceInvoker { get; } = new();

    /// <summary>Gets the fake event publisher for test assertions.</summary>
    public FakeEventPublisher EventPublisher { get; } = new();

    /// <summary>Gets the fake dead-letter publisher for test assertions.</summary>
    public FakeDeadLetterPublisher DeadLetterPublisher { get; } = new();

    /// <summary>Gets the in-memory command status store for test assertions.</summary>
    public InMemoryCommandStatusStore CommandStatusStore { get; } = new();

    /// <inheritdoc/>
    public async Task InitializeAsync() {
        _appPort = GetAvailablePort();
        _daprHttpPort = GetAvailablePort();
        _daprGrpcPort = GetAvailablePort();

        _componentsDir = CreateComponentFiles();

        await StartTestHostAsync().ConfigureAwait(false);

        StartDaprSidecar();

        await WaitForDaprHealthAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DisposeAsync() {
        if (_testHost is not null) {
            await _testHost.StopAsync().ConfigureAwait(false);
            await _testHost.DisposeAsync().ConfigureAwait(false);
        }

        if (_daprProcess is not null && !_daprProcess.HasExited) {
            _daprProcess.Kill(entireProcessTree: true);
            await _daprProcess.WaitForExitAsync().ConfigureAwait(false);
        }

        _daprProcess?.Dispose();

        if (_componentsDir is not null && Directory.Exists(_componentsDir)) {
            try {
                Directory.Delete(_componentsDir, recursive: true);
            }
            catch {
                // Best-effort cleanup of temp files
            }
        }
    }

    /// <summary>
    /// Configures the domain service invoker with Counter domain responses for integration tests.
    /// </summary>
    public void SetupCounterDomain() {
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

    private void StartDaprSidecar() {
        string daprdPath = ResolveDaprdPath();

        _daprProcess = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = daprdPath,
                Arguments = string.Join(' ',
                    "--app-id", AppId,
                    "--app-port", _appPort.ToString(),
                    "--app-protocol", "http",
                    "--dapr-http-port", _daprHttpPort.ToString(),
                    "--dapr-grpc-port", _daprGrpcPort.ToString(),
                    "--resources-path", $"\"{_componentsDir}\"",
                    "--log-level", "info",
                    "--placement-host-address", $"localhost:{PlacementPort}",
                    "--scheduler-host-address", $"localhost:{SchedulerPort}"),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };

        _daprProcess.Start();

        if (_daprProcess.HasExited) {
            string stderr = _daprProcess.StandardError.ReadToEnd();
            throw new InvalidOperationException(
                $"daprd exited immediately with code {_daprProcess.ExitCode}. stderr: {stderr}");
        }
    }

    private static string ResolveDaprdPath() {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string candidate = Path.Combine(home, ".dapr", "bin", "daprd" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));

        if (File.Exists(candidate)) {
            return candidate;
        }

        // Fall back to PATH
        return OperatingSystem.IsWindows() ? "daprd.exe" : "daprd";
    }

    private async Task StartTestHostAsync() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        builder.WebHost.UseUrls($"http://0.0.0.0:{_appPort}");

        builder.Services.AddEventStoreServer(builder.Configuration);

        builder.Services.AddSingleton<IDomainServiceInvoker>(DomainServiceInvoker);
        builder.Services.AddSingleton<IEventPublisher>(EventPublisher);
        builder.Services.AddSingleton<IDeadLetterPublisher>(DeadLetterPublisher);
        builder.Services.AddSingleton<ICommandStatusStore>(CommandStatusStore);

        builder.Services.Configure<SnapshotOptions>(o => {
            o.DomainIntervals["counter"] = 100;
        });

        _testHost = builder.Build();

        _testHost.MapActorsHandlers();

        await _testHost.StartAsync().ConfigureAwait(false);
    }

    private static string CreateComponentFiles() {
        string tempDir = Path.Combine(Path.GetTempPath(), $"dapr-components-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

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
                  value: "localhost:{{RedisPort}}"
                - name: redisPassword
                  value: ""
                - name: actorStateStore
                  value: "true"
            scopes:
              - commandapi
            """;

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
                  value: "localhost:{{RedisPort}}"
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

    private async Task WaitForDaprHealthAsync() {
        using var httpClient = new HttpClient();
        string healthUrl = $"{DaprHttpEndpoint}/v1.0/healthz";

        for (int i = 0; i < 30; i++) {
            if (_daprProcess?.HasExited == true) {
                string stderr = await _daprProcess.StandardError.ReadToEndAsync().ConfigureAwait(false);
                throw new InvalidOperationException(
                    $"daprd exited with code {_daprProcess.ExitCode} during health check. stderr: {stderr}");
            }

            try {
                HttpResponseMessage response = await httpClient.GetAsync(healthUrl).ConfigureAwait(false);
                if (response.IsSuccessStatusCode) {
                    return;
                }
            }
            catch (HttpRequestException) {
                // Sidecar not ready yet
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Dapr sidecar did not become healthy within 30 seconds");
    }

    private static int GetAvailablePort() {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
