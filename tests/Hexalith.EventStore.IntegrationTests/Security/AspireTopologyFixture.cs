
using System.Net;
using System.Text;

using global::Aspire.Hosting;
using global::Aspire.Hosting.Testing;

using Hexalith.EventStore.IntegrationTests.Helpers;

namespace Hexalith.EventStore.IntegrationTests.Security;
/// <summary>
/// Shared fixture that starts the full Aspire topology with Keycloak ONCE
/// for all E2E security tests. Implements <see cref="IAsyncLifetime"/> so xUnit
/// creates/disposes it around the test collection lifetime.
/// Tests access the running topology via <see cref="EventStoreClient"/>,
/// <see cref="App"/>, and <see cref="GetTokenAsync"/>.
/// </summary>
public class AspireTopologyFixture : IAsyncLifetime {
    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;
    private string? _previousEnableKeycloak;
    private HttpClient? _eventStoreClient;
    private string? _keycloakBaseUrl;

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
        _previousEnableKeycloak = Environment.GetEnvironmentVariable("EnableKeycloak");
        Environment.SetEnvironmentVariable("EnableKeycloak", "true");

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
        Uri keycloakEndpoint = _app.GetEndpoint("keycloak", "http");
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
        await WaitForTokenAcquisitionAsync(
            timeout: TimeSpan.FromMinutes(2),
            pollInterval: TimeSpan.FromSeconds(3)).ConfigureAwait(false);

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

    public async ValueTask DisposeAsync() {
        _eventStoreClient?.Dispose();
        if (_app is not null) {
            await _app.DisposeAsync().ConfigureAwait(false);
        }

        if (_builder is not null) {
            await _builder.DisposeAsync().ConfigureAwait(false);
        }

        // Restore prior process setting to avoid leaking fixture state across test runs.
        Environment.SetEnvironmentVariable("EnableKeycloak", _previousEnableKeycloak);
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

    private async Task WaitForTokenAcquisitionAsync(TimeSpan timeout, TimeSpan pollInterval) {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        Exception? lastException = null;

        while (DateTimeOffset.UtcNow < deadline) {
            try {
                _ = await GetTokenAsync("admin-user", "admin-pass").ConfigureAwait(false);
                return;
            }
            catch (Exception ex) {
                lastException = ex;
            }

            await Task.Delay(pollInterval).ConfigureAwait(false);
        }

        // Capture Keycloak container logs to diagnose the 500 error.
        string keycloakLogs = await CaptureContainerLogsAsync("keycloak").ConfigureAwait(false);

        throw new TimeoutException(
            $"Keycloak token acquisition did not become ready within {timeout}. "
            + $"Last error: {lastException?.Message ?? "n/a"}"
            + Environment.NewLine
            + "--- Keycloak container logs (last 200 lines) ---"
            + Environment.NewLine
            + keycloakLogs);
    }

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
