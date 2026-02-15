namespace Hexalith.EventStore.IntegrationTests.Security;

using System.Net.Http;

using global::Aspire.Hosting;
using global::Aspire.Hosting.Testing;

using Hexalith.EventStore.IntegrationTests.Helpers;

/// <summary>
/// Shared fixture that starts the full Aspire topology with Keycloak ONCE
/// for all E2E security tests. Implements <see cref="IAsyncLifetime"/> so xUnit
/// creates/disposes it around the test collection lifetime.
/// Tests access the running topology via <see cref="CommandApiClient"/>,
/// <see cref="App"/>, and <see cref="GetTokenAsync"/>.
/// </summary>
public class AspireTopologyFixture : IAsyncLifetime
{
    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;
    private string? _previousEnableKeycloak;
    private HttpClient? _commandApiClient;
    private string? _keycloakBaseUrl;

    /// <summary>
    /// Gets the HTTP client for the CommandApi service.
    /// Available after <see cref="InitializeAsync"/> completes.
    /// </summary>
    public HttpClient CommandApiClient => _commandApiClient ?? throw new InvalidOperationException(
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

    public async Task InitializeAsync()
    {
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

        // Create HTTP client for CommandApi
        _commandApiClient = _app.CreateHttpClient("commandapi");

        // Get the Keycloak base URL for token acquisition
        var keycloakEndpoint = _app.GetEndpoint("keycloak", "http");
        _keycloakBaseUrl = keycloakEndpoint.ToString().TrimEnd('/');
    }

    public async Task DisposeAsync()
    {
        _commandApiClient?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync().ConfigureAwait(false);
        }

        if (_builder is not null)
        {
            await _builder.DisposeAsync().ConfigureAwait(false);
        }

        // Restore prior process setting to avoid leaking fixture state across test runs.
        Environment.SetEnvironmentVariable("EnableKeycloak", _previousEnableKeycloak);
    }

    /// <summary>
    /// Acquires a real OIDC token from Keycloak for the specified test user (D11, Rule #16).
    /// </summary>
    public async Task<string> GetTokenAsync(string username, string password)
    {
        string tokenEndpoint = $"{KeycloakBaseUrl}/realms/hexalith/protocol/openid-connect/token";
        return await KeycloakTokenHelper
            .AcquireTokenAsync(tokenEndpoint, "hexalith-eventstore", username, password)
            .ConfigureAwait(false);
    }
}
