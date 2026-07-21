using global::Aspire.Hosting;
using global::Aspire.Hosting.ApplicationModel;
using global::Aspire.Hosting.Testing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace Hexalith.EventStore.IntegrationTests.Fixtures;
/// <summary>
/// Shared fixture that starts the full Aspire topology WITHOUT Keycloak for Tier 3
/// contract tests. Uses symmetric key JWT authentication via <see cref="TestJwtTokenGenerator"/>
/// for fast test execution (Rule #16: synthetic JWTs for integration tests only).
/// Implements <see cref="IAsyncLifetime"/> so xUnit manages lifecycle around the test collection.
/// </summary>
public class AspireContractTestFixture : IAsyncLifetime {
    private const string HandlerQueryTypesStateKey = "admin:query-types:tenants";
    private const string RedisEndpoint = "localhost:6379";

    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;
    private string? _previousEnableKeycloak;
    private string? _previousAspNetCoreEnvironment;
    private string? _previousDotNetEnvironment;
    private string? _previousAggregateActorTypeName;
    private string? _aggregateActorTypeName;
    private HttpClient? _eventStoreClient;
    private HttpClient? _adminServerClient;

    /// <summary>
    /// Gets the HTTP client for the EventStore service.
    /// Available after <see cref="InitializeAsync"/> completes.
    /// </summary>
    public HttpClient EventStoreClient => _eventStoreClient ?? throw new InvalidOperationException(
        "Test infrastructure not initialized. Ensure InitializeAsync has completed.");

    /// <summary>
    /// Gets the HTTP client for the Admin Server service.
    /// Available after <see cref="InitializeAsync"/> completes.
    /// </summary>
    public HttpClient AdminServerClient => _adminServerClient ?? throw new InvalidOperationException(
        "Test infrastructure not initialized. Ensure InitializeAsync has completed.");

    /// <summary>
    /// Gets the running Aspire distributed application instance.
    /// </summary>
    public DistributedApplication App => _app ?? throw new InvalidOperationException(
        "Test infrastructure not initialized. Ensure InitializeAsync has completed.");

    /// <summary>Gets the live EventStore DAPR HTTP endpoint for persisted-state proofs.</summary>
    public Uri EventStoreDaprHttpEndpoint => App.GetEndpoint("eventstore-dapr-cli", "http");

    /// <summary>Gets the live EventStore DAPR gRPC endpoint for DAPR client state operations.</summary>
    public Uri EventStoreDaprGrpcEndpoint => App.GetEndpoint("eventstore-dapr-cli", "grpc");

    /// <summary>Gets the unique aggregate actor type used by this topology.</summary>
    public string AggregateActorTypeName => _aggregateActorTypeName ?? throw new InvalidOperationException(
        "Test infrastructure not initialized. Ensure InitializeAsync has completed.");

