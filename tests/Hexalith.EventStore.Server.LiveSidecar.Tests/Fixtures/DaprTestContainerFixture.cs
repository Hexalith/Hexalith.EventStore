
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using RedisConnectionMultiplexer = StackExchange.Redis.IConnectionMultiplexer;
using RedisDatabase = StackExchange.Redis.IDatabase;
using RedisValue = StackExchange.Redis.RedisValue;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
/// <summary>
/// Integration test fixture that starts a local <c>daprd</c> process,
/// reusing the existing Dapr infrastructure (Redis, placement, scheduler) from <c>dapr init</c>.
/// Provides a running Dapr environment with actor support for Tier 2 integration tests.
/// Implements <see cref="IAsyncLifetime"/> for xUnit lifecycle management.
/// </summary>
public sealed class DaprTestContainerFixture : IAsyncLifetime
{
    private const string AppId = "eventstore";
    private static readonly int PlacementPort = OperatingSystem.IsWindows() ? 6050 : 50005;
    private static readonly int SchedulerPort = OperatingSystem.IsWindows() ? 6060 : 50006;
    private const int RedisPort = 6379;
    private const int HealthTimeoutSeconds = 60;
    private const int WarmUpTimeoutSeconds = 45;
    private static readonly Lazy<Task<RedisConnectionMultiplexer>> RedisConnection =
        new(async () => await StackExchange.Redis.ConnectionMultiplexer
            .ConnectAsync($"localhost:{RedisPort},abortConnect=false")
            .ConfigureAwait(false));

    private Process? _daprProcess;
    private WebApplication? _testHost;
    private int _appPort;
    private int _daprHttpPort;
    private int _daprGrpcPort;
    private int _daprInternalGrpcPort;
    private int _daprMetricsPort;
    private int _daprProfilePort;
    private string? _componentsDir;

    private string? _previousDaprHttpPort;
    private string? _previousDaprGrpcPort;
    private readonly StringBuilder _daprStdout = new();
    private readonly StringBuilder _daprStderr = new();
    private volatile bool _hostStopping;
    private string? _hostStopStackTrace;

    /// <summary>Gets the Dapr HTTP endpoint for test clients.</summary>
    public string DaprHttpEndpoint => $"http://localhost:{_daprHttpPort}";

    /// <summary>Gets the application HTTP endpoint hosting the Dapr actors.</summary>
    public string AppHttpEndpoint => $"http://localhost:{_appPort}";

    /// <summary>Gets the Dapr gRPC endpoint for test clients.</summary>
    public string DaprGrpcEndpoint => $"http://localhost:{_daprGrpcPort}";

    /// <summary>Gets the isolated aggregate actor type name registered by this fixture run.</summary>
    public string AggregateActorTypeName { get; } = $"AggregateActorTests{Guid.NewGuid():N}";

    /// <summary>Gets the fake domain service invoker for configuring test responses.</summary>
    public FakeDomainServiceInvoker DomainServiceInvoker { get; } = new();

    /// <summary>Gets the fake event publisher for test assertions.</summary>
    public FakeEventPublisher EventPublisher { get; } = new();

    /// <summary>Gets the fake dead-letter publisher for test assertions.</summary>
    public FakeDeadLetterPublisher DeadLetterPublisher { get; } = new();

    /// <summary>Gets the in-memory command status store for test assertions.</summary>
    public InMemoryCommandStatusStore CommandStatusStore { get; } = new();

    /// <summary>Gets the running test host services.</summary>
    public IServiceProvider Services => _testHost?.Services
        ?? throw new InvalidOperationException("The live-sidecar test host has not started.");

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        KillOrphanedDaprdProcesses();

        int[] ports = GetAvailablePorts(6);
        _appPort = ports[0];
        _daprHttpPort = ports[1];
        _daprGrpcPort = ports[2];
        _daprInternalGrpcPort = ports[3];
        _daprMetricsPort = ports[4];
        _daprProfilePort = ports[5];

