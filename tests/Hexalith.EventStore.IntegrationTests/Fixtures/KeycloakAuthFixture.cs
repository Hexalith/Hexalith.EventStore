using System.Security.Cryptography;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Aspire.Hosting.ApplicationModel;

using Hexalith.EventStore.AppHost;
using Hexalith.EventStore.IntegrationTests.Helpers;
using Hexalith.EventStore.IntegrationTests.Security;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.IntegrationTests.Fixtures;

/// <summary>
/// Shared fixture that starts an owned gateway, Counter and Keycloak cohort for
/// Tier 3 E2E security tests (D11, R-001). Uses real OIDC tokens from Keycloak
/// instead of synthetic symmetric JWTs.
/// </summary>
public class KeycloakAuthFixture : IAsyncLifetime {
    private const string SecurityResourceName = "security";

    private readonly Dictionary<string, string?> _envSnapshot = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _userPasswords = new(StringComparer.Ordinal);
    private readonly PrivateAuthenticationInfrastructure _infrastructure = new();
    private ProjectionDeliveryWriterProtocolTestLease? _writerProtocolLease;

    private Task? _startupTask;
    private CancellationTokenSource? _startupCancellation;
    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;
    private HttpClient? _eventStoreClient;
    private LocalAuthenticationTestInvocation? _localAuthenticationTestInvocation;

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

    /// <summary>
    /// Gets the per-run administrator subject used by the tenant-bootstrap contract.
    /// </summary>
    public string AdminUserId { get; private set; } = string.Empty;

    /// <summary>Gets the private backing store for persisted isolation assertions.</summary>
    internal string RedisEndpoint => _infrastructure.RedisEndpoint;

    /// <summary>Gets the actor type owned by this topology.</summary>
    internal string AggregateActorTypeName { get; } = $"AggregateActorIntegration{Guid.NewGuid():N}";

    /// <summary>Creates an independently disposed client with strict run-private TLS trust.</summary>
    public HttpClient CreateEventStoreClient() => _infrastructure.CreateClient(EventStoreClient.BaseAddress!);

