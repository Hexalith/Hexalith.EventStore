using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Xunit;

namespace Hexalith.EventStore.Testing.Integration;

/// <summary>
/// Reusable xUnit fixture base that boots a full Aspire AppHost topology (with DAPR sidecars) and
/// creates HTTP clients for smoke tests. A concrete fixture supplies the AppHost project type via
/// <typeparamref name="TAppHost"/> and the resources to wait on via <see cref="Resources"/>.
/// </summary>
/// <typeparam name="TAppHost">The Aspire AppHost project marker type (e.g. <c>Projects.X_AppHost</c>).</typeparam>
/// <remarks>
/// <para>
/// This fixture verifies <strong>process liveness</strong>, not full readiness. It waits for resources
/// to reach <c>Running</c> state and (for resources flagged <see cref="AspireResource.WaitForAliveness"/>)
/// for the aliveness endpoint to return HTTP 200 — that proves the host is responding to HTTP, not that
/// every dependency is ready to serve traffic.
/// </para>
/// <para>
/// Full Dapr readiness (placement registration, sidecar handshake, state-store availability) is covered
/// by Dapr-specific integration tests, not by this liveness smoke check.
/// </para>
/// </remarks>
public abstract class AspireTopologyFixtureBase<TAppHost> : IAsyncLifetime
    where TAppHost : class {
    private static readonly int PlacementPort = DaprLocalEndpoints.PlacementPort;
    private static readonly int SchedulerPort = DaprLocalEndpoints.SchedulerPort;

    // The Redis prerequisite targets the `dapr init`-managed Redis (which DAPR sidecars use as their
    // state-store and pub-sub backend). dapr init defaults to localhost:6379, but developers can run it
    // on a non-default port; HEXALITH_EVENTSTORE_TEST_REDIS_PORT lets them override the probe port.
    private static readonly int RedisPort = ResolveRedisPort();
    private const int DefaultRedisPort = 6379;
    private static readonly TimeSpan DockerProbeTimeout = TimeSpan.FromSeconds(5);

    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;
    private FileStream? _daprFixtureLock;
    private readonly Stopwatch _startupStopwatch = new();
    private readonly Dictionary<string, HttpClient> _clients = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (HttpStatusCode? Status, string? Error)> _diagnostics = new(StringComparer.Ordinal);

    /// <summary>Describes an Aspire resource the fixture waits on and creates an HTTP client for.</summary>
    /// <param name="Name">The Aspire resource name.</param>
    /// <param name="EndpointName">The endpoint name to resolve (typically <c>http</c>).</param>
    /// <param name="ClientTimeout">The <see cref="HttpClient.Timeout"/> for the created client.</param>
    /// <param name="ReadinessTimeout">The maximum time to wait for the resource to reach Running with the endpoint published.</param>
    /// <param name="WaitForAliveness">Whether to additionally poll the aliveness endpoint for HTTP 200.</param>
    /// <param name="AlivenessTimeout">The maximum time to wait for the aliveness endpoint.</param>
    protected sealed record AspireResource(
        string Name,
        string EndpointName,
        TimeSpan ClientTimeout,
        TimeSpan ReadinessTimeout,
        bool WaitForAliveness,
        TimeSpan AlivenessTimeout);

    /// <summary>Gets the resources to start, wait on, and create clients for.</summary>
    protected abstract IReadOnlyList<AspireResource> Resources { get; }

    /// <summary>Gets extra AppHost command-line arguments (placement/scheduler are added automatically).</summary>
    protected virtual IReadOnlyList<string> ExtraAppArgs => [];

    /// <summary>Gets the overall startup timeout for build + graph evaluation + start.</summary>
    protected virtual TimeSpan StartupTimeout => TimeSpan.FromMinutes(6);

    /// <summary>Gets the aliveness endpoint path probed for resources flagged for liveness.</summary>
    protected virtual string AlivenessEndpointPath => "/alive";

    /// <summary>Gets the name of the cross-process lock file that serializes DAPR fixtures.</summary>
    protected virtual string FixtureLockName => "hexalith-dapr-test-fixture.lock";

    /// <summary>
    /// Gets a value indicating whether local DAPR prerequisites were available during fixture startup.
    /// </summary>
    public bool PrerequisitesAvailable { get; private set; } = true;

    /// <summary>
    /// Gets the skip reason when local DAPR prerequisites are unavailable.
    /// </summary>
    public string? SkipReason { get; private set; }

    /// <summary>Gets the HTTP client created for the named resource, skipping the test if unavailable.</summary>
    /// <param name="resourceName">The Aspire resource name.</param>
    /// <returns>The HTTP client for the resource.</returns>
    protected HttpClient Client(string resourceName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        SkipIfUnavailable();
        return _clients.TryGetValue(resourceName, out HttpClient? client)
            ? client
            : throw new InvalidOperationException(
                $"No HTTP client was created for resource '{resourceName}'. Ensure InitializeAsync has completed and the resource is declared in {nameof(Resources)}.");
    }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync() {
        AcquireDaprFixtureLock();
        _startupStopwatch.Start();

        using var startupCts = new CancellationTokenSource(StartupTimeout);

        try {
            IReadOnlyList<string> prerequisiteFailures = await GetPrerequisiteFailuresAsync().ConfigureAwait(false);
            if (prerequisiteFailures.Count > 0) {
                PrerequisitesAvailable = false;
                SkipReason = BuildPrerequisiteFailureMessage(prerequisiteFailures);
                _startupStopwatch.Stop();
                return;
            }

            // Point the Aspire-managed DAPR sidecars at the same placement/scheduler host ports that the
            // prerequisite probe found reachable. Without this the sidecars fall back to the daprd default
            // (localhost:50005/:50006); under a containerized `dapr init` (host ports 6050/6060) the actor
            // runtime can never reach placement, so the host blocks during startup and never serves /alive.
            string[] args =
            [
                .. ExtraAppArgs,
                $"--Dapr:PlacementHostAddress=localhost:{PlacementPort}",
                $"--Dapr:SchedulerHostAddress=localhost:{SchedulerPort}",
            ];

            _builder = await DistributedApplicationTestingBuilder
                .CreateAsync<TAppHost>(args, startupCts.Token)
                .ConfigureAwait(false);

            // Honor StartupTimeout during the build/graph-evaluation phase as well; MSBuild hangs during
            // project graph evaluation would otherwise never trip the timeout.
            _app = await _builder.BuildAsync(startupCts.Token).ConfigureAwait(false);

            await _app.StartAsync(startupCts.Token).ConfigureAwait(false);

            // Create HTTP clients for all resources through Aspire's CreateHttpClient so service-discovery
            // and the DelegatingHandler chain remain attached.
            foreach (AspireResource resource in Resources) {
                HttpClient client = await WaitForResourceAndCreateClientAsync(
                    resource, startupCts.Token).ConfigureAwait(false);
                _clients[resource.Name] = client;
            }

            // Wait for process liveness. Full Dapr readiness is covered by Dapr-specific integration tests.
            foreach (AspireResource resource in Resources) {
                if (resource.WaitForAliveness) {
                    await WaitForEndpointAsync(
                        _clients[resource.Name], resource.Name, AlivenessEndpointPath, resource.AlivenessTimeout, CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (startupCts.IsCancellationRequested) {
            _startupStopwatch.Stop();
            string diagnostics = BuildTimeoutDiagnostics();
            await DisposeAsync().ConfigureAwait(false);
            throw new TimeoutException(
                $"Aspire topology did not start within {StartupTimeout}. Startup ran for {_startupStopwatch.Elapsed}.{Environment.NewLine}{diagnostics}");
        }
        catch {
            _startupStopwatch.Stop();
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }

        _startupStopwatch.Stop();
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
        foreach (HttpClient client in _clients.Values) {
            client.Dispose();
        }

        _clients.Clear();

        if (_app is not null) {
            await _app.DisposeAsync().ConfigureAwait(false);
        }

        if (_builder is not null) {
            await _builder.DisposeAsync().ConfigureAwait(false);
        }

        _daprFixtureLock?.Dispose();

        GC.SuppressFinalize(this);
    }

    private void AcquireDaprFixtureLock() {
        string lockPath = Path.Combine(Path.GetTempPath(), FixtureLockName);
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

    private async Task WaitForEndpointAsync(HttpClient client, string resourceName, string endpointPath, TimeSpan timeout, CancellationToken cancellationToken) {
        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probeCts.CancelAfter(timeout);

        while (!probeCts.Token.IsCancellationRequested) {
            try {
                using HttpResponseMessage response = await client
                    .GetAsync(endpointPath, probeCts.Token)
                    .ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.OK) {
                    SetHealthDiagnostics(resourceName, response.StatusCode, null);
                    return;
                }

                SetHealthDiagnostics(resourceName, response.StatusCode, null);
            }
            catch (HttpRequestException ex) {
                SetHealthDiagnostics(resourceName, null, ex.Message);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested) {
                SetHealthDiagnostics(resourceName, null, ex.Message);
                if (probeCts.Token.IsCancellationRequested) {
                    break;
                }
            }

            try {
                await Task.Delay(TimeSpan.FromSeconds(2), probeCts.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) {
                break;
            }
        }

        throw new TimeoutException(
            $"Resource '{resourceName}' endpoint '{endpointPath}' did not return HTTP 200 within {timeout}. {GetHealthDiagnostic(resourceName)}");
    }

    private async Task<HttpClient> WaitForResourceAndCreateClientAsync(
        AspireResource resource,
        CancellationToken cancellationToken) {
        if (_app is null) {
            throw new InvalidOperationException("Aspire application has not been built.");
        }

        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probeCts.CancelAfter(resource.ReadinessTimeout);

        try {
            await _app.ResourceNotifications
                .WaitForResourceAsync(resource.Name, KnownResourceStates.Running, probeCts.Token)
                .ConfigureAwait(false);

            // WaitForResourceAsync(Running) returns before endpoint URLs are guaranteed to be published.
            // Poll Snapshot.Urls until the named endpoint appears (or the readiness timeout fires) — this
            // avoids the misleading "did not expose endpoint" error on a URL-publication race.
            UrlSnapshot endpoint = await WaitForEndpointPublishedAsync(
                resource.Name, resource.EndpointName, probeCts.Token).ConfigureAwait(false);

            // UrlSnapshot.Url can be null; throw a descriptive error rather than letting new Uri(null!)
            // surface a generic ArgumentNullException.
            if (string.IsNullOrWhiteSpace(endpoint.Url)) {
                throw new InvalidOperationException(
                    $"Resource '{resource.Name}' published endpoint '{resource.EndpointName}' but its URL value is null or whitespace.");
            }

            HttpClient client = _app.CreateHttpClient(resource.Name, resource.EndpointName);
            client.BaseAddress ??= new Uri(endpoint.Url);
            client.Timeout = resource.ClientTimeout;
            return client;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            string state = _app.ResourceNotifications.TryGetCurrentState(resource.Name, out ResourceEvent? current)
                ? current.Snapshot.State?.Text ?? "n/a"
                : "n/a";

            throw new TimeoutException(
                $"Resource '{resource.Name}' did not reach Running with endpoint '{resource.EndpointName}' published within {resource.ReadinessTimeout}. Last state: {state}.");
        }
    }

    private async Task<UrlSnapshot> WaitForEndpointPublishedAsync(
        string resourceName, string endpointName, CancellationToken cancellationToken) {
        if (_app is null) {
            throw new InvalidOperationException("Aspire application has not been built.");
        }

        while (true) {
            cancellationToken.ThrowIfCancellationRequested();

            if (_app.ResourceNotifications.TryGetCurrentState(resourceName, out ResourceEvent? resourceEvent)) {
                UrlSnapshot? endpoint = resourceEvent.Snapshot.Urls
                    .FirstOrDefault(url => string.Equals(url.Name, endpointName, StringComparison.OrdinalIgnoreCase));

                if (endpoint is not null) {
                    return endpoint;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<IReadOnlyList<string>> GetPrerequisiteFailuresAsync() {
        var failures = new List<string>();

        if (!IsDockerHealthy()) {
            failures.Add("Docker is not running or is not healthy enough for Aspire container orchestration");
        }

        if (!await IsRedisResponsiveAsync().ConfigureAwait(false)) {
            failures.Add($"Redis is not responding to PING on localhost:{RedisPort}");
        }

        if (!await IsPortReachableAsync("localhost", PlacementPort).ConfigureAwait(false)) {
            failures.Add($"Dapr placement service is not reachable on localhost:{PlacementPort}");
        }

        if (!await IsPortReachableAsync("localhost", SchedulerPort).ConfigureAwait(false)) {
            failures.Add($"Dapr scheduler service is not reachable on localhost:{SchedulerPort}");
        }

        return failures;
    }

    private static string BuildPrerequisiteFailureMessage(IReadOnlyList<string> failures)
        => "Aspire topology prerequisites are missing. Start Docker Desktop and run 'dapr init' before running these tests." + Environment.NewLine
            + string.Join(Environment.NewLine, failures.Select(f => $"  - {f}"));

    private static bool IsDockerHealthy() {
        try {
            using var process = Process.Start(new ProcessStartInfo {
                FileName = "docker",
                Arguments = "info --format \"{{.ServerVersion}}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null) {
                return false;
            }

            if (!process.WaitForExit(DockerProbeTimeout)) {
                try {
                    process.Kill(entireProcessTree: true);
                }
                catch {
                    // Best-effort cleanup for a hung Docker CLI probe.
                }

                return false;
            }

            return process.ExitCode == 0;
        }
        catch {
            return false;
        }
    }

    private static async Task<bool> IsPortReachableAsync(string host, int port) {
        try {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
            return true;
        }
        catch {
            return false;
        }
    }

    private static int ResolveRedisPort() {
        string? overrideValue = Environment.GetEnvironmentVariable("HEXALITH_EVENTSTORE_TEST_REDIS_PORT");
        if (!string.IsNullOrWhiteSpace(overrideValue)
            && int.TryParse(overrideValue, CultureInfo.InvariantCulture, out int parsed)
            && parsed is > 0 and < 65536) {
            return parsed;
        }

        return DefaultRedisPort;
    }

    private static async Task<bool> IsRedisResponsiveAsync() {
        try {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await client.ConnectAsync("localhost", RedisPort, cts.Token).ConfigureAwait(false);
            NetworkStream stream = client.GetStream();
            await using (stream.ConfigureAwait(false)) {
                byte[] ping = Encoding.ASCII.GetBytes("*1\r\n$4\r\nPING\r\n");
                await stream.WriteAsync(ping, cts.Token).ConfigureAwait(false);

                byte[] buffer = new byte[16];
                int total = 0;
                while (total < 5) {
                    int chunk = await stream.ReadAsync(buffer.AsMemory(total), cts.Token).ConfigureAwait(false);
                    if (chunk <= 0) {
                        break;
                    }

                    total += chunk;
                }

                return total >= 5 && Encoding.ASCII.GetString(buffer, 0, total).StartsWith("+PONG", StringComparison.Ordinal);
            }
        }
        catch {
            return false;
        }
    }

    private void SetHealthDiagnostics(string resourceName, HttpStatusCode? status, string? error)
        => _diagnostics[resourceName] = (status, error);

    private string GetHealthDiagnostic(string resourceName)
        => _diagnostics.TryGetValue(resourceName, out (HttpStatusCode? Status, string? Error) diagnostic)
            ? $"Last status: {diagnostic.Status?.ToString() ?? "n/a"}, Last error: {diagnostic.Error ?? "n/a"}"
            : "Last status: n/a, Last error: n/a";

    private string BuildTimeoutDiagnostics() {
        try {
            if (_app is null) {
                return "Application did not start (builder or build phase failed).";
            }

            var builder = new StringBuilder();
            _ = builder.Append(CultureInfo.InvariantCulture, $"Resources expected: {string.Join(", ", Resources.Select(r => r.Name))}. ");
            _ = builder.Append(CultureInfo.InvariantCulture, $"Startup duration: {_startupStopwatch.Elapsed}. ");
            foreach (AspireResource resource in Resources) {
                _ = builder.Append(CultureInfo.InvariantCulture, $"{resource.Name} => {GetHealthDiagnostic(resource.Name)}. ");
            }

            return builder.ToString();
        }
        catch (Exception ex) {
            return $"Failed to capture diagnostics: {ex.Message}";
        }
    }
}
