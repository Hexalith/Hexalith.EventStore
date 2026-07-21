
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

using global::Aspire.Hosting;
using global::Aspire.Hosting.Testing;

using Hexalith.EventStore.IntegrationTests.Helpers;

using StackExchange.Redis;

namespace Hexalith.EventStore.IntegrationTests.Security;
/// <summary>
/// Shared fixture that starts the full Aspire topology with Keycloak ONCE
/// for all E2E security tests. Implements <see cref="IAsyncLifetime"/> so xUnit
/// creates/disposes it around the test collection lifetime.
/// Tests access the running topology via <see cref="EventStoreClient"/>,
/// <see cref="App"/>, and <see cref="GetTokenAsync"/>.
/// </summary>
public class AspireTopologyFixture : IAsyncLifetime {
    private const string ProjectionDeliveryWriterProtocolStateKey = "projection-delivery-writer-protocol";
    private const string ProjectionDeliveryWriterProtocolHealthCheck = "projection-delivery-writer-protocol";
    private const string RedisEndpoint = "localhost:6379";
    private const string SecurityResourceName = "security";
    private const int ProjectionDeliveryWriterProtocolSchemaVersion = 1;
    private const int ProjectionDeliveryWriterProtocolVersion = 2;

    private static readonly TimeSpan s_cutoverRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, string?> _envSnapshot = new(StringComparer.Ordinal);

    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;
    private HttpClient? _eventStoreClient;
    private string? _keycloakBaseUrl;
    private byte[]? _writerProtocolMarkerSnapshot;
    private TimeSpan? _writerProtocolMarkerSnapshotExpiry;
    private string? _writerProtocolMarkerSourceCommit;
    private bool _writerProtocolMarkerIsolated;

    /// <summary>
    /// Gets the HTTP client for the EventStore service.
    /// Available after <see cref="InitializeAsync"/> completes.
    /// </summary>
    public HttpClient EventStoreClient => _eventStoreClient ?? throw new InvalidOperationException(
        "Test infrastructure not initialized. Ensure InitializeAsync has completed.");

    /// <summary>
    /// Gets the Keycloak base URL for token acquisition.
    /// Available after <see cref="InitializeAsync"/> completes.
    /// </summary>
    public string KeycloakBaseUrl => _keycloakBaseUrl ?? throw new InvalidOperationException(
        "Test infrastructure not initialized. Ensure InitializeAsync has completed.");

    /// <summary>
    /// Gets the running Aspire distributed application instance.
    /// Available after <see cref="InitializeAsync"/> completes.
    /// </summary>
    public DistributedApplication App => _app ?? throw new InvalidOperationException(
        "Test infrastructure not initialized. Ensure InitializeAsync has completed.");