    public async ValueTask InitializeAsync() {
        // Enable Keycloak for real OIDC auth testing.
        SnapshotAndSet("EnableKeycloak", "true");
        SnapshotAndSet("KeycloakPersistent", "false");
        AdminUserId = Guid.NewGuid().ToString("D");
        _localAuthenticationTestInvocation = LocalAuthenticationCredentials.RegisterTestInvocation(
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
            AdminUserId,
            "admin-user");
        _localAuthenticationTestInvocation.Activate();
        ConfigureUsers(_localAuthenticationTestInvocation.Credentials);

        SnapshotAndSet("ASPNETCORE_ENVIRONMENT", "Development");
        SnapshotAndSet("DOTNET_ENVIRONMENT", "Development");
        SnapshotAndSet("EventStore__Actors__AggregateActorTypeName", AggregateActorTypeName);

        try {
            // This fixture budget leaves room for all test bodies within the fixed 180-second lane.
            _startupCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            CancellationTokenSource cts = _startupCancellation;

            await _infrastructure.StartAsync(cts.Token).ConfigureAwait(false);
            _builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.Hexalith_EventStore_AppHost>()
                .ConfigureAwait(false);

            _infrastructure.Configure(_builder);
            _ = _builder.Services.AddLogging(logging => {
                _ = logging.SetMinimumLevel(LogLevel.Warning);
                _ = logging.AddFilter(_builder.Environment.ApplicationName, LogLevel.Warning);
                _ = logging.AddFilter(_builder.Environment.ApplicationName + ".Resources.eventstore", LogLevel.Information);
                _ = logging.AddFilter(_builder.Environment.ApplicationName + ".Resources.sample", LogLevel.Information);
                _ = logging.AddFilter("Aspire.", LogLevel.Warning);
            });

            _ = _builder.Services.ConfigureHttpClientDefaults(clientBuilder => _ = clientBuilder.AddStandardResilienceHandler());

            _app = await _builder.BuildAsync().ConfigureAwait(false);
            _startupTask = _app.StartAsync(cts.Token);

            // Wait for Keycloak to be healthy (realm import must complete).
            _ = await _app.ResourceNotifications
                .WaitForResourceHealthyAsync(SecurityResourceName, cts.Token)
                .WaitAsync(TimeSpan.FromMinutes(4), cts.Token)
                .ConfigureAwait(false);

            string gatewayUrl = await _builder.CreateResourceBuilder<ProjectResource>("eventstore")
                .GetEndpoint("https").GetValueAsync(cts.Token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The private gateway HTTPS endpoint was not allocated.");
            _eventStoreClient = _infrastructure.CreateClient(new Uri(gatewayUrl));
            _writerProtocolLease = new ProjectionDeliveryWriterProtocolTestLease(_infrastructure.RedisEndpoint);

            // Resolve the token endpoint and acquire a real administrator token before EventStore
            // can be healthy; the production writer-protocol cutover itself requires that role.
            string keycloakUrl = await _builder.CreateResourceBuilder<ContainerResource>(SecurityResourceName)
                .GetEndpoint("http").GetValueAsync(cts.Token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The private Keycloak endpoint was not allocated.");
            KeycloakTokenEndpoint = keycloakUrl.TrimEnd('/') + "/realms/hexalith/protocol/openid-connect/token";
            string administratorToken = await AcquireAdministratorTokenAsync(cts.Token).ConfigureAwait(false);
            string sourceCommit = await ProjectionDeliveryWriterProtocolTestLease
                .ReadExactSourceCommitAsync(cts.Token)
                .ConfigureAwait(false);
            await _writerProtocolLease.ActivateAsync(
                _eventStoreClient,
                administratorToken,
                sourceCommit,
                "disposable-keycloak-auth-fixture-backup",
                cts.Token).ConfigureAwait(false);

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
            await _startupTask.ConfigureAwait(false);
            // Aspire's resource state alone does not prove that an application's
            // real catalog and sidecar readiness checks have completed.
            string sampleUrl = await _builder.CreateResourceBuilder<ProjectResource>("sample")
                .GetEndpoint("https").GetValueAsync(cts.Token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("The private sample HTTPS endpoint was not allocated.");
            using HttpClient sampleClient = _infrastructure.CreateClient(new Uri(sampleUrl));
            await WaitForApplicationHealthAsync(sampleClient, cts.Token).ConfigureAwait(false);
            await WaitForApplicationHealthAsync(_eventStoreClient, cts.Token).ConfigureAwait(false);
            // Verify the real mutually authenticated sidecar hop as a readiness
            // condition, without submitting or retrying a business command.
            using var sidecarClient = new HttpClient {
                BaseAddress = _app.GetEndpoint("eventstore-dapr-cli", "http"),
                Timeout = TimeSpan.FromSeconds(15),
            };
            await WaitForApplicationHealthAsync(sidecarClient, cts.Token,
                "/v1.0/invoke/sample/method/health").ConfigureAwait(false);
            using var sampleSidecarClient = new HttpClient {
                BaseAddress = _app.GetEndpoint("sample-dapr-cli", "http"),
                Timeout = TimeSpan.FromSeconds(15),
            };
            await WaitForApplicationHealthAsync(sampleSidecarClient, cts.Token,
                "/v1.0/healthz", HttpStatusCode.NoContent).ConfigureAwait(false);
            await WaitForAuthenticatedMetadataAsync(sidecarClient, cts.Token).ConfigureAwait(false);

        }
        catch (Exception initializationException) {
            try {
                await SafeShutdownAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupException) {
                throw new AggregateException(initializationException, cleanupException);
            }
            finally {
                RestoreEnvironmentSnapshot();
            }
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
        var failures = new List<Exception>();
        try { _startupCancellation?.Cancel(); }
        catch (Exception ex) { failures.Add(ex); }
        if (_writerProtocolLease is not null) {
            try { await _writerProtocolLease.RestoreAsync().ConfigureAwait(false); }
            catch (Exception ex) { failures.Add(ex); }
            _writerProtocolLease = null;
        }
        _eventStoreClient?.Dispose();
        _eventStoreClient = null;
        if (_app is not null) {
            try { await _app.DisposeAsync().ConfigureAwait(false); }
            catch (Exception ex) { failures.Add(ex); }
            _app = null;
        }
        if (_startupTask is not null) {
            try { await _startupTask.ConfigureAwait(false); }
            catch (OperationCanceledException) when (_startupCancellation?.IsCancellationRequested == true) { }
            catch (Exception ex) { failures.Add(ex); }
            _startupTask = null;
        }
        _startupCancellation?.Dispose();
        _startupCancellation = null;
        if (_builder is not null) {
            try { await _builder.DisposeAsync().ConfigureAwait(false); }
            catch (Exception ex) { failures.Add(ex); }
            _builder = null;
        }
        try { await _infrastructure.DisposeAsync().ConfigureAwait(false); }
        catch (Exception ex) { failures.Add(ex); }
        if (failures.Count != 0)
            throw new AggregateException("Private Keycloak topology cleanup failed.", failures);
    }

    private async Task<string> AcquireAdministratorTokenAsync(CancellationToken cancellationToken) {
        Exception? lastException = null;
        while (!cancellationToken.IsCancellationRequested) {
            try {
                return await AcquireTokenAsync("admin-user", cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) {
                lastException = ex;
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
        }

        throw new TimeoutException(
            "Keycloak administrator token acquisition did not complete within the topology startup budget.",
            lastException);
    }

    private static async Task WaitForApplicationHealthAsync(HttpClient client, CancellationToken cancellationToken,
        string path = "/health", HttpStatusCode expectedStatus = HttpStatusCode.OK) {
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                using HttpResponseMessage response = await client.GetAsync(path, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == expectedStatus) return;
            }
            catch (HttpRequestException) { }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WaitForAuthenticatedMetadataAsync(HttpClient sidecarClient, CancellationToken cancellationToken) {
        LocalAuthenticationCredentials credentials = _localAuthenticationTestInvocation?.Credentials
            ?? throw new InvalidOperationException("The owned workload credential is unavailable.");
        using var identityClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var form = new FormUrlEncodedContent(new Dictionary<string, string> {
            ["grant_type"] = "client_credentials", ["client_id"] = "eventstore",
            ["client_secret"] = credentials.WorkloadClientSecret,
            ["scope"] = new WorkloadAssertionIssuerOptions().BuildScope("sample", EventStoreWorkloadOperations.DomainServiceMetadata),
        });
        using HttpResponseMessage issued = await identityClient.PostAsync(KeycloakTokenEndpoint, form, cancellationToken).ConfigureAwait(false);
        _ = issued.EnsureSuccessStatusCode();
        using JsonDocument tokenResponse = JsonDocument.Parse(await issued.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        string assertion = tokenResponse.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("The owned issuer did not return a workload assertion.");
        while (true) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                var readback = _infrastructure.ReadCommittedGateway();
                using var request = new HttpRequestMessage(HttpMethod.Post, "/v1.0/invoke/sample/method/admin/operational-index-metadata") {
                    Content = JsonContent.Create(new { domains = new[] { "counter" } }),
                };
                request.Headers.Add(EventStoreWorkloadAuthenticationDefaults.AssertionHeaderName, assertion);
                // These are credential-free generation hints, never authority. The receiver
                // authenticates the workload and independently reacquires live catalog authority.
                request.Headers.Add(RouteCatalogInvocationHandler.RootHeader, readback.RootDigest);
                request.Headers.Add(RouteCatalogInvocationHandler.EpochHeader, readback.Epoch.ToString(System.Globalization.CultureInfo.InvariantCulture));
                request.Headers.Add(RouteCatalogInvocationHandler.BindingHeader, readback.ActivationBindingDigest);
                using HttpResponseMessage response = await sidecarClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK) return;
                if (response.StatusCode is not (HttpStatusCode.InternalServerError or HttpStatusCode.ServiceUnavailable))
                    throw new InvalidOperationException($"Authenticated metadata readiness was refused: Status={(int)response.StatusCode}.");
                Console.WriteLine($"Authenticated metadata readiness is pending: Status={(int)response.StatusCode}.");
            }
            catch (HttpRequestException) { }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
        }
    }

    private void SnapshotAndSet(string name, string? newValue) {
        if (!_envSnapshot.ContainsKey(name)) {
            _envSnapshot[name] = Environment.GetEnvironmentVariable(name);
        }

        Environment.SetEnvironmentVariable(name, newValue);
    }

    /// <summary>
    /// Acquires a token using the runtime-generated password for a named fixture identity.
    /// </summary>
    /// <param name="username">The non-secret fixture username.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The acquired access token.</returns>
    public Task<string> AcquireTokenAsync(string username, CancellationToken cancellationToken = default) {
        if (!_userPasswords.TryGetValue(username, out string? password)) {
            throw new InvalidOperationException("The requested Keycloak fixture identity is not configured.");
        }

        return KeycloakTokenHelper.AcquireTokenAsync(
            KeycloakTokenEndpoint,
            "hexalith-eventstore",
            username,
            password,
            cancellationToken);
    }

    /// <summary>Acquires a real, otherwise equivalent token without the interactive surface grant.</summary>
    /// <param name="username">The fixture identity.</param>
    /// <returns>The IdP-signed token lacking a surface claim.</returns>
    public Task<string> AcquireUnmarkedTokenAsync(string username)
        => KeycloakTokenHelper.AcquireTokenAsync(KeycloakTokenEndpoint, "hexalith-eventstore-unmarked", username,
            _userPasswords[username], TestContext.Current.CancellationToken);

    /// <summary>Acquires a real token carrying the forbidden historical storage tenant as a public grant.</summary>
    /// <param name="username">The fixture identity.</param>
    /// <returns>The signed invalid-grant control token.</returns>
    public Task<string> AcquireReservedTenantTokenAsync(string username)
        => KeycloakTokenHelper.AcquireTokenAsync(KeycloakTokenEndpoint, "hexalith-eventstore-reserved-tenant", username,
            _userPasswords[username], TestContext.Current.CancellationToken);

    private void ConfigureUsers(LocalAuthenticationCredentials credentials) {
        _userPasswords[credentials.AdminUsername] = credentials.AdminPassword;
        _userPasswords["tenant-a-user"] = credentials.TenantAPassword;
        _userPasswords["tenant-b-user"] = credentials.TenantBPassword;
        _userPasswords["readonly-user"] = credentials.ReadOnlyPassword;
        _userPasswords["no-tenant-user"] = credentials.NoTenantPassword;
    }

    private void RestoreEnvironmentSnapshot() {
        foreach (KeyValuePair<string, string?> entry in _envSnapshot) {
            Environment.SetEnvironmentVariable(entry.Key, entry.Value);
        }

        _envSnapshot.Clear();
        _userPasswords.Clear();
        _localAuthenticationTestInvocation?.Dispose();
        _localAuthenticationTestInvocation = null;
        AdminUserId = string.Empty;
    }
}
