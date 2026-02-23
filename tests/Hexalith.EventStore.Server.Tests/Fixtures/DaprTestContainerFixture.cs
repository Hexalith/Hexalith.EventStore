
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
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
    private int _daprInternalGrpcPort;
    private int _daprMetricsPort;
    private int _daprProfilePort;
    private string? _componentsDir;

    private string? _previousDaprHttpPort;
    private string? _previousDaprGrpcPort;
    private readonly StringBuilder _daprStdout = new();
    private readonly StringBuilder _daprStderr = new();
    private volatile bool _hostStopping;
    private string? _hostStopStackTrace;

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
        KillOrphanedDaprdProcesses();

        int[] ports = GetAvailablePorts(6);
        _appPort = ports[0];
        _daprHttpPort = ports[1];
        _daprGrpcPort = ports[2];
        _daprInternalGrpcPort = ports[3];
        _daprMetricsPort = ports[4];
        _daprProfilePort = ports[5];

        // The Dapr .NET Actors runtime uses the DAPR_* env vars to find the sidecar.
        // Since the fixture starts daprd on random ports, we must set these for the in-process app.
        _previousDaprHttpPort = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT");
        _previousDaprGrpcPort = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT");
        Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", _daprHttpPort.ToString());
        Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", _daprGrpcPort.ToString());

        await VerifyPrerequisitesAsync().ConfigureAwait(false);

        _componentsDir = CreateComponentFiles();

        await StartTestHostAsync().ConfigureAwait(false);

        await VerifyAppListeningAsync().ConfigureAwait(false);

        StartDaprSidecar();

        await WaitForDaprHealthAsync().ConfigureAwait(false);

        // Let the sidecar complete its initial app discovery (GET /dapr/config)
        // and actor registration with the placement service before running tests.
        await Task.Delay(2000).ConfigureAwait(false);

        await VerifyAppListeningAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Throws if the test host has begun shutting down. Call from tests to get a clear
    /// diagnostic instead of a generic "connection refused" from the sidecar.
    /// </summary>
    public void ThrowIfHostStopped() {
        if (_hostStopping) {
            throw new InvalidOperationException(
                $"Test host shut down unexpectedly.\n" +
                $"Stop stack trace:\n{_hostStopStackTrace}");
        }
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

        // Restore previous env var values to reduce cross-test interference.
        Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", _previousDaprHttpPort);
        Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", _previousDaprGrpcPort);
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
                    "--app-channel-address", "127.0.0.1",
                    "--dapr-http-port", _daprHttpPort.ToString(),
                    "--dapr-grpc-port", _daprGrpcPort.ToString(),
                    "--dapr-internal-grpc-port", _daprInternalGrpcPort.ToString(),
                    "--metrics-port", _daprMetricsPort.ToString(),
                    "--profile-port", _daprProfilePort.ToString(),
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

    /// <summary>
    /// Kills any orphaned daprd processes from previous test runs that used the same app ID.
    /// If the test runner exits without calling DisposeAsync, stale sidecars remain registered
    /// with the placement service. Actor calls get routed to these stale instances, which try
    /// to connect to old app ports that are no longer listening.
    /// </summary>
    private static void KillOrphanedDaprdProcesses() {
        try {
            foreach (Process process in Process.GetProcessesByName("daprd")) {
                try {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
                catch (Exception) {
                    // Best-effort: process may have already exited
                }
                finally {
                    process.Dispose();
                }
            }
        }
        catch (Exception) {
            // Best-effort cleanup
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
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions());

        builder.Configuration["DAPR_HTTP_PORT"] = _daprHttpPort.ToString();
        builder.Configuration["DAPR_GRPC_PORT"] = _daprGrpcPort.ToString();
        builder.Configuration["Dapr:HttpPort"] = _daprHttpPort.ToString();
        builder.Configuration["Dapr:GrpcPort"] = _daprGrpcPort.ToString();

        builder.WebHost.ConfigureKestrel(serverOptions => {
            serverOptions.ListenLocalhost(_appPort, listenOptions => {
                listenOptions.Protocols = HttpProtocols.Http1;
            });
        });

        _ = builder.Services.AddEventStoreServer(builder.Configuration);

        _ = builder.Services.AddSingleton<IDomainServiceInvoker>(DomainServiceInvoker);
        _ = builder.Services.AddSingleton<IEventPublisher>(EventPublisher);
        _ = builder.Services.AddSingleton<IDeadLetterPublisher>(DeadLetterPublisher);
        _ = builder.Services.AddSingleton<ICommandStatusStore>(CommandStatusStore);

        _ = builder.Services.Configure<SnapshotOptions>(o => o.DomainIntervals["counter"] = 100);

        _testHost = builder.Build();

        _testHost.Lifetime.ApplicationStopping.Register(() => {
            _hostStopping = true;
            _hostStopStackTrace = Environment.StackTrace;
        });

        _ = _testHost.MapActorsHandlers();
        _ = _testHost.MapGet("/healthz", () => Microsoft.AspNetCore.Http.Results.Ok("healthy"));

        await _testHost.StartAsync().ConfigureAwait(false);

        IServer server = _testHost.Services.GetRequiredService<IServer>();
        ICollection<string>? addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        if (addresses is null || addresses.Count == 0) {
            throw new InvalidOperationException(
                $"Kestrel did not bind to any addresses. Expected port {_appPort}.");
        }
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

    /// <summary>
    /// Allocates multiple unique ports simultaneously, eliminating the TOCTOU race
    /// where sequential allocations can return the same port after the listener closes.
    /// </summary>
    private static int[] GetAvailablePorts(int count) {
        var listeners = new TcpListener[count];
        var ports = new int[count];

        for (int i = 0; i < count; i++) {
            listeners[i] = new TcpListener(IPAddress.Loopback, 0);
            listeners[i].Start();
            ports[i] = ((IPEndPoint)listeners[i].LocalEndpoint).Port;
        }

        for (int i = 0; i < count; i++) {
            listeners[i].Stop();
        }

        return ports;
    }

    /// <summary>
    /// Verifies the test host app is accepting HTTP requests on the expected port.
    /// Uses actual HTTP GET (not just TCP connect) to confirm the full request pipeline is alive.
    /// Also detects if the host has started shutting down.
    /// </summary>
    private async Task VerifyAppListeningAsync() {
        if (_hostStopping) {
            throw new InvalidOperationException(
                $"Test host is shutting down before verification.\n" +
                $"Stop stack trace:\n{_hostStopStackTrace}");
        }

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        string healthUrl = $"http://127.0.0.1:{_appPort}/healthz";
        string? lastError = null;

        for (int i = 0; i < 30; i++) {
            if (_hostStopping) {
                throw new InvalidOperationException(
                    $"Test host began shutting down during verification (attempt {i + 1}).\n" +
                    $"Stop stack trace:\n{_hostStopStackTrace}");
            }

            try {
                _ = await httpClient.GetAsync(healthUrl).ConfigureAwait(false);
                return;
            }
            catch (HttpRequestException ex) {
                lastError = ex.Message;
            }
            catch (TaskCanceledException) {
                lastError = "Request timed out";
            }

            await Task.Delay(200).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"Test host HTTP check failed on http://127.0.0.1:{_appPort} after 30 attempts.\n" +
            $"Host stopping: {_hostStopping}\n" +
            $"Last HTTP error: {lastError}\n" +
            $"Ports: app={_appPort}, daprHttp={_daprHttpPort}, daprGrpc={_daprGrpcPort}\n" +
            $"--- daprd stdout (last 2000 chars) ---\n{TailString(GetCapturedStdout(), 2000)}\n" +
            $"--- daprd stderr (last 2000 chars) ---\n{TailString(GetCapturedStderr(), 2000)}");
    }
}
