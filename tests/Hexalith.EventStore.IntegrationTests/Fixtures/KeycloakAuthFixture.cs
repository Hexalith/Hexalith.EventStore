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
    private const string SecurityResourceName = "security";

    private readonly Dictionary<string, string?> _envSnapshot = new(StringComparer.Ordinal);

    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;
    private HttpClient? _eventStoreClient;

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
    public string KeycloakTokenEndpoint {
        get => field ?? throw new InvalidOperationException(
        "Keycloak not initialized. Ensure InitializeAsync has completed."); private set;
    }

    public async ValueTask InitializeAsync() {
        // Enable Keycloak for real OIDC auth testing.
        SnapshotAndSet("EnableKeycloak", "true");

        // Opt-in container reuse for faster LOCAL test iteration (default OFF so CI stays
        // cold/clean). Set KEYCLOAK_TEST_REUSE=true to keep the Keycloak container warm
        // between `dotnet test` runs — only the first run pays the cold-start.
        if (bool.TryParse(Environment.GetEnvironmentVariable("KEYCLOAK_TEST_REUSE")?.Trim(), out bool reuseKeycloak)
            && reuseKeycloak) {
            SnapshotAndSet("KeycloakPersistent", "true");
        }

        SnapshotAndSet("ASPNETCORE_ENVIRONMENT", "Development");
        SnapshotAndSet("DOTNET_ENVIRONMENT", "Development");

        try {
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
                .WaitForResourceHealthyAsync(SecurityResourceName, cts.Token)
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
            HttpClient keycloakClient = _app.CreateHttpClient(SecurityResourceName);
            KeycloakTokenEndpoint = $"{keycloakClient.BaseAddress}realms/hexalith/protocol/openid-connect/token";
        }
        catch {
            // xUnit does not invoke DisposeAsync when InitializeAsync throws (e.g. the 5-min topology
            // timeout), so restore the env-var snapshot here to keep this fixture's process-wide
            // mutations from leaking into the next serially-run collection.
            await SafeShutdownAsync().ConfigureAwait(false);
            RestoreEnvironmentSnapshot();
            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        try {
            await SafeShutdownAsync().ConfigureAwait(false);
        }
        finally {
            RestoreEnvironmentSnapshot();
        }
    }

    private async Task SafeShutdownAsync() {
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
}

[CollectionDefinition("KeycloakAuthTests")]
public class KeycloakAuthTestCollection : ICollectionFixture<KeycloakAuthFixture>;