        // The Dapr .NET Actors runtime uses the DAPR_* env vars to find the sidecar.
        // Since the fixture starts daprd on random ports, we must set these for the in-process app.
        _previousDaprHttpPort = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT");
        _previousDaprGrpcPort = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT");
        Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", _daprHttpPort.ToString());
        Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", _daprGrpcPort.ToString());

        try
        {
            await VerifyPrerequisitesAsync().ConfigureAwait(false);

            _componentsDir = CreateComponentFiles();

            await StartTestHostAsync().ConfigureAwait(false);

            await VerifyAppListeningAsync().ConfigureAwait(false);

            StartDaprSidecar();

            await WaitForDaprHealthAsync().ConfigureAwait(false);

            // Let the sidecar complete its initial app discovery (GET /dapr/config)
            // and actor registration with the placement service before running tests.
            await Task.Delay(2000).ConfigureAwait(false);

            await VerifyAppListeningAsync().ConfigureAwait(false);

            await ActivateProjectionDeliveryWriterProtocolAsync().ConfigureAwait(false);

            // Warm the actor runtime (placement dissemination + activation + Redis round-trip)
            // before any live-sidecar test asserts, so cold-start latency on CI runners does not
            // make the first real actor call fail open. See
            // sprint-change-proposal-2026-06-22-ci-release-retier.md.
            await WarmUpActorRuntimeAsync().ConfigureAwait(false);
        }
        catch
        {
            await DisposeTestResourcesAsync().ConfigureAwait(false);
            RestoreDaprPortEnvironment();
            throw;
        }
    }

    private async Task ActivateProjectionDeliveryWriterProtocolAsync()
    {
        IProjectionDeliveryCutover cutover = Services.GetRequiredService<IProjectionDeliveryCutover>();
        ProjectionDeliveryCutoverStatus status = await cutover.ActivateAsync(
            new ProjectionDeliveryCutoverRequest(
                "2794ecba4c435de5e53603aa6080b8d32d669858",
                "disposable-live-fixture-backup",
                WritersQuiesced: true,
                RetryWorkersQuiesced: true,
                DowngradeProhibitedAcknowledged: true)).ConfigureAwait(false);
        if (status != ProjectionDeliveryCutoverStatus.Activated)
        {
            throw new InvalidOperationException("Projection delivery writer protocol v2 could not be activated for the live fixture.");
        }
    }

    /// <summary>
    /// Performs a throwaway ETag actor round-trip with bounded retry so placement dissemination,
    /// actor activation, and the Redis state round-trip are all hot before any live-sidecar test
    /// asserts. This absorbs the cold-start latency that otherwise makes the first real actor call
    /// fail open (return null) on slower CI runners.
    /// </summary>
    private async Task WarmUpActorRuntimeAsync()
    {
        var factory = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = DaprHttpEndpoint,
            RequestTimeout = TimeSpan.FromSeconds(15),
        });

        string actorId = $"warmup:{Guid.NewGuid():N}";
        IETagActor proxy = factory.CreateActorProxy<IETagActor>(
            new ActorId(actorId), ETagActor.ETagActorTypeName);

        var stopwatch = Stopwatch.StartNew();
        Exception? lastError = null;
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(WarmUpTimeoutSeconds))
        {
            try
            {
                string seeded = await proxy.RegenerateAsync().ConfigureAwait(false);
                string? readBack = await proxy.GetCurrentETagAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(readBack) && string.Equals(readBack, seeded, StringComparison.Ordinal))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"Dapr actor runtime did not warm up within {WarmUpTimeoutSeconds}s.\n" +
            $"Last error: {lastError?.Message ?? "(round-trip returned an inconsistent ETag)"}\n" +
            $"--- daprd stdout (last 2000 chars) ---\n{TailString(GetCapturedStdout(), 2000)}\n" +
            $"--- daprd stderr (last 2000 chars) ---\n{TailString(GetCapturedStderr(), 2000)}");
    }

    /// <summary>
    /// Throws if the test host has begun shutting down. Call from tests to get a clear
    /// diagnostic instead of a generic "connection refused" from the sidecar.
    /// </summary>
    public void ThrowIfHostStopped()
    {
        if (_hostStopping)
        {
            throw new InvalidOperationException(
                $"Test host shut down unexpectedly.\n" +
                $"Stop stack trace:\n{_hostStopStackTrace}");
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await DisposeTestResourcesAsync().ConfigureAwait(false);
        RestoreDaprPortEnvironment();
    }

    /// <summary>
    /// Clears shared fake state before a test class configures its scenario.
    /// </summary>
    public void ResetTestState()
    {
        DomainServiceInvoker.ClearAll();
        EventPublisher.Reset();
        DeadLetterPublisher.Reset();
        CommandStatusStore.Clear();
        Services.GetRequiredService<LiveNamedProjectionFaultControl>().Reset();
    }

    /// <summary>
    /// Configures the domain service invoker with Counter domain responses for integration tests.
    /// </summary>
    public void SetupCounterDomain()
    {
        DomainServiceInvoker.SetupResponse(
            "IncrementCounter",
            DomainResult.Success(new IEventPayload[]
            {
                new Hexalith.EventStore.Sample.Counter.Events.CounterIncremented(),
            }));

        DomainServiceInvoker.SetupResponse(
            "DecrementCounter",
            DomainResult.Success(new IEventPayload[]
            {
                new Hexalith.EventStore.Sample.Counter.Events.CounterDecremented(),
            }));

        DomainServiceInvoker.SetupResponse(
            "ResetCounter",
            DomainResult.Success(new IEventPayload[]
            {
                new Hexalith.EventStore.Sample.Counter.Events.CounterReset(),
            }));
    }

    /// <summary>
    /// Reads aggregate actor state directly from Redis using the fixture's isolated actor type name.
    /// </summary>
    /// <param name="key">The aggregate state key, such as a metadata, snapshot, or event-stream key.</param>
    /// <returns>The raw JSON value stored by Dapr for the actor state entry.</returns>
    public async Task<string> GetAggregateActorStateJsonAsync(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        string redisKey = GetAggregateActorRedisKey(key);
        RedisConnectionMultiplexer multiplexer = await RedisConnection.Value.ConfigureAwait(false);
        RedisDatabase database = multiplexer.GetDatabase();

        for (int attempt = 0; attempt < 10; attempt++)
        {
            RedisValue json = await database.HashGetAsync(redisKey, "data").ConfigureAwait(false);
            if (!json.IsNullOrEmpty)
            {
                return json.ToString();
            }

            await Task.Delay(50).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"Redis actor state for key '{key}' did not become available after retries. Redis key: '{redisKey}'.");
    }

    private async ValueTask DisposeTestResourcesAsync()
    {
        if (_testHost is not null)
        {
            await _testHost.StopAsync().ConfigureAwait(false);
            await _testHost.DisposeAsync().ConfigureAwait(false);
            _testHost = null;
        }

        if (_daprProcess is not null && !_daprProcess.HasExited)
        {
            _daprProcess.Kill(entireProcessTree: true);
            await _daprProcess.WaitForExitAsync().ConfigureAwait(false);
        }

        _daprProcess?.Dispose();
        _daprProcess = null;

        if (_componentsDir is not null && Directory.Exists(_componentsDir))
        {
            try
            {
                Directory.Delete(_componentsDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup of temp files
            }
        }

        _componentsDir = null;
    }

    private void RestoreDaprPortEnvironment()
    {
        Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", _previousDaprHttpPort);
        Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", _previousDaprGrpcPort);
    }

    private static async Task VerifyPrerequisitesAsync()
    {
        var failures = new List<string>();

        if (!await IsPortReachableAsync("localhost", RedisPort, "Redis").ConfigureAwait(false))
        {
            failures.Add($"Redis is not reachable on localhost:{RedisPort}");
        }

        if (!await IsPortReachableAsync("localhost", PlacementPort, "Placement").ConfigureAwait(false))
        {
            failures.Add($"Dapr placement service is not reachable on localhost:{PlacementPort}");
        }

        if (!await IsPortReachableAsync("localhost", SchedulerPort, "Scheduler").ConfigureAwait(false))
        {
            failures.Add($"Dapr scheduler service is not reachable on localhost:{SchedulerPort}");
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"Dapr infrastructure pre-flight check failed. Have you run 'dapr init'?\n" +
                string.Join("\n", failures.Select(f => $"  - {f}")));
        }
    }

    private static async Task<bool> IsPortReachableAsync(string host, int port, string serviceName)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void StartDaprSidecar()
    {
        string daprdPath = ResolveDaprdPath();

        _daprProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = daprdPath,
                Arguments = string.Join(' ',
                    "--app-id", AppId,
                    "--app-port", _appPort.ToString(),
                    "--app-protocol", "http",
                    "--app-channel-address", "127.0.0.1",
                    "--dapr-http-port", _daprHttpPort.ToString(),
                    "--dapr-grpc-port", _daprGrpcPort.ToString(),
                    "--dapr-internal-grpc-port", _daprInternalGrpcPort.ToString(),
                    "--metrics-port", _daprMetricsPort.ToString(),
                    "--profile-port", _daprProfilePort.ToString(),
                    "--resources-path", $"\"{_componentsDir}\"",
                    "--log-level", "info",
                    "--placement-host-address", $"localhost:{PlacementPort}",
                    "--scheduler-host-address", $"localhost:{SchedulerPort}"),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        _daprProcess.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (_daprStdout)
                {
                    _ = _daprStdout.AppendLine(e.Data);
                }
            }
        };

        _daprProcess.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (_daprStderr)
                {
                    _ = _daprStderr.AppendLine(e.Data);
                }
            }
        };

        _ = _daprProcess.Start();
        _daprProcess.BeginOutputReadLine();
        _daprProcess.BeginErrorReadLine();

        if (_daprProcess.HasExited)
        {
            throw new InvalidOperationException(
                $"daprd exited immediately with code {_daprProcess.ExitCode}.\nstderr: {GetCapturedStderr()}");
        }
    }

    /// <summary>
    /// Kills orphaned daprd processes from previous test runs that used the same app ID.
    /// If the test runner exits without calling DisposeAsync, stale sidecars remain registered
    /// with the placement service. Actor calls get routed to these stale instances, which try
    /// to connect to old app ports that are no longer listening.
    /// </summary>
    /// <remarks>
    /// <para><b>Safety:</b> Only kills daprd processes whose command line contains the test
    /// app ID ("<see cref="AppId"/>"). Set environment variable <c>DAPR_TEST_PRESERVE_SIDECARS=1</c>
    /// to skip cleanup entirely (useful when running a local Dapr sidecar for other projects).</para>
    /// </remarks>
    private static void KillOrphanedDaprdProcesses()
    {
        if (Environment.GetEnvironmentVariable("DAPR_TEST_PRESERVE_SIDECARS") == "1")
        {
            return;
        }

        try
        {
            foreach (Process process in Process.GetProcessesByName("daprd"))
            {
                try
                {
                    // Only kill sidecars started with our test app-id to avoid
                    // disrupting other local Dapr workloads.
                    string? cmdLine = GetProcessCommandLine(process);
                    if (cmdLine is null || !cmdLine.Contains(AppId, StringComparison.OrdinalIgnoreCase))
                    {
                        process.Dispose();
                        continue;
                    }

                    process.Kill(entireProcessTree: true);
                    _ = process.WaitForExit(5000);
                }
                catch (Exception)
                {
                    // Best-effort: process may have already exited
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception)
        {
            // Best-effort cleanup
        }
    }

    /// <summary>
    /// Attempts to read the command line of a process. Returns null if inaccessible.
    /// </summary>
    private static string? GetProcessCommandLine(Process process)
    {
        try
        {
            // On Windows, MainModule.FileName + StartInfo are not available for other processes.
            // Fall back to reading /proc on Linux or wmic on Windows.
            if (OperatingSystem.IsWindows())
            {
                using var searcher = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "wmic",
                        Arguments = $"process where processid={process.Id} get CommandLine /format:list",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                    },
                };
                _ = searcher.Start();
                string output = searcher.StandardOutput.ReadToEnd();
                _ = searcher.WaitForExit(3000);
                return output;
            }

            // On Linux/macOS, read /proc/{pid}/cmdline
            string cmdlinePath = $"/proc/{process.Id}/cmdline";
            if (File.Exists(cmdlinePath))
            {
                return File.ReadAllText(cmdlinePath).Replace('\0', ' ');
            }
        }
        catch
        {
            // Best-effort: access denied or process exited
        }

        return null;
    }

    private static string ResolveDaprdPath()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string candidate = Path.Combine(home, ".dapr", "bin", "daprd" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));

        if (File.Exists(candidate))
        {
            return candidate;
        }

        return OperatingSystem.IsWindows() ? "daprd.exe" : "daprd";
    }

    private async Task StartTestHostAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions());

        builder.Configuration["DAPR_HTTP_PORT"] = _daprHttpPort.ToString();
        builder.Configuration["DAPR_GRPC_PORT"] = _daprGrpcPort.ToString();
        builder.Configuration["Dapr:HttpPort"] = _daprHttpPort.ToString();
        builder.Configuration["Dapr:GrpcPort"] = _daprGrpcPort.ToString();
        builder.Configuration["EventStore:Actors:AggregateActorTypeName"] = AggregateActorTypeName;
        builder.Configuration["EventStore:ProjectionDispatch:RetryWorkerInterval"] = "00:10:00";
        builder.Configuration["EventStore:DomainService:AppId"] = "eventstore";
        builder.Configuration["EventStore:DomainService:ServiceVersion"] = "v1";

        _ = builder.WebHost.ConfigureKestrel(serverOptions => serverOptions.ListenLocalhost(_appPort, listenOptions => listenOptions.Protocols = HttpProtocols.Http1));

        _ = builder.Services.AddSingleton(
            new DaprClientBuilder()
                .UseHttpEndpoint(DaprHttpEndpoint)
                .UseGrpcEndpoint(DaprGrpcEndpoint)
                .Build());
        _ = builder.Services.AddEventStoreServer(builder.Configuration);
        _ = builder.Services.AddSingleton<DomainProjectionCatalogRegistry>();
        _ = builder.Services.AddOptions<DomainProjectionIdentityOptions>()
            .BindConfiguration("EventStore:DomainService");
        _ = builder.Services.AddScoped<IAsyncDomainProjectionHandler, LiveCounterDetailProjectionHandler>();
        _ = builder.Services.AddScoped<IAsyncDomainProjectionHandler, LiveCounterIndexProjectionHandler>();
        _ = builder.Services.AddSingleton<LiveNamedProjectionFaultControl>();

        _ = builder.Services.AddSingleton<IDomainServiceInvoker>(DomainServiceInvoker);
        _ = builder.Services.AddSingleton<IEventPublisher>(EventPublisher);
        _ = builder.Services.AddSingleton<IDeadLetterPublisher>(DeadLetterPublisher);
        _ = builder.Services.AddSingleton<ICommandStatusStore>(CommandStatusStore);

        _ = builder.Services.Configure<SnapshotOptions>(o => o.DomainIntervals["counter"] = 15);

        _testHost = builder.Build();

        _ = _testHost.Lifetime.ApplicationStopping.Register(() =>
        {
            _hostStopping = true;
            _hostStopStackTrace = Environment.StackTrace;
        });

        _ = _testHost.MapActorsHandlers();
        _ = _testHost.MapPost(
            "/project",
            (ProjectionRequest request) => Microsoft.AspNetCore.Http.Results.Ok(
                new ProjectionResponse(
                    "counter-legacy",
                    JsonSerializer.SerializeToElement(new { eventCount = request.Events.Length }))));
        _ = _testHost.MapPost(
            "/project/v2",
            async (ProjectionDispatchRequest request,
                   IServiceProvider serviceProvider,
                   IOptions<Hexalith.EventStore.Client.Projections.ProjectionDispatchOptions> options,
                   IOptions<DomainProjectionIdentityOptions> identityOptions,
                   CancellationToken cancellationToken) => Microsoft.AspNetCore.Http.Results.Ok(
                       await DomainProjectionDispatcher
                           .DispatchAsync(serviceProvider, request, options.Value, identityOptions.Value, cancellationToken)
                           .ConfigureAwait(false)));
        _ = _testHost.MapPost(
            "/project/rebuild/v1",
            async (ProjectionDispatchRequest request,
                   IServiceProvider serviceProvider,
                   IOptions<Hexalith.EventStore.Client.Projections.ProjectionDispatchOptions> options,
                   IOptions<DomainProjectionIdentityOptions> identityOptions,
                   CancellationToken cancellationToken) => Microsoft.AspNetCore.Http.Results.Ok(
                       await DomainProjectionDispatcher
                           .RebuildAsync(serviceProvider, request, options.Value, identityOptions.Value, cancellationToken)
                           .ConfigureAwait(false)));
        _ = _testHost.MapPost(
            "/project/rebuild/stage/v1",
            async (ProjectionDispatchRequest request,
                   IServiceProvider serviceProvider,
                   IOptions<Hexalith.EventStore.Client.Projections.ProjectionDispatchOptions> options,
                   IOptions<DomainProjectionIdentityOptions> identityOptions,
                   CancellationToken cancellationToken) => Microsoft.AspNetCore.Http.Results.Ok(
                       await DomainProjectionDispatcher
                           .StageRebuildAsync(serviceProvider, request, options.Value, identityOptions.Value, cancellationToken)
                           .ConfigureAwait(false)));
        _ = _testHost.MapPost(
            "/project/rebuild/commit/v1",
            async (ProjectionDispatchRequest request,
                   IServiceProvider serviceProvider,
                   IOptions<Hexalith.EventStore.Client.Projections.ProjectionDispatchOptions> options,
                   IOptions<DomainProjectionIdentityOptions> identityOptions,
                   CancellationToken cancellationToken) => Microsoft.AspNetCore.Http.Results.Ok(
                       await DomainProjectionDispatcher
                           .CommitRebuildAsync(serviceProvider, request, options.Value, identityOptions.Value, cancellationToken)
                           .ConfigureAwait(false)));
        _ = _testHost.MapPost(
            "/project/rebuild/abort/v1",
            async (ProjectionDispatchRequest request,
                   IServiceProvider serviceProvider,
                   IOptions<Hexalith.EventStore.Client.Projections.ProjectionDispatchOptions> options,
                   IOptions<DomainProjectionIdentityOptions> identityOptions,
                   CancellationToken cancellationToken) => Microsoft.AspNetCore.Http.Results.Ok(
                       await DomainProjectionDispatcher
                           .AbortRebuildAsync(serviceProvider, request, options.Value, identityOptions.Value, cancellationToken)
                           .ConfigureAwait(false)));
        _ = _testHost.MapPost(
            "/project/rebuild/verify/v1",
            async (ProjectionDispatchRequest request,
                   IServiceProvider serviceProvider,
                   IOptions<Hexalith.EventStore.Client.Projections.ProjectionDispatchOptions> options,
                   IOptions<DomainProjectionIdentityOptions> identityOptions,
                   CancellationToken cancellationToken) => Microsoft.AspNetCore.Http.Results.Ok(
                       await DomainProjectionDispatcher
                           .VerifyRebuildAsync(serviceProvider, request, options.Value, identityOptions.Value, cancellationToken)
                           .ConfigureAwait(false)));
        _ = _testHost.MapPost(
            "/admin/operational-index-metadata",
            (AdminOperationalIndexMetadata.Request request,
             IServiceProvider serviceProvider,
             IOptions<Hexalith.EventStore.Client.Projections.ProjectionDispatchOptions> options,
             IOptions<DomainProjectionIdentityOptions> identityOptions) => {
                var discovery = new Hexalith.EventStore.Client.Discovery.DiscoveryResult(
                    [new Hexalith.EventStore.Client.Discovery.DiscoveredDomain(
                        typeof(Hexalith.EventStore.Sample.Counter.CounterAggregate),
                        "counter",
                        typeof(Hexalith.EventStore.Sample.Counter.State.CounterState),
                        Hexalith.EventStore.Client.Discovery.DomainKind.Aggregate)],
                    []);
                return Microsoft.AspNetCore.Http.Results.Ok(AdminOperationalIndexMetadata.Create(
                    discovery,
                    request.Domains,
                    queryHandlers: null,
                    serviceProvider.GetServices<IAsyncDomainProjectionHandler>(),
                    identityOptions.Value.AppId,
                    identityOptions.Value.ServiceVersion,
                    options.Value));
            });
        _ = _testHost.MapGet("/healthz", () => Microsoft.AspNetCore.Http.Results.Ok("healthy"));

        await _testHost.StartAsync().ConfigureAwait(false);

        IServer server = _testHost.Services.GetRequiredService<IServer>();
        ICollection<string>? addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        if (addresses is null || addresses.Count == 0)
        {
            throw new InvalidOperationException(
                $"Kestrel did not bind to any addresses. Expected port {_appPort}.");
        }
    }

    private string GetAggregateActorRedisKey(string key)
    {
        string[] segments = key.Split(':', 4);
        if (segments.Length < 3)
        {
            throw new ArgumentException(
                "Aggregate actor state keys must begin with tenant, domain, and aggregate id segments.",
                nameof(key));
        }

        string actorId = $"{segments[0]}:{segments[1]}:{segments[2]}";
        return $"{AppId}||{AggregateActorTypeName}||{actorId}||{key}";
    }

    private static string CreateComponentFiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"dapr-components-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(tempDir);

        string stateStoreYaml = $$"""
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: statestore
            spec:
              type: state.redis
              version: v1
              metadata:
                - name: redisHost
                  value: "localhost:{{RedisPort}}"
                - name: redisPassword
                  value: ""
                - name: actorStateStore
                  value: "true"
            scopes:
              - eventstore
            """;

        string pubSubYaml = $$"""
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: pubsub
            spec:
              type: pubsub.redis
              version: v1
              metadata:
                - name: redisHost
                  value: "localhost:{{RedisPort}}"
                - name: redisPassword
                  value: ""
                - name: enableDeadLetter
                  value: "true"
            scopes:
              - eventstore
            """;

        File.WriteAllText(Path.Combine(tempDir, "statestore.yaml"), stateStoreYaml);
        File.WriteAllText(Path.Combine(tempDir, "pubsub.yaml"), pubSubYaml);

        return tempDir;
    }

    /// <summary>
    /// Waits for daprd to become healthy using the outbound healthcheck,
    /// which verifies placement connectivity required for actors.
    /// </summary>
    private async Task WaitForDaprHealthAsync()
    {
        using var httpClient = new HttpClient();
        string healthUrl = $"{DaprHttpEndpoint}/v1.0/healthz/outbound";

        HttpStatusCode lastStatus = default;
        string? lastError = null;

        for (int i = 0; i < HealthTimeoutSeconds; i++)
        {
            if (_daprProcess?.HasExited == true)
            {
                throw new InvalidOperationException(
                    $"daprd exited with code {_daprProcess.ExitCode} during health check.\n" +
                    $"stdout:\n{GetCapturedStdout()}\n" +
                    $"stderr:\n{GetCapturedStderr()}");
            }

            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(healthUrl).ConfigureAwait(false);
                lastStatus = response.StatusCode;
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException ex)
            {
                lastError = ex.Message;
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        string diagnostics =
            $"Dapr sidecar did not become healthy within {HealthTimeoutSeconds} seconds.\n" +
            $"Health endpoint: {healthUrl}\n" +
            $"Last HTTP status: {lastStatus}\n" +
            $"Last connection error: {lastError ?? "(none)"}\n" +
            $"Ports: app={_appPort}, daprHttp={_daprHttpPort}, daprGrpc={_daprGrpcPort}\n" +
            $"--- daprd stdout (last 2000 chars) ---\n{TailString(GetCapturedStdout(), 2000)}\n" +
            $"--- daprd stderr (last 2000 chars) ---\n{TailString(GetCapturedStderr(), 2000)}";

        throw new InvalidOperationException(diagnostics);
    }

    private string GetCapturedStdout()
    {
        lock (_daprStdout)
        {
            return _daprStdout.ToString();
        }
    }

    private string GetCapturedStderr()
    {
        lock (_daprStderr)
        {
            return _daprStderr.ToString();
        }
    }

    private static string TailString(string value, int maxChars)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxChars)
        {
            return value;
        }

        return "..." + value[^maxChars..];
    }

    /// <summary>
    /// Allocates multiple unique ports simultaneously, eliminating the TOCTOU race
    /// where sequential allocations can return the same port after the listener closes.
    /// </summary>
    private static int[] GetAvailablePorts(int count)
    {
        var listeners = new TcpListener[count];
        int[] ports = new int[count];

        for (int i = 0; i < count; i++)
        {
            listeners[i] = new TcpListener(IPAddress.Loopback, 0);
            listeners[i].Start();
            ports[i] = ((IPEndPoint)listeners[i].LocalEndpoint).Port;
        }

        for (int i = 0; i < count; i++)
        {
            listeners[i].Stop();
        }

        return ports;
    }

    /// <summary>
    /// Verifies the test host app is accepting HTTP requests on the expected port.
    /// Uses actual HTTP GET (not just TCP connect) to confirm the full request pipeline is alive.
    /// Also detects if the host has started shutting down.
    /// </summary>
    private async Task VerifyAppListeningAsync()
    {
        if (_hostStopping)
        {
            throw new InvalidOperationException(
                $"Test host is shutting down before verification.\n" +
                $"Stop stack trace:\n{_hostStopStackTrace}");
        }

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3),
        };
        string healthUrl = $"http://127.0.0.1:{_appPort}/healthz";
        string? lastError = null;

        for (int i = 0; i < 30; i++)
        {
            if (_hostStopping)
            {
                throw new InvalidOperationException(
                    $"Test host began shutting down during verification (attempt {i + 1}).\n" +
                    $"Stop stack trace:\n{_hostStopStackTrace}");
            }

            try
            {
                _ = await httpClient.GetAsync(healthUrl).ConfigureAwait(false);
                return;
            }
            catch (HttpRequestException ex)
            {
                lastError = ex.Message;
            }
            catch (TaskCanceledException)
            {
                lastError = "Request timed out";
            }

            await Task.Delay(200).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"Test host HTTP check failed on http://127.0.0.1:{_appPort} after 30 attempts.\n" +
            $"Host stopping: {_hostStopping}\n" +
            $"Last HTTP error: {lastError}\n" +
            $"Ports: app={_appPort}, daprHttp={_daprHttpPort}, daprGrpc={_daprGrpcPort}\n" +
            $"--- daprd stdout (last 2000 chars) ---\n{TailString(GetCapturedStdout(), 2000)}\n" +
            $"--- daprd stderr (last 2000 chars) ---\n{TailString(GetCapturedStderr(), 2000)}");
    }
}
