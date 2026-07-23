using global::Aspire.Hosting;
using global::Aspire.Hosting.Testing;

using Hexalith.EventStore.IntegrationTests.Helpers;
using Hexalith.EventStore.IntegrationTests.Security;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.IntegrationTests.Fixtures;

/// <summary>
/// Dedicated fixture that starts Aspire topology with malformed projection fault injection enabled
/// in the sample domain service.
/// </summary>
public sealed class AspireProjectionFaultTestFixture : IAsyncLifetime {
    private readonly ProjectionDeliveryWriterProtocolTestLease _writerProtocolLease = new();

    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;
    private string? _previousEnableKeycloak;
    private string? _previousAspNetCoreEnvironment;
    private string? _previousDotNetEnvironment;
    private string? _previousAggregateActorTypeName;
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

        _previousAggregateActorTypeName = Environment.GetEnvironmentVariable("EventStore__Actors__AggregateActorTypeName");
        Environment.SetEnvironmentVariable("EventStore__Actors__AggregateActorTypeName", $"AggregateActorIntegration{Guid.NewGuid():N}");

        _previousProjectionFaultFlag = Environment.GetEnvironmentVariable("EventStore__SampleFaults__MalformedProjectResponse");
        Environment.SetEnvironmentVariable("EventStore__SampleFaults__MalformedProjectResponse", "true");

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

            _eventStoreClient = _app.CreateHttpClient("eventstore");
            _eventStoreClient.Timeout = TimeSpan.FromSeconds(60);
            string sourceCommit = await ProjectionDeliveryWriterProtocolTestLease
                .ReadExactSourceCommitAsync(cts.Token)
                .ConfigureAwait(false);
            string administratorToken = TestJwtTokenGenerator.GenerateToken(
                subject: "fixture-admin",
                role: "GlobalAdministrator");
            await _writerProtocolLease.ActivateAsync(
                _eventStoreClient,
                administratorToken,
                sourceCommit,
                "disposable-aspire-projection-fault-fixture-backup",
                cts.Token).ConfigureAwait(false);

            _ = await _app.ResourceNotifications
                .WaitForResourceHealthyAsync("eventstore", cts.Token)
                .WaitAsync(TimeSpan.FromMinutes(3), cts.Token)
                .ConfigureAwait(false);

            _ = await _app.ResourceNotifications
                .WaitForResourceHealthyAsync("sample", cts.Token)
                .WaitAsync(TimeSpan.FromMinutes(3), cts.Token)
                .ConfigureAwait(false);
        }
        catch (Exception initializationException) {
            Exception? cleanupException = null;
            try {
                await SafeShutdownAsync().ConfigureAwait(false);
            }
            catch (Exception ex) {
                cleanupException = ex;
            }
            finally {
                RestoreEnvironment();
            }

            if (cleanupException is not null) {
                throw new AggregateException(initializationException, cleanupException);
            }

            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        try {
            await SafeShutdownAsync().ConfigureAwait(false);
        }
        finally {
            RestoreEnvironment();
        }
    }

    private async Task SafeShutdownAsync() {
        Exception? markerRestorationException = null;
        try {
            await _writerProtocolLease.RestoreAsync().ConfigureAwait(false);
        }
        catch (Exception ex) {
            markerRestorationException = ex;
        }

        _eventStoreClient?.Dispose();
        _eventStoreClient = null;

        if (_app is not null) {
            try {
                await _app.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception) {
                // Preserve marker-restoration diagnostics and always restore process state.
            }

            _app = null;
        }

        if (_builder is not null) {
            try {
                await _builder.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception) {
                // Preserve marker-restoration diagnostics and always restore process state.
            }

            _builder = null;
        }

        if (markerRestorationException is not null) {
            throw new InvalidOperationException(
                "The disposable projection-fault topology could not restore the store-global writer-protocol marker.",
                markerRestorationException);
        }
    }

    private void RestoreEnvironment() {
        Environment.SetEnvironmentVariable("EnableKeycloak", _previousEnableKeycloak);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _previousAspNetCoreEnvironment);
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", _previousDotNetEnvironment);
        Environment.SetEnvironmentVariable("EventStore__Actors__AggregateActorTypeName", _previousAggregateActorTypeName);
        Environment.SetEnvironmentVariable("EventStore__SampleFaults__MalformedProjectResponse", _previousProjectionFaultFlag);
    }
}