    public async ValueTask InitializeAsync() {
        // Disable Keycloak for fast contract tests -- use symmetric key JWT auth instead.
        _previousEnableKeycloak = Environment.GetEnvironmentVariable("EnableKeycloak");
        Environment.SetEnvironmentVariable("EnableKeycloak", "false");

        // Force Development environment so AppHost children (especially EventStore)
        // load appsettings.Development.json expected by Tier 3 contract tests.
        _previousAspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        _previousDotNetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");

        _previousAggregateActorTypeName = Environment.GetEnvironmentVariable("EventStore__Actors__AggregateActorTypeName");
        _aggregateActorTypeName = $"AggregateActorIntegration{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable("EventStore__Actors__AggregateActorTypeName", _aggregateActorTypeName);

        // 3-minute timeout for Aspire topology startup (no Keycloak container to pull/start).
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        _builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Hexalith_EventStore_AppHost>()
            .ConfigureAwait(false);

        // Task 1.2: Configure test logging -- Debug for app, Warning for Aspire infrastructure.
        _ = _builder.Services.AddLogging(logging => {
            _ = logging.SetMinimumLevel(LogLevel.Debug);
            _ = logging.AddFilter(_builder.Environment.ApplicationName, LogLevel.Debug);
            _ = logging.AddFilter("Aspire.", LogLevel.Warning);
        });

        // Task 1.3: Configure resilient HTTP defaults for test clients.
        // Raise per-attempt and total timeouts above the standard defaults — Tier 3 runs all
        // Aspire collections serially, so the shared Docker/Redis/DAPR stack can take longer
        // than the 10s/30s defaults under back-to-back topology startup/teardown pressure.
        _ = _builder.Services.ConfigureHttpClientDefaults(clientBuilder =>
            _ = clientBuilder.AddStandardResilienceHandler(ConfigureTestClientResilience));

        _app = await _builder.BuildAsync().ConfigureAwait(false);
        await _app.StartAsync(cts.Token).ConfigureAwait(false);

        // Task 1.4 / AC #1: Wait for eventstore resource to be healthy via Aspire resource notifications.
        _ = await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("eventstore", cts.Token)
            .WaitAsync(TimeSpan.FromMinutes(3), cts.Token)
            .ConfigureAwait(false);

        // Create HTTP client for EventStore
        _eventStoreClient = _app.CreateHttpClient("eventstore");
        _eventStoreClient.Timeout = TimeSpan.FromSeconds(60);

        // Wait for admin server to be healthy and create HTTP client.
        _ = await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("eventstore-admin", cts.Token)
            .WaitAsync(TimeSpan.FromMinutes(3), cts.Token)
            .ConfigureAwait(false);

        _adminServerClient = _app.CreateHttpClient("eventstore-admin");
        _adminServerClient.Timeout = TimeSpan.FromSeconds(60);

        // Also wait for the sample domain service to be healthy.
        _ = await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("sample", cts.Token)
            .WaitAsync(TimeSpan.FromMinutes(3), cts.Token)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Restarts EventStore after clearing the persisted Tenants handler index. This proves the
    /// next healthy EventStore instance rebuilt the index from live operational metadata.
    /// </summary>
    public async Task RestartEventStoreWithClearedHandlerQueryTypesStateAsync(CancellationToken cancellationToken) {
        ExecuteCommandResult stopResult = await App.ResourceCommands
            .ExecuteCommandAsync("eventstore", KnownResourceCommands.StopCommand, cancellationToken)
            .ConfigureAwait(false);
        if (!stopResult.Success) {
            throw new InvalidOperationException(
                $"Unable to stop EventStore before clearing {HandlerQueryTypesStateKey}. "
                + $"Message: {stopResult.Message}; Error: {stopResult.ErrorMessage}");
        }

        _ = await App.ResourceNotifications
            .WaitForResourceAsync("eventstore", KnownResourceStates.TerminalStates, cancellationToken)
            .WaitAsync(TimeSpan.FromMinutes(1), cancellationToken)
            .ConfigureAwait(false);

        await DeleteHandlerQueryTypesStateAsync(cancellationToken).ConfigureAwait(false);

        ExecuteCommandResult startResult = await App.ResourceCommands
            .ExecuteCommandAsync("eventstore", KnownResourceCommands.StartCommand, cancellationToken)
            .ConfigureAwait(false);
        if (!startResult.Success) {
            throw new InvalidOperationException(
                $"Unable to restart EventStore after clearing {HandlerQueryTypesStateKey}. "
                + $"Message: {startResult.Message}; Error: {startResult.ErrorMessage}");
        }

        _ = await App.ResourceNotifications
            .WaitForResourceHealthyAsync("eventstore", cancellationToken)
            .WaitAsync(TimeSpan.FromMinutes(3), cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() {
        _eventStoreClient?.Dispose();
        _adminServerClient?.Dispose();
        if (_app is not null) {
            await _app.DisposeAsync().ConfigureAwait(false);
        }

        if (_builder is not null) {
            await _builder.DisposeAsync().ConfigureAwait(false);
        }

        Environment.SetEnvironmentVariable("EnableKeycloak", _previousEnableKeycloak);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _previousAspNetCoreEnvironment);
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", _previousDotNetEnvironment);
        Environment.SetEnvironmentVariable("EventStore__Actors__AggregateActorTypeName", _previousAggregateActorTypeName);
    }

    private static async Task DeleteHandlerQueryTypesStateAsync(CancellationToken cancellationToken) {
        using IConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions {
            EndPoints = { RedisEndpoint },
            ConnectTimeout = 5_000,
            SyncTimeout = 5_000,
            AbortOnConnectFail = false,
            AllowAdmin = false,
        }).ConfigureAwait(false);

        _ = await redis.GetDatabase()
            .KeyDeleteAsync(HandlerQueryTypesStateKey)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        await redis.CloseAsync(allowCommandsToComplete: true).ConfigureAwait(false);
    }

    internal static void ConfigureTestClientResilience(HttpStandardResilienceOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(180);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
        options.Retry.DisableForUnsafeHttpMethods();
    }
}
