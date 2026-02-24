
using System.Net;

using global::Aspire.Hosting;
using global::Aspire.Hosting.Testing;

namespace Hexalith.EventStore.IntegrationTests.Fixtures;
/// <summary>
/// Shared fixture that starts the full Aspire topology WITHOUT Keycloak for Tier 3
/// contract tests. Uses symmetric key JWT authentication via <see cref="TestJwtTokenGenerator"/>
/// for fast test execution (Rule #16: synthetic JWTs for integration tests only).
/// Implements <see cref="IAsyncLifetime"/> so xUnit manages lifecycle around the test collection.
/// </summary>
public class AspireContractTestFixture : IAsyncLifetime {
    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;
    private string? _previousEnableKeycloak;
    private HttpClient? _commandApiClient;

    /// <summary>
    /// Gets the HTTP client for the CommandApi service.
    /// Available after <see cref="InitializeAsync"/> completes.
    /// </summary>
    public HttpClient CommandApiClient => _commandApiClient ?? throw new InvalidOperationException(
        "Test infrastructure not initialized. Ensure InitializeAsync has completed.");

    /// <summary>
    /// Gets the running Aspire distributed application instance.
    /// </summary>
    public DistributedApplication App => _app ?? throw new InvalidOperationException(
        "Test infrastructure not initialized. Ensure InitializeAsync has completed.");

    public async Task InitializeAsync() {
        // Disable Keycloak for fast contract tests -- use symmetric key JWT auth instead.
        _previousEnableKeycloak = Environment.GetEnvironmentVariable("EnableKeycloak");
        Environment.SetEnvironmentVariable("EnableKeycloak", "false");

        // 3-minute timeout for Aspire topology startup (no Keycloak container to pull/start).
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        _builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Hexalith_EventStore_AppHost>()
            .ConfigureAwait(false);

        _app = await _builder.BuildAsync().ConfigureAwait(false);
        await _app.StartAsync(cts.Token).ConfigureAwait(false);

        // Create HTTP client for CommandApi
        _commandApiClient = _app.CreateHttpClient("commandapi");
        _commandApiClient.Timeout = TimeSpan.FromSeconds(60);

        // Wait for CommandApi endpoint to respond (any status code means the service is up)
        await WaitForEndpointAsync(
            _commandApiClient,
            "/api/v1/commands",
            expectedStatusCodes: [HttpStatusCode.Unauthorized, HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound],
            timeout: TimeSpan.FromMinutes(3),
            pollInterval: TimeSpan.FromSeconds(2)).ConfigureAwait(false);

        // Wait for CommandApi health endpoint (includes Dapr sidecar, state store, pub/sub checks).
        await WaitForEndpointAsync(
            _commandApiClient,
            "/health",
            expectedStatusCodes: [HttpStatusCode.OK],
            timeout: TimeSpan.FromMinutes(3),
            pollInterval: TimeSpan.FromSeconds(3)).ConfigureAwait(false);

        // Wait for the sample domain service to be ready.
        using var sampleProbeClient = _app.CreateHttpClient("sample");
        sampleProbeClient.Timeout = TimeSpan.FromSeconds(15);
        await WaitForEndpointAsync(
            sampleProbeClient,
            "/health",
            expectedStatusCodes: [HttpStatusCode.OK],
            timeout: TimeSpan.FromMinutes(3),
            pollInterval: TimeSpan.FromSeconds(2)).ConfigureAwait(false);
    }

    public async Task DisposeAsync() {
        _commandApiClient?.Dispose();
        if (_app is not null) {
            await _app.DisposeAsync().ConfigureAwait(false);
        }

        if (_builder is not null) {
            await _builder.DisposeAsync().ConfigureAwait(false);
        }

        Environment.SetEnvironmentVariable("EnableKeycloak", _previousEnableKeycloak);
    }

    private static async Task WaitForEndpointAsync(
        HttpClient client,
        string url,
        IReadOnlyCollection<HttpStatusCode> expectedStatusCodes,
        TimeSpan timeout,
        TimeSpan pollInterval) {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        Exception? lastException = null;

        while (DateTimeOffset.UtcNow < deadline) {
            try {
                using HttpResponseMessage response = await client
                    .GetAsync(url)
                    .ConfigureAwait(false);

                if (expectedStatusCodes.Contains(response.StatusCode)) {
                    return;
                }
            }
            catch (Exception ex) {
                lastException = ex;
            }

            await Task.Delay(pollInterval).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"Endpoint did not become ready within {timeout}. Url: {url}. Last error: {lastException?.Message ?? "n/a"}");
    }
}
