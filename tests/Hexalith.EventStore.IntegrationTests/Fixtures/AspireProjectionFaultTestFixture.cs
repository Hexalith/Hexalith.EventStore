using global::Aspire.Hosting;
using global::Aspire.Hosting.Testing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.IntegrationTests.Fixtures;

/// <summary>
/// Dedicated fixture that starts Aspire topology with malformed projection fault injection enabled
/// in the sample domain service.
/// </summary>
public sealed class AspireProjectionFaultTestFixture : IAsyncLifetime {
    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;
    private string? _previousEnableKeycloak;
    private string? _previousAspNetCoreEnvironment;
    private string? _previousDotNetEnvironment;
    private string? _previousProjectionFaultFlag;
    private HttpClient? _eventStoreClient;

    public HttpClient EventStoreClient => _eventStoreClient ?? throw new InvalidOperationException(
        "Test infrastructure not initialized. Ensure InitializeAsync has completed.");

    public DistributedApplication App => _app ?? throw new InvalidOperationException(
        "Test infrastructure not initialized. Ensure InitializeAsync has completed.");

    public async ValueTask InitializeAsync() {
        _previousEnableKeycloak = Environment.GetEnvironmentVariable("EnableKeycloak");
        Environment.SetEnvironmentVariable("EnableKeycloak", "false");

        _previousAspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        _previousDotNetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");

        _previousProjectionFaultFlag = Environment.GetEnvironmentVariable("EventStore__SampleFaults__MalformedProjectResponse");
        Environment.SetEnvironmentVariable("EventStore__SampleFaults__MalformedProjectResponse", "true");

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

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

        _ = await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("eventstore", cts.Token)
            .WaitAsync(TimeSpan.FromMinutes(3), cts.Token)
            .ConfigureAwait(false);

        _eventStoreClient = _app.CreateHttpClient("eventstore");
        _eventStoreClient.Timeout = TimeSpan.FromSeconds(60);

        _ = await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("sample", cts.Token)
            .WaitAsync(TimeSpan.FromMinutes(3), cts.Token)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync() {
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
        Environment.SetEnvironmentVariable("EventStore__SampleFaults__MalformedProjectResponse", _previousProjectionFaultFlag);
    }
}
