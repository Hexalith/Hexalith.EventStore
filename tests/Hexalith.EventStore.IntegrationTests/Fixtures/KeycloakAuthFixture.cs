using Aspire.Hosting;
using Aspire.Hosting.Testing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.IntegrationTests.Fixtures;

/// <summary>
/// Shared fixture that starts the full Aspire topology WITH Keycloak enabled for
/// Tier 3 E2E security tests (D11, R-001). Uses real OIDC tokens from Keycloak
/// instead of synthetic symmetric JWTs.
/// </summary>
public class KeycloakAuthFixture : IAsyncLifetime {
    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;
    private string? _previousEnableKeycloak;
    private string? _previousAspNetCoreEnvironment;
    private string? _previousDotNetEnvironment;
    private HttpClient? _eventStoreClient;
    private string? _keycloakTokenEndpoint;

    /// <summary>
    /// Gets the HTTP client for the EventStore service.
    /// </summary>
    public HttpClient EventStoreClient => _eventStoreClient ?? throw new InvalidOperationException(
        "Test infrastructure not initialized. Ensure InitializeAsync has completed.");

    /// <summary>
    /// Gets the running Aspire distributed application instance.
    /// </summary>
    public DistributedApplication App => _app ?? throw new InvalidOperationException(
        "Test infrastructure not initialized. Ensure InitializeAsync has completed.");

    /// <summary>
    /// Gets the Keycloak token endpoint URL for acquiring real OIDC tokens.
    /// </summary>
    public string KeycloakTokenEndpoint => _keycloakTokenEndpoint ?? throw new InvalidOperationException(
        "Keycloak not initialized. Ensure InitializeAsync has completed.");

    public async Task InitializeAsync() {
        // Enable Keycloak for real OIDC auth testing.
        _previousEnableKeycloak = Environment.GetEnvironmentVariable("EnableKeycloak");
        Environment.SetEnvironmentVariable("EnableKeycloak", "true");

        _previousAspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        _previousDotNetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");

        // 5-minute timeout: Keycloak container pull + realm import takes longer than no-Keycloak.
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        _builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Hexalith_EventStore_AppHost>()
            .ConfigureAwait(false);

        _ = _builder.Services.AddLogging(logging => {
            _ = logging.SetMinimumLevel(LogLevel.Debug);
            _ = logging.AddFilter(_builder.Environment.ApplicationName, LogLevel.Debug);
            _ = logging.AddFilter("Aspire.", LogLevel.Warning);
        });

        _ = _builder.Services.ConfigureHttpClientDefaults(clientBuilder => _ = clientBuilder.AddStandardResilienceHandler());

        _app = await _builder.BuildAsync().ConfigureAwait(false);
        await _app.StartAsync(cts.Token).ConfigureAwait(false);

        // Wait for Keycloak to be healthy (realm import must complete).
        _ = await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("keycloak", cts.Token)
            .WaitAsync(TimeSpan.FromMinutes(4), cts.Token)
            .ConfigureAwait(false);

        // Wait for EventStore to be healthy (depends on Keycloak for OIDC discovery).
        _ = await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("eventstore", cts.Token)
            .WaitAsync(TimeSpan.FromMinutes(3), cts.Token)
            .ConfigureAwait(false);

        // Wait for sample domain service.
        _ = await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("sample", cts.Token)
            .WaitAsync(TimeSpan.FromMinutes(3), cts.Token)
            .ConfigureAwait(false);

        _eventStoreClient = _app.CreateHttpClient("eventstore");
        _eventStoreClient.Timeout = TimeSpan.FromSeconds(60);

        // Resolve Keycloak token endpoint from the running container.
        HttpClient keycloakClient = _app.CreateHttpClient("keycloak");
        _keycloakTokenEndpoint = $"{keycloakClient.BaseAddress}realms/hexalith/protocol/openid-connect/token";
    }

    public async Task DisposeAsync() {
        _eventStoreClient?.Dispose();
        if (_app is not null) {
            await _app.DisposeAsync().ConfigureAwait(false);
        }

        if (_builder is not null) {
            await _builder.DisposeAsync().ConfigureAwait(false);
        }

        Environment.SetEnvironmentVariable("EnableKeycloak", _previousEnableKeycloak);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _previousAspNetCoreEnvironment);
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", _previousDotNetEnvironment);
    }
}

[CollectionDefinition("KeycloakAuthTests")]
public class KeycloakAuthTestCollection : ICollectionFixture<KeycloakAuthFixture>;
