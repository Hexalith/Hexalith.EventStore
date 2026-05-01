using global::Aspire.Hosting;
using global::Aspire.Hosting.Testing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using StackExchange.Redis;

namespace Hexalith.EventStore.IntegrationTests.Fixtures;

public sealed class AspirePubSubProofTestFixture : IAsyncLifetime {
    private const string AuthHeaderName = "X-Test-Auth";
    private const string RedisEndpoint = "localhost:6379";

    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;
    private HttpClient? _eventStoreClient;
    private HttpClient? _subscriberClient;
    private IConnectionMultiplexer? _redis;

    private readonly Dictionary<string, string?> _envSnapshot = new(StringComparer.Ordinal);

    public HttpClient EventStoreClient => _eventStoreClient ?? throw new InvalidOperationException(
        "Test infrastructure not initialized. Ensure InitializeAsync has completed.");

    public HttpClient SubscriberClient => _subscriberClient ?? throw new InvalidOperationException(
        "Test subscriber not initialized. Ensure InitializeAsync has completed.");

    public IDatabase RedisDatabase => (_redis ?? throw new InvalidOperationException(
        "Redis multiplexer not initialized. Ensure InitializeAsync has completed.")).GetDatabase();

    public string FaultFilePath { get; } = Path.Combine(
        Path.GetTempPath(),
        $"hexalith-r4a5-pubsub-fault-{Guid.NewGuid():N}.flag");

    public string SubscriberAuthSecret { get; } = Guid.NewGuid().ToString("N");

    public async ValueTask InitializeAsync() {
        SnapshotAndSet("EnableKeycloak", "false");
        SnapshotAndSet("ASPNETCORE_ENVIRONMENT", "Development");
        SnapshotAndSet("DOTNET_ENVIRONMENT", "Development");
        SnapshotAndSet("EnablePubSubTestSubscriber", "true");
        SnapshotAndSet("EVENTSTORE_TEST_SUBSCRIBER_AUTH_SECRET", SubscriberAuthSecret);
        SnapshotAndSet("EventStore__Publisher__TestPublishFaultFilePath", FaultFilePath);
        SnapshotAndSet("EventStore__Publisher__TestPublishFaultCorrelationIdPrefix", null);
        SnapshotAndSet("EventStore__Drain__InitialDrainDelay", "00:00:01");
        SnapshotAndSet("EventStore__Drain__DrainPeriod", "00:00:01");
        SnapshotAndSet("EventStore__Drain__MaxDrainPeriod", "00:00:05");

        // Clear any leftover fault file from a previous crashed run before launching the AppHost
        // so EventStore does not see a ghost fault on first publish.
        TryDeleteFaultFile();

        try {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

            _builder = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.Hexalith_EventStore_AppHost>()
                .ConfigureAwait(false);

            _ = _builder.Services.AddLogging(logging => {
                _ = logging.SetMinimumLevel(LogLevel.Debug);
                _ = logging.AddFilter(_builder.Environment.ApplicationName, LogLevel.Debug);
                _ = logging.AddFilter("Aspire.", LogLevel.Warning);
            });

            _ = _builder.Services.ConfigureHttpClientDefaults(clientBuilder => _ = clientBuilder.AddStandardResilienceHandler(options => {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(180);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
            }));

            _app = await _builder.BuildAsync().ConfigureAwait(false);
            await _app.StartAsync(cts.Token).ConfigureAwait(false);

            _ = await _app.ResourceNotifications
                .WaitForResourceHealthyAsync("eventstore", cts.Token)
                .WaitAsync(TimeSpan.FromMinutes(3), cts.Token)
                .ConfigureAwait(false);

            _ = await _app.ResourceNotifications
                .WaitForResourceHealthyAsync("sample", cts.Token)
                .WaitAsync(TimeSpan.FromMinutes(3), cts.Token)
                .ConfigureAwait(false);

            _ = await _app.ResourceNotifications
                .WaitForResourceHealthyAsync("eventstore-test-subscriber", cts.Token)
                .WaitAsync(TimeSpan.FromMinutes(3), cts.Token)
                .ConfigureAwait(false);

            _eventStoreClient = _app.CreateHttpClient("eventstore");
            _eventStoreClient.Timeout = TimeSpan.FromSeconds(60);

            _subscriberClient = _app.CreateHttpClient("eventstore-test-subscriber");
            _subscriberClient.Timeout = TimeSpan.FromSeconds(60);
            _subscriberClient.DefaultRequestHeaders.TryAddWithoutValidation(AuthHeaderName, SubscriberAuthSecret);

            // Connect to the Redis backing the DAPR state store / pub/sub. `dapr init` runs Redis on
            // localhost:6379 (see HexalithEventStoreExtensions); Aspire does not manage this resource.
            _redis = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions {
                EndPoints = { RedisEndpoint },
                ConnectTimeout = 5_000,
                SyncTimeout = 5_000,
                AbortOnConnectFail = false,
                AllowAdmin = false,
            }).ConfigureAwait(false);
        }
        catch {
            // xUnit does not invoke DisposeAsync when InitializeAsync throws, so restore the env-var
            // block here to keep this fixture's mutations from polluting downstream test collections.
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
        TryDeleteFaultFile();

        if (_redis is not null) {
            try {
                await _redis.CloseAsync(allowCommandsToComplete: false).ConfigureAwait(false);
            }
            catch (RedisException) {
                // Already disconnected; nothing to do.
            }

            _redis.Dispose();
            _redis = null;
        }

        _eventStoreClient?.Dispose();
        _eventStoreClient = null;
        _subscriberClient?.Dispose();
        _subscriberClient = null;

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

    private void TryDeleteFaultFile() {
        try {
            if (File.Exists(FaultFilePath)) {
                File.Delete(FaultFilePath);
            }
        }
        catch (IOException) {
            // File locked / transient AV; not critical at fixture level.
        }
        catch (UnauthorizedAccessException) {
            // Same.
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
