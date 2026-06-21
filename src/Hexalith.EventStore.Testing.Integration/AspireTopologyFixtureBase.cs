using System.Diagnostics;
using System.Net;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Hexalith.EventStore.Testing.Integration;

/// <summary>
/// Base xUnit fixture that boots the full Aspire AppHost topology (with DAPR sidecars) for a domain
/// module and creates HTTP clients for smoke tests. Implements <see cref="IAsyncLifetime"/> for
/// xUnit lifecycle management.
/// </summary>
/// <typeparam name="TAppHost">The Aspire AppHost project type to boot.</typeparam>
/// <remarks>
/// <para>
/// This fixture verifies <strong>process liveness</strong>, not full readiness. It waits for
/// resources to reach <c>Running</c> state and for the <c>/alive</c> endpoint to return
/// HTTP 200 — that proves the host is responding to HTTP, not that every dependency
/// (database connections, Dapr sidecars, downstream services) is ready to serve traffic.
/// </para>
/// <para>
/// Full Dapr readiness (placement registration, sidecar handshake, state-store availability)
/// is covered by Dapr-specific integration tests that exercise the actor/state pipeline
/// end-to-end, not by this liveness smoke check.
/// </para>
/// <para>
/// A domain module subclasses this base and supplies its <see cref="ResourceNames"/>,
/// <see cref="AlivenessResourceNames"/>, and any <see cref="ExtraAppArgs"/>, then exposes typed
/// client accessors over <see cref="Client(string)"/>.
/// </para>
/// </remarks>
public abstract class AspireTopologyFixtureBase<TAppHost> : IAsyncLifetime
    where TAppHost : class {
    private static readonly int PlacementPort = DaprLocalEndpoints.PlacementPort;
    private static readonly int SchedulerPort = DaprLocalEndpoints.SchedulerPort;

    // The Redis prerequisite targets the `dapr init`-managed Redis (which DAPR sidecars use as their
    // state-store and pub-sub backend). dapr init defaults to localhost:6379, but developers can run
    // dapr init on a non-default port; the port can be overridden without editing the fixture. This
    // probe does NOT target an Aspire-managed Redis: the AppHost does not currently manage its own
    // Redis resource. If the AppHost ever takes over Redis management, switch this probe to read the
    // dynamically allocated port from the Aspire resource configuration instead.
    private readonly int _redisPort = DaprDiagnostics.ResolveRedisPort();
    private static readonly TimeSpan DockerProbeTimeout = TimeSpan.FromSeconds(5);

    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;
    private readonly Dictionary<string, HttpClient> _clients = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (HttpStatusCode? Status, string? Error)> _healthDiagnostics = new(StringComparer.Ordinal);
    private FileStream? _daprFixtureLock;
    private readonly Stopwatch _startupStopwatch = new();

    /// <summary>Gets the timeout for the full topology build/start phase.</summary>
    protected virtual TimeSpan StartupTimeout => TimeSpan.FromMinutes(6);

    /// <summary>Gets the per-resource readiness/aliveness timeout.</summary>
    protected virtual TimeSpan ResourceReadinessTimeout => TimeSpan.FromMinutes(4);

    /// <summary>
    /// Gets the per-request <see cref="HttpClient"/> timeout for the created resource clients. The
    /// heaviest end-to-end tests budget several minutes of wall-clock for a flow; keeping this aligned
    /// with those budgets prevents a single slow cold request (first actor activation + DAPR
    /// placement-table dissemination) from failing an otherwise-passing flow.
    /// </summary>
    protected virtual TimeSpan RequestTimeout => TimeSpan.FromMinutes(3);

    private const string AlivenessEndpointPath = "/alive";

    /// <summary>Gets the Aspire resource names to start and create clients for.</summary>
    protected abstract IReadOnlyList<string> ResourceNames { get; }

    /// <summary>Gets the subset of <see cref="ResourceNames"/> that must pass the <c>/alive</c> check.</summary>
    protected abstract IReadOnlyList<string> AlivenessResourceNames { get; }

    /// <summary>Gets any extra AppHost arguments (e.g. <c>--EnableKeycloak=false</c>).</summary>
    protected virtual IReadOnlyList<string> ExtraAppArgs => [];

    /// <summary>
    /// Gets a value indicating whether local DAPR prerequisites were available during fixture startup.
    /// </summary>
    public bool PrerequisitesAvailable { get; private set; } = true;

    /// <summary>
    /// Gets the skip reason when local DAPR prerequisites are unavailable.
    /// </summary>
    public string? SkipReason { get; private set; }

    /// <summary>
    /// Gets the HTTP client for the named Aspire resource. Available after
    /// <see cref="InitializeAsync"/> completes.
    /// </summary>
    /// <param name="resourceName">The Aspire resource name (must be one of <see cref="ResourceNames"/>).</param>
    public HttpClient Client(string resourceName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        SkipIfUnavailable();
        return _clients.TryGetValue(resourceName, out HttpClient? client)
            ? client
            : throw new InvalidOperationException(
                $"No HTTP client was created for resource '{resourceName}'. Ensure it is listed in {nameof(ResourceNames)} and InitializeAsync has completed.");
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
            string[] appArgs =
            [
                .. ExtraAppArgs,
                $"--Dapr:PlacementHostAddress=localhost:{PlacementPort}",
                $"--Dapr:SchedulerHostAddress=localhost:{SchedulerPort}",
            ];

            _builder = await DistributedApplicationTestingBuilder
                .CreateAsync<TAppHost>(appArgs, startupCts.Token)
                .ConfigureAwait(false);

            // Honor StartupTimeout during the build/graph-evaluation phase as well; MSBuild hangs during
            // project graph evaluation would otherwise never trip the timeout.
            _app = await _builder.BuildAsync(startupCts.Token).ConfigureAwait(false);

            await _app.StartAsync(startupCts.Token).ConfigureAwait(false);

            StartResourceLogCaptureIfRequested();

            // Create HTTP clients for all resources. Clients are built through Aspire's
            // _app.CreateHttpClient(resourceName, endpointName) so service-discovery and the
            // DelegatingHandler chain remain attached, and HttpClient Timeout is configured
            // inline at construction time rather than mutated after first use.
            foreach (string resourceName in ResourceNames) {
                _clients[resourceName] = await WaitForResourceAndCreateClientAsync(
                    resourceName, "http", RequestTimeout, ResourceReadinessTimeout, startupCts.Token).ConfigureAwait(false);
            }

            // Wait for process liveness. Full Dapr readiness is covered by Dapr-specific integration tests.
            foreach (string resourceName in AlivenessResourceNames) {
                await WaitForEndpointAsync(
                    _clients[resourceName], resourceName, AlivenessEndpointPath, ResourceReadinessTimeout, CancellationToken.None).ConfigureAwait(false);
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

    // Temporary diagnostic: when HEXALITH_EVENTSTORE_TEST_DUMP_LOGS=1, stream every resource's console
    // output to /tmp/aspire-logs-<resource>.log so a hanging command flow can be root-caused.
    private void StartResourceLogCaptureIfRequested() {
        if (_app is null
            || DaprTestEnvironment.GetVariable("HEXALITH_EVENTSTORE_TEST_DUMP_LOGS", "HEXALITH_TENANTS_TEST_DUMP_LOGS") != "1") {
            return;
        }

        ResourceLoggerService loggerService = _app.Services.GetRequiredService<ResourceLoggerService>();
        DistributedApplicationModel model = _app.Services.GetRequiredService<DistributedApplicationModel>();

        foreach (IResource resource in model.Resources) {
            string name = resource.Name;
            string path = Path.Combine(Path.GetTempPath(), $"aspire-logs-{name}.log");
            _ = Task.Run(async () => {
                try {
                    await foreach (IReadOnlyList<LogLine> batch in loggerService.WatchAsync(resource).ConfigureAwait(false)) {
                        await File.AppendAllLinesAsync(path, batch.Select(line => line.Content)).ConfigureAwait(false);
                    }
                }
                catch {
                    // Best-effort diagnostic capture.
                }
            });
        }
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
        string resourceName,
        string endpointName,
        TimeSpan clientTimeout,
        TimeSpan readinessTimeout,
        CancellationToken cancellationToken) {
        if (_app is null) {
            throw new InvalidOperationException("Aspire application has not been built.");
        }

        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probeCts.CancelAfter(readinessTimeout);

        try {
            await _app.ResourceNotifications
                .WaitForResourceAsync(resourceName, KnownResourceStates.Running, probeCts.Token)
                .ConfigureAwait(false);

            // WaitForResourceAsync(Running) returns before endpoint URLs are guaranteed to be
            // published. Poll Snapshot.Urls until the named endpoint appears (or the readiness
            // timeout fires) — this avoids the misleading "did not expose endpoint" error that
            // the previous one-shot snapshot check raised on a URL-publication race.
            UrlSnapshot endpoint = await WaitForEndpointPublishedAsync(
                resourceName, endpointName, probeCts.Token).ConfigureAwait(false);

            // UrlSnapshot.Url can be null; throw a descriptive error rather than letting
            // new Uri(null!) surface a generic ArgumentNullException.
            if (string.IsNullOrWhiteSpace(endpoint.Url)) {
                throw new InvalidOperationException(
                    $"Resource '{resourceName}' published endpoint '{endpointName}' but its URL value is null or whitespace.");
            }

            // Use Aspire's CreateHttpClient so service-discovery handlers, retry policies, and
            // tracing DelegatingHandlers stay attached; set Timeout in the same statement so it
            // is in effect before the first request is issued.
            HttpClient client = _app.CreateHttpClient(resourceName, endpointName);
            client.BaseAddress ??= new Uri(endpoint.Url);
            client.Timeout = clientTimeout;
            return client;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            string state = _app.ResourceNotifications.TryGetCurrentState(resourceName, out ResourceEvent? current)
                ? current.Snapshot.State?.Text ?? "n/a"
                : "n/a";

            throw new TimeoutException(
                $"Resource '{resourceName}' did not reach Running with endpoint '{endpointName}' published within {readinessTimeout}. Last state: {state}.");
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

            try {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                throw;
            }
        }
    }

    private async Task<IReadOnlyList<string>> GetPrerequisiteFailuresAsync() {
        var failures = new List<string>();

        if (!IsDockerHealthy()) {
            failures.Add("Docker is not running or is not healthy enough for Aspire container orchestration");
        }

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

    private void SetHealthDiagnostics(string resourceName, HttpStatusCode? status, string? error)
        => _healthDiagnostics[resourceName] = (status, error);

    private string GetHealthDiagnostic(string resourceName)
        => _healthDiagnostics.TryGetValue(resourceName, out (HttpStatusCode? Status, string? Error) diagnostic)
            ? $"Last status: {diagnostic.Status?.ToString() ?? "n/a"}, Last error: {diagnostic.Error ?? "n/a"}"
            : "Last status: n/a, Last error: n/a";

    private string BuildTimeoutDiagnostics() {
        try {
            if (_app is null) {
                return "Application did not start (builder or build phase failed).";
            }

            string resources = string.Join(", ", ResourceNames);
            string perResource = string.Join(" ", ResourceNames.Select(name => $"{name} => {GetHealthDiagnostic(name)}."));
            return $"Resources expected: {resources}. Startup duration: {_startupStopwatch.Elapsed}. {perResource}";
        }
        catch (Exception ex) {
            return $"Failed to capture diagnostics: {ex.Message}";
        }
    }
}
