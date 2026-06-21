using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hexalith.EventStore.Testing.Integration;

/// <summary>
/// Base integration-test fixture that starts a domain-service CommandApi host with a local
/// <c>daprd</c> sidecar, reusing the DAPR infrastructure (Redis, placement, scheduler) from
/// <c>dapr init</c>. It exercises the full command pipeline:
/// Actor → Domain Service Invocation → <c>/process</c> → Aggregate → Events.
/// </summary>
/// <remarks>
/// <para>
/// This base owns every domain-agnostic concern: the cross-fixture lock, orphan-sidecar cleanup,
/// local port allocation, prerequisite probing, DAPR component-file generation, the generic host
/// scaffold (Kestrel, actor handlers, health endpoint), the <c>daprd</c> launch, the health wait, and
/// support-safe diagnostics. A domain module subclasses it and supplies only its domain specifics
/// through <see cref="ConfigureDomainConfiguration"/>, <see cref="ConfigureDomainServices"/>, and
/// <see cref="MapDomainEndpoints"/>.
/// </para>
/// </remarks>
public abstract class DaprDomainServiceTestFixtureBase : IAsyncLifetime {
    private const int HealthTimeoutSeconds = 60;

    private static readonly int PlacementPort = DaprLocalEndpoints.PlacementPort;
    private static readonly int SchedulerPort = DaprLocalEndpoints.SchedulerPort;
    private readonly int _redisPort = DaprDiagnostics.ResolveRedisPort();

    private Process? _daprProcess;
    private WebApplication? _testHost;
    private int _appPort;
    private int _daprHttpPort;
    private int _daprGrpcPort;
    private int _daprInternalGrpcPort;
    private int _daprMetricsPort;
    private int _daprProfilePort;
    private string? _componentsDir;
    private FileStream? _daprFixtureLock;

    private string? _previousDaprHttpPort;
    private string? _previousDaprGrpcPort;
    private readonly StringBuilder _daprStdout = new();
    private readonly StringBuilder _daprStderr = new();
    private bool _disposed;

    /// <summary>Gets the DAPR application id (sidecar <c>--app-id</c>) for this fixture's host.</summary>
    protected abstract string AppId { get; }

    /// <summary>Gets the DAPR pub/sub component name used by the generated component files.</summary>
    protected virtual string PubSubName => "pubsub";

    /// <summary>Gets the dead-letter topic configured on the generated pub/sub component.</summary>
    protected virtual string DeadLetterTopic => $"deadletter.{AppId}.events";

    /// <summary>Gets the Dapr HTTP endpoint for actor proxy clients.</summary>
    public string DaprHttpEndpoint {
        get {
            SkipIfUnavailable();
            return $"http://localhost:{_daprHttpPort}";
        }
    }

    /// <summary>Gets the application HTTP endpoint (used to force actor deactivation in tests).</summary>
    public string AppEndpoint {
        get {
            SkipIfUnavailable();
            return $"http://localhost:{_appPort}";
        }
    }

    /// <summary>Gets the last exception thrown by the <c>/process</c> endpoint, for test diagnostics.</summary>
    public Exception? LastProcessException { get; private set; }

    /// <summary>Gets a support-safe description of the last <c>/process</c> endpoint failure.</summary>
    public string? LastProcessDiagnostic { get; private set; }

    /// <summary>
    /// Gets a value indicating whether local DAPR prerequisites were available during fixture startup.
    /// </summary>
    public bool PrerequisitesAvailable { get; private set; } = true;

