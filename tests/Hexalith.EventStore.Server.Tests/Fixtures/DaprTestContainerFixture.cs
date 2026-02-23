
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

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

namespace Hexalith.EventStore.Server.Tests.Fixtures;
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
    private const int HealthTimeoutSeconds = 60;

    private Process? _daprProcess;
    private WebApplication? _testHost;
    private int _appPort;
    private int _daprHttpPort;
    private int _daprGrpcPort;
    private string? _componentsDir;
    private readonly StringBuilder _daprStdout = new();
    private readonly StringBuilder _daprStderr = new();

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

        await VerifyPrerequisitesAsync().ConfigureAwait(false);

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

    private static async Task VerifyPrerequisitesAsync() {
        var failures = new List<string>();

        if (!await IsPortReachableAsync("localhost", RedisPort, "Redis").ConfigureAwait(false)) {
            failures.Add($"Redis is not reachable on localhost:{RedisPort}");
        }

        if (!await IsPortReachableAsync("localhost", PlacementPort, "Placement").ConfigureAwait(false)) {
            failures.Add($"Dapr placement service is not reachable on localhost:{PlacementPort}");
        }

        if (!await IsPortReachableAsync("localhost", SchedulerPort, "Scheduler").ConfigureAwait(false)) {
            failures.Add($"Dapr scheduler service is not reachable on localhost:{SchedulerPort}");
        }

        if (failures.Count > 0) {
            throw new InvalidOperationException(
                $"Dapr infrastructure pre-flight check failed. Have you run 'dapr init'?\n" +
                string.Join("\n", failures.Select(f => $"  - {f}")));
        }
    }

    private static async Task<bool> IsPortReachableAsync(string host, int port, string serviceName) {
        try {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception) {
            return false;
        }
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
            EnableRaisingEvents = true,
        };

        _daprProcess.OutputDataReceived += (_, e) => {
            if (e.Data is not null) {
                lock (_daprStdout) {
                    _ = _daprStdout.AppendLine(e.Data);
                }
            }
        };

        _daprProcess.ErrorDataReceived += (_, e) => {
            if (e.Data is not null) {
                lock (_daprStderr) {
                    _ = _daprStderr.AppendLine(e.Data);
                }
            }
        };

        _ = _daprProcess.Start();
        _daprProcess.BeginOutputReadLine();
        _daprProcess.BeginErrorReadLine();

        if (_daprProcess.HasExited) {
            throw new InvalidOperationException(
                $"daprd exited immediately with code {_daprProcess.ExitCode}.\nstderr: {GetCapturedStderr()}");
        }
    }

    private static string ResolveDaprdPath() {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string candidate = Path.Combine(home, ".dapr", "bin", "daprd" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));

        if (File.Exists(candidate)) {
            return candidate;
        }

        return OperatingSystem.IsWindows() ? "daprd.exe" : "daprd";
    }

    private async Task StartTestHostAsync() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        _ = builder.WebHost.UseUrls($"http://0.0.0.0:{_appPort}");

        _ = builder.Services.AddEventStoreServer(builder.Configuration);

        _ = builder.Services.AddSingleton<IDomainServiceInvoker>(DomainServiceInvoker);
        _ = builder.Services.AddSingleton<IEventPublisher>(EventPublisher);
        _ = builder.Services.AddSingleton<IDeadLetterPublisher>(DeadLetterPublisher);
        _ = builder.Services.AddSingleton<ICommandStatusStore>(CommandStatusStore);

        _ = builder.Services.Configure<SnapshotOptions>(o => o.DomainIntervals["counter"] = 100);

        _testHost = builder.Build();

        _ = _testHost.MapActorsHandlers();

        await _testHost.StartAsync().ConfigureAwait(false);
    }

    private static string CreateComponentFiles() {
        string tempDir = Path.Combine(Path.GetTempPath(), $"dapr-components-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempDir);

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

    /// <summary>
    /// Waits for daprd to become healthy using the outbound healthcheck,
    /// which verifies placement connectivity required for actors.
    /// </summary>
    private async Task WaitForDaprHealthAsync() {
        using var httpClient = new HttpClient();
        string healthUrl = $"{DaprHttpEndpoint}/v1.0/healthz/outbound";

        HttpStatusCode lastStatus = default;
        string? lastError = null;

        for (int i = 0; i < HealthTimeoutSeconds; i++) {
            if (_daprProcess?.HasExited == true) {
                throw new InvalidOperationException(
                    $"daprd exited with code {_daprProcess.ExitCode} during health check.\n" +
                    $"stdout:\n{GetCapturedStdout()}\n" +
                    $"stderr:\n{GetCapturedStderr()}");
            }

            try {
                HttpResponseMessage response = await httpClient.GetAsync(healthUrl).ConfigureAwait(false);
                lastStatus = response.StatusCode;
                if (response.IsSuccessStatusCode) {
                    return;
                }
            }
            catch (HttpRequestException ex) {
                lastError = ex.Message;
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        string diagnostics =
            $"Dapr sidecar did not become healthy within {HealthTimeoutSeconds} seconds.\n" +
            $"Health endpoint: {healthUrl}\n" +
            $"Last HTTP status: {lastStatus}\n" +
            $"Last connection error: {lastError ?? "(none)"}\n" +
            $"Ports: app={_appPort}, daprHttp={_daprHttpPort}, daprGrpc={_daprGrpcPort}\n" +
            $"--- daprd stdout (last 2000 chars) ---\n{TailString(GetCapturedStdout(), 2000)}\n" +
            $"--- daprd stderr (last 2000 chars) ---\n{TailString(GetCapturedStderr(), 2000)}";

        throw new InvalidOperationException(diagnostics);
    }

    private string GetCapturedStdout() {
        lock (_daprStdout) {
            return _daprStdout.ToString();
        }
    }

    private string GetCapturedStderr() {
        lock (_daprStderr) {
            return _daprStderr.ToString();
        }
    }

    private static string TailString(string value, int maxChars) {
        if (string.IsNullOrEmpty(value) || value.Length <= maxChars) {
            return value;
        }

        return "..." + value[^maxChars..];
    }

    private static int GetAvailablePort() {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
