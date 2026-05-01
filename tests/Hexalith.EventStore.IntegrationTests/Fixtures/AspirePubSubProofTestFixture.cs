using global::Aspire.Hosting;
using global::Aspire.Hosting.Testing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.IntegrationTests.Fixtures;

public sealed class AspirePubSubProofTestFixture : IAsyncLifetime {
    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;
    private string? _previousEnableKeycloak;
    private string? _previousAspNetCoreEnvironment;
    private string? _previousDotNetEnvironment;
    private string? _previousSubscriberFlag;
    private string? _previousFaultPath;
    private string? _previousFaultPrefix;
    private string? _previousInitialDrainDelay;
    private string? _previousDrainPeriod;
    private string? _previousMaxDrainPeriod;
    private HttpClient? _eventStoreClient;
    private HttpClient? _subscriberClient;

    public HttpClient EventStoreClient => _eventStoreClient ?? throw new InvalidOperationException(
        "Test infrastructure not initialized. Ensure InitializeAsync has completed.");

    public HttpClient SubscriberClient => _subscriberClient ?? throw new InvalidOperationException(
        "Test subscriber not initialized. Ensure InitializeAsync has completed.");

    public string FaultFilePath { get; } = Path.Combine(
        Path.GetTempPath(),
        $"hexalith-r4a5-pubsub-fault-{Guid.NewGuid():N}.flag");

    public async ValueTask InitializeAsync() {
        _previousEnableKeycloak = Environment.GetEnvironmentVariable("EnableKeycloak");
        _previousAspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        _previousDotNetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        _previousSubscriberFlag = Environment.GetEnvironmentVariable("EnablePubSubTestSubscriber");
        _previousFaultPath = Environment.GetEnvironmentVariable("EventStore__Publisher__TestPublishFaultFilePath");
        _previousFaultPrefix = Environment.GetEnvironmentVariable("EventStore__Publisher__TestPublishFaultCorrelationIdPrefix");
        _previousInitialDrainDelay = Environment.GetEnvironmentVariable("EventStore__Drain__InitialDrainDelay");
        _previousDrainPeriod = Environment.GetEnvironmentVariable("EventStore__Drain__DrainPeriod");
        _previousMaxDrainPeriod = Environment.GetEnvironmentVariable("EventStore__Drain__MaxDrainPeriod");

        Environment.SetEnvironmentVariable("EnableKeycloak", "false");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("EnablePubSubTestSubscriber", "true");
        Environment.SetEnvironmentVariable("EventStore__Publisher__TestPublishFaultFilePath", FaultFilePath);
        Environment.SetEnvironmentVariable("EventStore__Publisher__TestPublishFaultCorrelationIdPrefix", null);
        Environment.SetEnvironmentVariable("EventStore__Drain__InitialDrainDelay", "00:00:01");
        Environment.SetEnvironmentVariable("EventStore__Drain__DrainPeriod", "00:00:01");
        Environment.SetEnvironmentVariable("EventStore__Drain__MaxDrainPeriod", "00:00:05");

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
    }

    public async ValueTask DisposeAsync() {
        File.Delete(FaultFilePath);
        _eventStoreClient?.Dispose();
        _subscriberClient?.Dispose();

        if (_app is not null) {
            await _app.DisposeAsync().ConfigureAwait(false);
        }

        if (_builder is not null) {
            await _builder.DisposeAsync().ConfigureAwait(false);
        }

        Environment.SetEnvironmentVariable("EnableKeycloak", _previousEnableKeycloak);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _previousAspNetCoreEnvironment);
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", _previousDotNetEnvironment);
        Environment.SetEnvironmentVariable("EnablePubSubTestSubscriber", _previousSubscriberFlag);
        Environment.SetEnvironmentVariable("EventStore__Publisher__TestPublishFaultFilePath", _previousFaultPath);
        Environment.SetEnvironmentVariable("EventStore__Publisher__TestPublishFaultCorrelationIdPrefix", _previousFaultPrefix);
        Environment.SetEnvironmentVariable("EventStore__Drain__InitialDrainDelay", _previousInitialDrainDelay);
        Environment.SetEnvironmentVariable("EventStore__Drain__DrainPeriod", _previousDrainPeriod);
        Environment.SetEnvironmentVariable("EventStore__Drain__MaxDrainPeriod", _previousMaxDrainPeriod);
    }
}
