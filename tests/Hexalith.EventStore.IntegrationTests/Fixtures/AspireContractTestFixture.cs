using System.Runtime.ExceptionServices;
using global::Aspire.Hosting;
using global::Aspire.Hosting.ApplicationModel;
using global::Aspire.Hosting.Testing;
using Hexalith.EventStore.IntegrationTests.Helpers;
using Hexalith.EventStore.IntegrationTests.Security;
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
    private static readonly TimeSpan s_resourceCommandTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan s_gracefulShutdownTimeout = TimeSpan.FromSeconds(30);

    private readonly ProjectionDeliveryWriterProtocolTestLease _writerProtocolLease = new();

    private DistributedApplication? _app;
    private IDistributedApplicationTestingBuilder? _builder;
    private string? _previousEnableKeycloak;
    private string? _previousAspNetCoreEnvironment;
    private string? _previousDotNetEnvironment;
    private string? _previousAggregateActorTypeName;
    private string? _previousRuntimeProofShutdownToken;
    private string? _aggregateActorTypeName;
    private string? _runtimeProofShutdownToken;
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

        _previousRuntimeProofShutdownToken = Environment.GetEnvironmentVariable(
            "EventStore__RuntimeProof__ShutdownToken");
        _runtimeProofShutdownToken = Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable("EventStore__RuntimeProof__ShutdownToken", _runtimeProofShutdownToken);

        try {
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

            // Production stays fail-closed until an operator activates the v2 writer protocol.
            // This disposable topology has no legacy writers, so perform the explicit cutover
            // before asking Aspire to observe a healthy EventStore resource.
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
                "disposable-aspire-contract-fixture-backup",
                cts.Token).ConfigureAwait(false);

            // Task 1.4 / AC #1: Wait for eventstore resource to be healthy via Aspire resource notifications.
            _ = await _app.ResourceNotifications
                .WaitForResourceHealthyAsync("eventstore", cts.Token)
                .WaitAsync(TimeSpan.FromMinutes(3), cts.Token)
                .ConfigureAwait(false);

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

    /// <summary>
    /// Restarts EventStore after clearing the persisted Tenants handler index. This proves the
    /// next healthy EventStore instance rebuilt the index from live operational metadata.
    /// </summary>
    public async Task RestartEventStoreWithClearedHandlerQueryTypesStateAsync(CancellationToken cancellationToken) {
        Exception? gracefulShutdownException = null;
        try {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/_test/runtime-proof/shutdown");
            request.Headers.Add(
                "X-Hexalith-Runtime-Proof-Token",
                _runtimeProofShutdownToken ?? throw new InvalidOperationException(
                    "The runtime-proof shutdown token was not initialized."));
            using HttpResponseMessage response = await EventStoreClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) {
            gracefulShutdownException = ex;
        }

        Exception? terminalStateException = await ObserveEventStoreTerminalStateAsync(
            s_gracefulShutdownTimeout,
            cancellationToken).ConfigureAwait(false);
        if (terminalStateException is null) {
            await MutateStateAndRestartEventStoreAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        ExecuteCommandResult? stopResult = null;
        Exception? commandException = null;
        try {
            stopResult = await App.ResourceCommands
                .ExecuteCommandAsync("eventstore", KnownResourceCommands.StopCommand, cancellationToken)
                .WaitAsync(s_resourceCommandTimeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) {
            commandException = ex;
        }

        terminalStateException = await ObserveEventStoreTerminalStateAsync(
            s_resourceCommandTimeout,
            cancellationToken).ConfigureAwait(false);

        if (terminalStateException is not null) {
            Exception? recoveryException = await TryRestoreHealthyEventStoreAsync().ConfigureAwait(false);
            Exception stopException = CreateStopException(
                gracefulShutdownException,
                stopResult,
                commandException,
                terminalStateException);
            if (recoveryException is not null) {
                throw new AggregateException(
                    "The EventStore stop command failed and the fixture could not restore a healthy resource.",
                    stopException,
                    recoveryException);
            }

            ExceptionDispatchInfo.Capture(stopException).Throw();
        }

        await MutateStateAndRestartEventStoreAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task MutateStateAndRestartEventStoreAsync(CancellationToken cancellationToken) {
        Exception? mutationException = null;
        try {
            await DeleteHandlerQueryTypesStateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) {
            mutationException = ex;
        }

        Exception? restartException = null;
        try {
            Exception? recoveryException = await TryRestoreHealthyEventStoreAsync().ConfigureAwait(false);
            if (recoveryException is not null) {
                ExceptionDispatchInfo.Capture(recoveryException).Throw();
            }
        }
        catch (Exception ex) {
            restartException = ex;
        }

        if (mutationException is not null && restartException is not null) {
            throw new AggregateException(
                "EventStore state mutation failed and the fixture could not restore a healthy EventStore resource.",
                mutationException,
                restartException);
        }

        if (restartException is not null) {
            ExceptionDispatchInfo.Capture(restartException).Throw();
        }

        if (mutationException is not null) {
            ExceptionDispatchInfo.Capture(mutationException).Throw();
        }
    }

    private static Exception CreateStopException(
        Exception? gracefulShutdownException,
        ExecuteCommandResult? stopResult,
        Exception? commandException,
        Exception terminalStateException) {
        string commandDiagnostic = stopResult is null
            ? "The stop command did not return a result."
            : $"Message: {stopResult.Message}; Error: {stopResult.ErrorMessage}";
        var stateException = new InvalidOperationException(
            $"Unable to observe EventStore reaching a terminal state before clearing {HandlerQueryTypesStateKey}. "
            + commandDiagnostic,
            terminalStateException);
        Exception[] exceptions = new Exception?[] { gracefulShutdownException, commandException, stateException }
            .OfType<Exception>()
            .ToArray();
        return exceptions.Length == 1 ? exceptions[0] : new AggregateException(exceptions);
    }

    private async Task<Exception?> ObserveEventStoreTerminalStateAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken) {
        try {
            // DCP can report Success=false when the Dapr CLI exits non-zero after it has already
            // stopped the child application. The observed resource state is authoritative.
            _ = await App.ResourceNotifications
                .WaitForResourceAsync("eventstore", KnownResourceStates.TerminalStates, cancellationToken)
                .WaitAsync(timeout, cancellationToken)
                .ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) {
            return ex;
        }
    }

    private async Task<Exception?> TryRestoreHealthyEventStoreAsync() {
        using var recoveryCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        ExecuteCommandResult? startResult = null;
        try {
            startResult = await App.ResourceCommands
                .ExecuteCommandAsync(
                    "eventstore",
                    KnownResourceCommands.StartCommand,
                    recoveryCancellation.Token)
                .WaitAsync(s_resourceCommandTimeout, recoveryCancellation.Token)
                .ConfigureAwait(false);

            _ = await App.ResourceNotifications
                .WaitForResourceHealthyAsync("eventstore", recoveryCancellation.Token)
                .WaitAsync(TimeSpan.FromMinutes(3), recoveryCancellation.Token)
                .ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) {
            return startResult is null || startResult.Success
                ? ex
                : new InvalidOperationException(
                    "Unable to restore a healthy EventStore resource after the restart probe. "
                    + $"Message: {startResult.Message}; Error: {startResult.ErrorMessage}",
                    ex);
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
        _adminServerClient?.Dispose();
        _adminServerClient = null;
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
                "The disposable contract topology could not restore the store-global writer-protocol marker.",
                markerRestorationException);
        }
    }

    private void RestoreEnvironment() {
        Environment.SetEnvironmentVariable("EnableKeycloak", _previousEnableKeycloak);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _previousAspNetCoreEnvironment);
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", _previousDotNetEnvironment);
        Environment.SetEnvironmentVariable("EventStore__Actors__AggregateActorTypeName", _previousAggregateActorTypeName);
        Environment.SetEnvironmentVariable(
            "EventStore__RuntimeProof__ShutdownToken",
            _previousRuntimeProofShutdownToken);
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
        AspireContractHttpResilience.Configure(options);
    }
}