    /// <summary>
    /// Gets the skip reason when local DAPR prerequisites are unavailable.
    /// </summary>
    public string? SkipReason { get; private set; }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync() {
        AcquireDaprFixtureLock();

        KillOrphanedDaprdProcesses();

        int[] ports;
        try {
            ports = GetAvailablePorts(6);
        }
        catch (SocketException ex) {
            PrerequisitesAvailable = false;
            SkipReason = "Dapr infrastructure pre-flight check failed. Unable to allocate local listener ports for the test fixture."
                + Environment.NewLine
                + $"  - {ex.GetType().FullName} ({ex.NativeErrorCode}): {ex.Message}";
            return;
        }

        _appPort = ports[0];
        _daprHttpPort = ports[1];
        _daprGrpcPort = ports[2];
        _daprInternalGrpcPort = ports[3];
        _daprMetricsPort = ports[4];
        _daprProfilePort = ports[5];

        _previousDaprHttpPort = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT");
        _previousDaprGrpcPort = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT");
        Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", _daprHttpPort.ToString());
        Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", _daprGrpcPort.ToString());

        IReadOnlyList<string> prerequisiteFailures = await GetPrerequisiteFailuresAsync().ConfigureAwait(false);
        if (prerequisiteFailures.Count > 0) {
            PrerequisitesAvailable = false;
            SkipReason = DaprDiagnostics.BuildPrerequisiteFailureMessage(prerequisiteFailures);
            return;
        }

        _componentsDir = CreateComponentFiles();

        await StartTestHostAsync().ConfigureAwait(false);

        await VerifyAppListeningAsync().ConfigureAwait(false);

        try {
            StartDaprSidecar();

            await WaitForDaprHealthAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (DaprDiagnostics.IsDaprInfrastructureStartupFailure(ex)) {
            PrerequisitesAvailable = false;
            SkipReason = "Dapr sidecar infrastructure startup failed. Ensure Redis, placement, and scheduler are healthy before running these tests."
                + Environment.NewLine
                + DaprDiagnostics.ToSupportSafeDiagnostic(ex.Message);
            await DisposeAsync().ConfigureAwait(false);
            return;
        }

        // Let sidecar complete actor registration with placement service.
        await Task.Delay(2000).ConfigureAwait(false);

        await VerifyAppListeningAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Skips the current test when local DAPR prerequisites were not available during fixture startup.
    /// </summary>
    public void SkipIfUnavailable() {
        if (!PrerequisitesAvailable) {
            Assert.Skip(SkipReason ?? DaprTestPrerequisites.SkipReason);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }

        _disposed = true;

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
                // Best-effort cleanup
            }
        }

        Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", _previousDaprHttpPort);
        Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", _previousDaprGrpcPort);

        _daprFixtureLock?.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Applies domain-specific configuration to the test host (domain-service registrations, topic
    /// overrides, drain timings, and any other configuration keys the domain needs).
    /// </summary>
    /// <param name="configuration">The host configuration manager.</param>
    protected abstract void ConfigureDomainConfiguration(ConfigurationManager configuration);

    /// <summary>
    /// Registers domain-specific services on the test host. Implementations own the full registration
    /// order, including registering publisher/store fakes <em>before</em> the EventStore server
    /// registration, adding the DAPR client and EventStore server, registering the domain aggregates,
    /// and registering the query-cursor codec.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="configuration">The host configuration.</param>
    protected abstract void ConfigureDomainServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    /// Maps the domain-specific endpoints on the test host (notably the <c>/process</c> domain-service
    /// router endpoint). Implementations should call <see cref="RecordProcessFailure"/> when the
    /// <c>/process</c> handler catches an exception so test diagnostics stay support-safe.
    /// </summary>
    /// <param name="app">The built web application.</param>
    protected abstract void MapDomainEndpoints(WebApplication app);

    /// <summary>
    /// Records the last <c>/process</c> endpoint failure for test diagnostics. The diagnostic must be
    /// support-safe (a command-category description, never a raw exception or payload).
    /// </summary>
    /// <param name="exception">The exception caught by the domain <c>/process</c> handler.</param>
    /// <param name="diagnostic">A support-safe description of the failure.</param>
    protected void RecordProcessFailure(Exception exception, string diagnostic) {
        LastProcessException = exception;
        LastProcessDiagnostic = diagnostic;
    }

    private void AcquireDaprFixtureLock() {
        string lockPath = Path.Combine(Path.GetTempPath(), "hexalith-eventstore-dapr-fixture.lock");
        while (true) {
            try {
                _daprFixtureLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return;
            }
            catch (IOException) {
                Thread.Sleep(250);
            }
        }
    }

    private async Task<IReadOnlyList<string>> GetPrerequisiteFailuresAsync() {
        var failures = new List<string>();

        if (!await DaprDiagnostics.IsRedisResponsiveAsync(_redisPort).ConfigureAwait(false)) {
            failures.Add($"Redis is not responding to PING on localhost:{_redisPort}");
        }

        if (!await DaprDiagnostics.IsPortReachableAsync("localhost", PlacementPort).ConfigureAwait(false)) {
            failures.Add($"Dapr placement service is not reachable on localhost:{PlacementPort}");
        }

        if (!await DaprDiagnostics.IsPortReachableAsync("localhost", SchedulerPort).ConfigureAwait(false)) {
            failures.Add($"Dapr scheduler service is not reachable on localhost:{SchedulerPort}");
        }

        return failures;
    }

    private async Task StartTestHostAsync() {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions());

        // Configure DAPR ports for actor runtime (generic — domain-agnostic).
        builder.Configuration["DAPR_HTTP_PORT"] = _daprHttpPort.ToString();
        builder.Configuration["DAPR_GRPC_PORT"] = _daprGrpcPort.ToString();
        builder.Configuration["Dapr:HttpPort"] = _daprHttpPort.ToString();
        builder.Configuration["Dapr:GrpcPort"] = _daprGrpcPort.ToString();

        // Domain-specific configuration: domain-service registrations, topic overrides, drain timings.
        ConfigureDomainConfiguration(builder.Configuration);

        _ = builder.WebHost.ConfigureKestrel(serverOptions =>
            serverOptions.ListenLocalhost(_appPort, listenOptions =>
                listenOptions.Protocols = HttpProtocols.Http1));

        // Domain-specific services: fakes (before EventStore server), DAPR client, EventStore server,
        // domain aggregates, data protection, and the query-cursor codec.
        ConfigureDomainServices(builder.Services, builder.Configuration);

        _testHost = builder.Build();

        // Map endpoints — generic actor handlers and health, then domain-specific routes (/process).
        _ = _testHost.MapActorsHandlers();
        MapDomainEndpoints(_testHost);
        _ = _testHost.MapGet("/healthz", () => Microsoft.AspNetCore.Http.Results.Ok("healthy"));

        await _testHost.StartAsync().ConfigureAwait(false);

        IServer server = _testHost.Services.GetRequiredService<IServer>();
        ICollection<string>? addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        if (addresses is null || addresses.Count == 0) {
            throw new InvalidOperationException(
                $"Kestrel did not bind to any addresses. Expected port {_appPort}.");
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

    private string CreateComponentFiles() {
        string tempDir = Path.Combine(Path.GetTempPath(), $"dapr-{AppId}-{Guid.NewGuid():N}");
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
                  value: "localhost:{{_redisPort}}"
                - name: redisPassword
                  value: ""
                - name: actorStateStore
                  value: "true"
            scopes:
              - {{AppId}}
            """;

        string pubSubYaml = $$"""
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: {{PubSubName}}
            spec:
              type: pubsub.redis
              version: v1
              metadata:
                - name: redisHost
                  value: "localhost:{{_redisPort}}"
                - name: redisPassword
                  value: ""
                - name: enableDeadLetter
                  value: "true"
                - name: deadLetterTopic
                  value: "{{DeadLetterTopic}}"
            scopes:
              - {{AppId}}
            """;

        File.WriteAllText(Path.Combine(tempDir, "statestore.yaml"), stateStoreYaml);
        File.WriteAllText(Path.Combine(tempDir, $"{PubSubName}.yaml"), pubSubYaml);

        return tempDir;
    }

    private async Task WaitForDaprHealthAsync() {
        using var httpClient = new HttpClient();
        string healthUrl = $"http://localhost:{_daprHttpPort}/v1.0/healthz/outbound";

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

        throw new InvalidOperationException(
            $"Dapr sidecar did not become healthy within {HealthTimeoutSeconds} seconds.\n" +
            $"Health endpoint: {healthUrl}\n" +
            $"Last HTTP status: {lastStatus}\n" +
            $"Last connection error: {lastError ?? "(none)"}\n" +
            $"Ports: app={_appPort}, daprHttp={_daprHttpPort}, daprGrpc={_daprGrpcPort}\n" +
            $"--- daprd stdout (last 2000 chars) ---\n{TailString(GetCapturedStdout(), 2000)}\n" +
            $"--- daprd stderr (last 2000 chars) ---\n{TailString(GetCapturedStderr(), 2000)}");
    }

    private async Task VerifyAppListeningAsync() {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        string healthUrl = $"http://127.0.0.1:{_appPort}/healthz";
        string? lastError = null;

        for (int i = 0; i < 30; i++) {
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
            $"Last HTTP error: {lastError}");
    }

    private void KillOrphanedDaprdProcesses() {
        if (Environment.GetEnvironmentVariable("DAPR_TEST_PRESERVE_SIDECARS") == "1") {
            return;
        }

        try {
            foreach (Process process in Process.GetProcessesByName("daprd")) {
                try {
                    string? cmdLine = GetProcessCommandLine(process);
                    if (cmdLine is null || !cmdLine.Contains(AppId, StringComparison.OrdinalIgnoreCase)) {
                        process.Dispose();
                        continue;
                    }

                    process.Kill(entireProcessTree: true);
                    _ = process.WaitForExit(5000);
                }
                catch {
                    // Best-effort cleanup
                }
                finally {
                    process.Dispose();
                }
            }
        }
        catch {
            // Best-effort cleanup
        }
    }

    private static string? GetProcessCommandLine(Process process) {
        try {
            if (OperatingSystem.IsWindows()) {
                using var searcher = new Process {
                    StartInfo = new ProcessStartInfo {
                        FileName = "wmic",
                        Arguments = $"process where processid={process.Id} get CommandLine /format:list",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                    },
                };
                _ = searcher.Start();
                string output = searcher.StandardOutput.ReadToEnd();
                _ = searcher.WaitForExit(3000);
                return output;
            }

            string cmdlinePath = $"/proc/{process.Id}/cmdline";
            if (File.Exists(cmdlinePath)) {
                return File.ReadAllText(cmdlinePath).Replace('\0', ' ');
            }
        }
        catch {
            // Best-effort
        }

        return null;
    }

    private static string ResolveDaprdPath() {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string candidate = Path.Combine(home, ".dapr", "bin", "daprd" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));

        if (File.Exists(candidate)) {
            return candidate;
        }

        return OperatingSystem.IsWindows() ? "daprd.exe" : "daprd";
    }

    private static int[] GetAvailablePorts(int count) {
        var listeners = new TcpListener[count];
        int[] ports = new int[count];

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

    private string GetCapturedStdout() {
        lock (_daprStdout) { return _daprStdout.ToString(); }
    }

    private string GetCapturedStderr() {
        lock (_daprStderr) { return _daprStderr.ToString(); }
    }

    private static string TailString(string value, int maxChars) {
        if (string.IsNullOrEmpty(value) || value.Length <= maxChars) {
            return value;
        }

        return "..." + value[^maxChars..];
    }
}