    public async ValueTask InitializeAsync() {
        // Ensure E2E tests always start Keycloak regardless of developer-local settings.
        // Program.cs allows disabling Keycloak via EnableKeycloak=false for standalone runs,
        // but this test suite depends on Keycloak being available.
        SnapshotAndSet("EnableKeycloak", "true");
        SnapshotAndSet("EventStore__Actors__AggregateActorTypeName", $"AggregateActorIntegration{Guid.NewGuid():N}");

        // Opt-in container reuse for faster LOCAL test iteration (default OFF so CI stays
        // cold/clean). Set KEYCLOAK_TEST_REUSE=true to keep the Keycloak container warm
        // between `dotnet test` runs — only the first run pays the cold-start.
        if (bool.TryParse(Environment.GetEnvironmentVariable("KEYCLOAK_TEST_REUSE")?.Trim(), out bool reuseKeycloak)
            && reuseKeycloak) {
            SnapshotAndSet("KeycloakPersistent", "true");
        }

        try {
            // 5-minute timeout for full Aspire topology startup including container pulls,
            // Keycloak realm import, and service readiness. Prevents indefinite hangs in CI.
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            _builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.Hexalith_EventStore_AppHost>()
                .ConfigureAwait(false);

            _app = await _builder.BuildAsync().ConfigureAwait(false);
            await _app.StartAsync(cts.Token).ConfigureAwait(false);

            // Create HTTP client for EventStore
            _eventStoreClient = _app.CreateHttpClient("eventstore");
            _eventStoreClient.Timeout = TimeSpan.FromSeconds(60);

            // Get the Keycloak base URL for token acquisition
            Uri keycloakEndpoint = _app.GetEndpoint(SecurityResourceName, "http");
            _keycloakBaseUrl = keycloakEndpoint.ToString().TrimEnd('/');

            // Wait for infrastructure readiness to avoid startup race conditions in E2E tests.
            await WaitForEndpointAsync(
                _eventStoreClient,
                "/api/v1/commands",
                expectedStatusCodes: [HttpStatusCode.Unauthorized, HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound],
                timeout: TimeSpan.FromMinutes(5),
                pollInterval: TimeSpan.FromSeconds(2)).ConfigureAwait(false);

            using var keycloakProbeClient = new HttpClient {
                Timeout = TimeSpan.FromSeconds(15),
            };

            await WaitForEndpointAsync(
                keycloakProbeClient,
                $"{_keycloakBaseUrl}/realms/hexalith/.well-known/openid-configuration",
                expectedStatusCodes: [HttpStatusCode.OK],
                timeout: TimeSpan.FromMinutes(5),
                pollInterval: TimeSpan.FromSeconds(2)).ConfigureAwait(false);

            // Wait for Keycloak to fully complete realm import (users, clients).
            // The OIDC discovery endpoint can return 200 before user/client data is committed,
            // causing token acquisition to fail with 500 unknown_error.
            string adminToken = await WaitForTokenAcquisitionAsync(
                timeout: TimeSpan.FromMinutes(2),
                pollInterval: TimeSpan.FromSeconds(3)).ConfigureAwait(false);

            // Production correctly stays fail-closed until an operator records the v2 writer
            // cutover. This disposable topology has no legacy writers or durable production data,
            // so perform the equivalent explicit test cutover before requiring /health to be 200.
            string sourceCommit = await ReadExactSourceCommitAsync(cts.Token).ConfigureAwait(false);
            await EnsureProjectionDeliveryWriterProtocolAsync(adminToken, sourceCommit, cts.Token)
                .ConfigureAwait(false);

            // Wait for EventStore health endpoint (includes Dapr sidecar, state store, pub/sub checks).
            // Without this, actor invocations fail because the Dapr sidecar/placement service isn't ready.
            await WaitForEndpointAsync(
                _eventStoreClient,
                "/health",
                expectedStatusCodes: [HttpStatusCode.OK],
                timeout: TimeSpan.FromMinutes(3),
                pollInterval: TimeSpan.FromSeconds(3)).ConfigureAwait(false);

            // Wait for the sample domain service to be ready. The EventStore's actor pipeline
            // invokes the sample service via Dapr service invocation. Without this check,
            // the first command submission hangs until the sample sidecar becomes available.
            using HttpClient sampleProbeClient = _app.CreateHttpClient("sample");
            sampleProbeClient.Timeout = TimeSpan.FromSeconds(15);
            await WaitForEndpointAsync(
                sampleProbeClient,
                "/health",
                expectedStatusCodes: [HttpStatusCode.OK],
                timeout: TimeSpan.FromMinutes(3),
                pollInterval: TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch (Exception initializationException) {
            // xUnit does not invoke DisposeAsync when InitializeAsync throws (e.g. the 5-min topology
            // timeout), so restore the env-var snapshot here to keep this fixture's process-wide
            // mutations from leaking into the next serially-run collection.
            Exception? cleanupException = null;
            try {
                await SafeShutdownAsync().ConfigureAwait(false);
            }
            catch (Exception ex) {
                cleanupException = ex;
            }
            finally {
                RestoreEnvironmentSnapshot();
            }

            if (cleanupException is not null) {
                throw new AggregateException(initializationException, cleanupException);
            }

            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        try {
            await SafeShutdownAsync().ConfigureAwait(false);
        }
        finally {
            // Restore prior process settings to avoid leaking fixture state across test runs.
            RestoreEnvironmentSnapshot();
        }
    }

    private async Task SafeShutdownAsync() {
        Exception? markerRestorationException = null;
        try {
            await RestoreWriterProtocolMarkerAsync().ConfigureAwait(false);
        }
        catch (Exception ex) {
            markerRestorationException = ex;
        }

        _eventStoreClient?.Dispose();
        _eventStoreClient = null;

        if (_app is not null) {
            try {
                await _app.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception) {
                // Ignore shutdown faults so the env-var restore in DisposeAsync still runs.
            }

            _app = null;
        }

        if (_builder is not null) {
            try {
                await _builder.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception) {
                // Ignore shutdown faults so the env-var restore in DisposeAsync still runs.
            }

            _builder = null;
        }

        if (markerRestorationException is not null) {
            throw new InvalidOperationException(
                "The disposable security topology could not restore the store-global writer-protocol marker.",
                markerRestorationException);
        }
    }

    private void SnapshotAndSet(string name, string? newValue) {
        if (!_envSnapshot.ContainsKey(name)) {
            _envSnapshot[name] = Environment.GetEnvironmentVariable(name);
        }

        Environment.SetEnvironmentVariable(name, newValue);
    }

    private void RestoreEnvironmentSnapshot() {
        foreach (KeyValuePair<string, string?> entry in _envSnapshot) {
            Environment.SetEnvironmentVariable(entry.Key, entry.Value);
        }

        _envSnapshot.Clear();
    }

    /// <summary>
    /// Acquires a real OIDC token from Keycloak for the specified test user (D11, Rule #16).
    /// </summary>
    public async Task<string> GetTokenAsync(string username, string password) {
        string tokenEndpoint = $"{KeycloakBaseUrl}/realms/hexalith/protocol/openid-connect/token";
        return await KeycloakTokenHelper
            .AcquireTokenAsync(tokenEndpoint, "hexalith-eventstore", username, password)
            .ConfigureAwait(false);
    }

    private async Task<string> WaitForTokenAcquisitionAsync(TimeSpan timeout, TimeSpan pollInterval) {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        Exception? lastException = null;

        while (DateTimeOffset.UtcNow < deadline) {
            try {
                return await GetTokenAsync("admin-user", "admin-pass").ConfigureAwait(false);
            }
            catch (Exception ex) {
                lastException = ex;
            }

            await Task.Delay(pollInterval).ConfigureAwait(false);
        }

        // Capture Keycloak container logs to diagnose the 500 error.
        string keycloakLogs = await CaptureContainerLogsAsync(SecurityResourceName).ConfigureAwait(false);

        throw new TimeoutException(
            $"Keycloak token acquisition did not become ready within {timeout}. "
            + $"Last error: {lastException?.Message ?? "n/a"}"
            + Environment.NewLine
            + "--- Keycloak container logs (last 200 lines) ---"
            + Environment.NewLine
            + keycloakLogs);
    }

    private async Task EnsureProjectionDeliveryWriterProtocolAsync(
        string adminToken,
        string sourceCommit,
        CancellationToken cancellationToken) {
        using IConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions {
            EndPoints = { RedisEndpoint },
            ConnectTimeout = 5_000,
            SyncTimeout = 5_000,
            AbortOnConnectFail = false,
            AllowAdmin = false,
        }).ConfigureAwait(false);
        IDatabase database = redis.GetDatabase();
        await IsolateWriterProtocolMarkerAsync(database, sourceCommit, cancellationToken)
            .ConfigureAwait(false);
        string lastDiagnostic = "No activation attempt completed.";

        try {
            while (true) {
                cancellationToken.ThrowIfCancellationRequested();

                try {
                    ProjectionDeliveryWriterProtocolMarker? marker = await ReadWriterProtocolMarkerAsync(
                        database,
                        cancellationToken).ConfigureAwait(false);
                    if (marker is not null) {
                        AssertWriterProtocolMarker(marker, sourceCommit);
                        return;
                    }
                }
                catch (RedisException ex) {
                    lastDiagnostic = $"Redis marker read is not ready: {ex.Message}";
                    await Task.Delay(s_cutoverRetryDelay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                try {
                    using HttpResponseMessage health = await _eventStoreClient!
                        .GetAsync("/health", cancellationToken)
                        .ConfigureAwait(false);
                    string healthBody = await health.Content
                        .ReadAsStringAsync(cancellationToken)
                        .ConfigureAwait(false);
                    if (!ProjectionDeliveryWriterProtocolCutoverPolicy.WriterProtocolIsOnlyUnhealthyCheck(
                        healthBody,
                        ProjectionDeliveryWriterProtocolHealthCheck)) {
                        lastDiagnostic = $"EventStore dependencies are not ready for cutover. "
                            + $"Status={(int)health.StatusCode} ({health.StatusCode}); Body={healthBody}";
                        await Task.Delay(s_cutoverRetryDelay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    using var request = new HttpRequestMessage(
                        HttpMethod.Post,
                        "/api/v1/admin/projections/delivery-writer-protocol/activate");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
                    request.Content = JsonContent.Create(new {
                        CutoverCommit = sourceCommit,
                        BackupReference = "disposable-aspire-security-fixture-backup",
                        WritersQuiesced = true,
                        RetryWorkersQuiesced = true,
                        DowngradeProhibitedAcknowledged = true,
                    });

                    using HttpResponseMessage response = await _eventStoreClient
                        .SendAsync(request, cancellationToken)
                        .ConfigureAwait(false);
                    string body = await response.Content
                        .ReadAsStringAsync(cancellationToken)
                        .ConfigureAwait(false);
                    if (ShouldRetryActivationResponse(response.StatusCode)) {
                        lastDiagnostic = $"Activation has not produced a verified marker yet. "
                            + $"Status={(int)response.StatusCode} ({response.StatusCode}); Body={body}";
                        await Task.Delay(s_cutoverRetryDelay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"Unable to activate the disposable topology's projection delivery writer protocol. "
                        + $"Status={(int)response.StatusCode} ({response.StatusCode}); Body={body}");
                }
                catch (HttpRequestException ex) {
                    lastDiagnostic = $"EventStore cutover transport is not ready: {ex.Message}";
                    await Task.Delay(s_cutoverRetryDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested) {
                    lastDiagnostic = $"EventStore cutover request timed out before the startup budget elapsed: {ex.Message}";
                    await Task.Delay(s_cutoverRetryDelay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested) {
            throw new TimeoutException(
                "Projection delivery writer-protocol activation did not complete within the topology startup budget. "
                + lastDiagnostic,
                ex);
        }
    }

    private static void AssertWriterProtocolMarker(
        ProjectionDeliveryWriterProtocolMarker marker,
        string sourceCommit) {
        if (marker.SchemaVersion != ProjectionDeliveryWriterProtocolSchemaVersion
            || marker.WriterProtocolVersion != ProjectionDeliveryWriterProtocolVersion
            || !string.Equals(marker.CutoverCommit, sourceCommit, StringComparison.Ordinal)) {
            throw new InvalidOperationException(
                "The persisted projection delivery writer-protocol marker does not identify the exact test runtime. "
                + $"Expected schema={ProjectionDeliveryWriterProtocolSchemaVersion}, "
                + $"protocol={ProjectionDeliveryWriterProtocolVersion}, commit={sourceCommit}; "
                + $"actual schema={marker.SchemaVersion}, protocol={marker.WriterProtocolVersion}, "
                + $"commit={marker.CutoverCommit}.");
        }
    }

    internal static bool ShouldRetryActivationResponse(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.OK
            || statusCode == HttpStatusCode.Conflict
            || ProjectionDeliveryWriterProtocolCutoverPolicy.IsTransientActivationStatus(statusCode);

    private async Task IsolateWriterProtocolMarkerAsync(
        IDatabase database,
        string sourceCommit,
        CancellationToken cancellationToken) {
        if (_writerProtocolMarkerIsolated) {
            throw new InvalidOperationException(
                "The disposable topology attempted to isolate the writer-protocol marker more than once.");
        }

        _writerProtocolMarkerSnapshot = await database
            .KeyDumpAsync(ProjectionDeliveryWriterProtocolStateKey)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        if (_writerProtocolMarkerSnapshot is not null) {
            _writerProtocolMarkerSnapshotExpiry = await database
                .KeyTimeToLiveAsync(ProjectionDeliveryWriterProtocolStateKey)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        _writerProtocolMarkerSourceCommit = sourceCommit;
        _writerProtocolMarkerIsolated = true;
        _ = await database
            .KeyDeleteAsync(ProjectionDeliveryWriterProtocolStateKey)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task RestoreWriterProtocolMarkerAsync() {
        if (!_writerProtocolMarkerIsolated) {
            return;
        }

        using IConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions {
            EndPoints = { RedisEndpoint },
            ConnectTimeout = 5_000,
            SyncTimeout = 5_000,
            AbortOnConnectFail = false,
            AllowAdmin = false,
        }).ConfigureAwait(false);
        IDatabase database = redis.GetDatabase();
        ProjectionDeliveryWriterProtocolMarker? current = await ReadWriterProtocolMarkerAsync(
            database,
            CancellationToken.None).ConfigureAwait(false);
        if (current is not null
            && !string.Equals(
                current.CutoverCommit,
                _writerProtocolMarkerSourceCommit,
                StringComparison.Ordinal)) {
            throw new InvalidOperationException(
                "The store-global writer-protocol marker changed after fixture isolation; "
                + "refusing to overwrite state owned by another topology.");
        }

        _ = await database.KeyDeleteAsync(ProjectionDeliveryWriterProtocolStateKey).ConfigureAwait(false);
        if (_writerProtocolMarkerSnapshot is not null) {
            await database.KeyRestoreAsync(
                ProjectionDeliveryWriterProtocolStateKey,
                _writerProtocolMarkerSnapshot,
                _writerProtocolMarkerSnapshotExpiry).ConfigureAwait(false);
        }

        await redis.CloseAsync(allowCommandsToComplete: true).ConfigureAwait(false);
        _writerProtocolMarkerSnapshot = null;
        _writerProtocolMarkerSnapshotExpiry = null;
        _writerProtocolMarkerSourceCommit = null;
        _writerProtocolMarkerIsolated = false;
    }

    private static async Task<ProjectionDeliveryWriterProtocolMarker?> ReadWriterProtocolMarkerAsync(
        IDatabase database,
        CancellationToken cancellationToken) {
        RedisValue payload = await database
            .HashGetAsync(ProjectionDeliveryWriterProtocolStateKey, "data")
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!payload.HasValue) {
            return null;
        }

        return JsonSerializer.Deserialize<ProjectionDeliveryWriterProtocolMarker>(
            payload.ToString(),
            s_jsonSerializerOptions)
            ?? throw new InvalidOperationException(
                "Redis returned an empty projection delivery writer-protocol marker payload.");
    }

    private static async Task<string> ReadExactSourceCommitAsync(CancellationToken cancellationToken) {
        var startInfo = new ProcessStartInfo {
            FileName = "git",
            WorkingDirectory = Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("rev-parse");
        startInfo.ArgumentList.Add("HEAD");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start git for exact source identity resolution.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        string commit = (await standardOutput.ConfigureAwait(false)).Trim();
        string error = (await standardError.ConfigureAwait(false)).Trim();
        if (process.ExitCode != 0
            || commit.Length != 40
            || commit.Any(static character => !Uri.IsHexDigit(character))) {
            throw new InvalidOperationException(
                $"Unable to resolve the exact runtime source commit. ExitCode={process.ExitCode}; "
                + $"Output={commit}; Error={error}");
        }

        return commit.ToLowerInvariant();
    }

    private sealed record ProjectionDeliveryWriterProtocolMarker(
        int SchemaVersion,
        int WriterProtocolVersion,
        string CutoverCommit);

    private static async Task<string> CaptureContainerLogsAsync(string nameFilter) {
        try {
            // Find container by label/name and capture its logs via docker CLI.
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "docker";
            process.StartInfo.Arguments = $"ps --filter \"name={nameFilter}\" --format \"{{{{.Names}}}}\" --no-trunc";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            _ = process.Start();

            string containerName = (await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false)).Trim();
            await process.WaitForExitAsync().ConfigureAwait(false);

            if (string.IsNullOrEmpty(containerName)) {
                // Fallback: list all containers to help debug the name pattern.
                using var psAll = new System.Diagnostics.Process();
                psAll.StartInfo.FileName = "docker";
                psAll.StartInfo.Arguments = "ps --format \"{{.Names}}\"";
                psAll.StartInfo.RedirectStandardOutput = true;
                psAll.StartInfo.UseShellExecute = false;
                psAll.StartInfo.CreateNoWindow = true;
                _ = psAll.Start();
                string allContainers = await psAll.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                await psAll.WaitForExitAsync().ConfigureAwait(false);

                return $"(no container found matching '{nameFilter}' via 'docker ps'. Available containers: {Environment.NewLine}{allContainers})";
            }

            // Take first container if multiple lines
            containerName = containerName.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();

            using var logsProcess = new System.Diagnostics.Process();
            logsProcess.StartInfo.FileName = "docker";
            logsProcess.StartInfo.Arguments = $"logs --tail 200 {containerName}";
            logsProcess.StartInfo.RedirectStandardOutput = true;
            logsProcess.StartInfo.RedirectStandardError = true;
            logsProcess.StartInfo.UseShellExecute = false;
            logsProcess.StartInfo.CreateNoWindow = true;
            _ = logsProcess.Start();

            string stdout = await logsProcess.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            string stderr = await logsProcess.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await logsProcess.WaitForExitAsync().ConfigureAwait(false);

            var combined = new StringBuilder();
            if (!string.IsNullOrEmpty(stdout)) {
                _ = combined.AppendLine(stdout);
            }

            if (!string.IsNullOrEmpty(stderr)) {
                _ = combined.AppendLine(stderr);
            }

            return combined.Length > 0 ? combined.ToString() : "(container found but no logs)";
        }
        catch (Exception ex) {
            return $"(failed to capture logs for '{nameFilter}': {ex.Message})";
        }
    }

    private static async Task WaitForEndpointAsync(
        HttpClient client,
        string url,
        IReadOnlyCollection<HttpStatusCode> expectedStatusCodes,
        TimeSpan timeout,
        TimeSpan pollInterval) {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        Exception? lastException = null;
        string? lastBodySnippet = null;

        while (DateTimeOffset.UtcNow < deadline) {
            try {
                using HttpResponseMessage response = await client
                    .GetAsync(url)
                    .ConfigureAwait(false);

                HttpStatusCode? lastStatusCode = response.StatusCode;
                if (response.Content is not null) {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(body)) {
                        lastBodySnippet = body.Length <= 500 ? body : body[..500];
                    }
                }

                if (expectedStatusCodes.Contains(response.StatusCode)) {
                    return;
                }
            }
            catch (Exception ex) {
                lastException = ex;
            }

            await Task.Delay(pollInterval).ConfigureAwait(false);
        }

        // Capture Dapr sidecar and Redis logs if we timed out waiting for health.
        // In Aspire Testing the sidecar runs as a process (not a container), so
        // "eventstore-dapr" may not appear in docker ps; the message below reflects that.
        string daprLogs = await CaptureContainerLogsAsync("eventstore-dapr").ConfigureAwait(false);
        string redisLogs = await CaptureContainerLogsAsync("redis").ConfigureAwait(false);

        throw new TimeoutException(
            $"Endpoint did not become ready within {timeout}. Url: {url}. Last error: {lastException?.Message ?? "n/a"}. Last body: {lastBodySnippet ?? "n/a"}"
            + Environment.NewLine
            + "--- Dapr sidecar logs (last 200 lines; in Aspire Testing sidecar runs as process, not container) ---"
            + Environment.NewLine
            + daprLogs
            + Environment.NewLine
            + "--- Redis logs (last 200 lines) ---"
            + Environment.NewLine
            + redisLogs);
    }
}
