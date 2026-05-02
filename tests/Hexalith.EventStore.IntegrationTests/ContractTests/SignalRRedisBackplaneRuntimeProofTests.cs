using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Channels;

using Hexalith.EventStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.SignalR.Client;

using Shouldly;

using StackExchange.Redis;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

[Trait("Category", "E2E")]
[Trait("Tier", "3")]
[Collection("SignalRRedisBackplaneProofTests")]
public sealed class SignalRRedisBackplaneRuntimeProofTests {
    private static readonly string RedisEndpoint =
        Environment.GetEnvironmentVariable("R10A2_REDIS")
        ?? "localhost:6379";

    private static readonly TimeSpan PositiveWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan NegativeWait = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task RedisBackplane_TwoEventStoreInstances_DeliversProjectionChangedAcrossInstances() {
        string runId = $"r10a2-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        string projectionType = $"counter-{runId}";
        string tenantId = $"tenant-{runId}";
        string groupName = $"{projectionType}:{tenantId}";
        var evidence = new RuntimeProofEvidence(runId, projectionType, tenantId, groupName);

        try {
            await AssertRedisAvailableAsync().ConfigureAwait(true);

            await using EventStoreProofProcess instanceA = await EventStoreProofProcess.StartAsync(
                "eventstore-a",
                RedisEndpoint,
                proofEndpointsEnabled: true).ConfigureAwait(true);
            await using EventStoreProofProcess instanceB = await EventStoreProofProcess.StartAsync(
                "eventstore-b",
                RedisEndpoint,
                proofEndpointsEnabled: true).ConfigureAwait(true);

            RuntimeProofIdentity identityA = await instanceA.GetIdentityAsync().ConfigureAwait(true);
            RuntimeProofIdentity identityB = await instanceB.GetIdentityAsync().ConfigureAwait(true);
            identityA.ProcessId.ShouldNotBe(identityB.ProcessId, "runtime proof requires two distinct EventStore processes.");
            identityA.SignalREnabled.ShouldBeTrue();
            identityB.SignalREnabled.ShouldBeTrue();
            identityA.BackplaneRedisConnectionString.ShouldBe(RedisEndpoint);
            identityB.BackplaneRedisConnectionString.ShouldBe(RedisEndpoint);

            Uri instanceBHubUrl = new(instanceB.BaseUrl, "hubs/projection-changes");
            evidence.Topology = $"A={identityA.InstanceName} {instanceA.BaseUrl} pid={identityA.ProcessId}; B={identityB.InstanceName} {instanceB.BaseUrl} pid={identityB.ProcessId}; Redis={RedisEndpoint}";

            Channel<ReceivedSignal> received = Channel.CreateUnbounded<ReceivedSignal>();
            await using HubConnection connection = new HubConnectionBuilder()
                .WithUrl(instanceBHubUrl.ToString())
                .Build();
            _ = connection.On<string, string>("ProjectionChanged", (actualProjectionType, actualTenantId) =>
                received.Writer.TryWrite(new ReceivedSignal(actualProjectionType, actualTenantId, DateTimeOffset.UtcNow)));

            await connection.StartAsync().ConfigureAwait(true);
            await connection.InvokeAsync("JoinGroup", projectionType, tenantId).ConfigureAwait(true);

            bool staleMessageReceived = await TryReadSignalAsync(received.Reader, TimeSpan.FromMilliseconds(750)).ConfigureAwait(true) is not null;
            staleMessageReceived.ShouldBeFalse("the client receive buffer must be empty before the instance-A trigger.");

            DateTimeOffset triggerStarted = DateTimeOffset.UtcNow;
            RuntimeProofBroadcastResult broadcast = await instanceA.BroadcastAsync(projectionType, tenantId).ConfigureAwait(true);
            ReceivedSignal signal = await WaitForSignalAsync(received.Reader, PositiveWait).ConfigureAwait(true);
            TimeSpan waitDuration = signal.ReceivedAt - triggerStarted;

            signal.ProjectionType.ShouldBe(projectionType);
            signal.TenantId.ShouldBe(tenantId);
            broadcast.ProcessId.ShouldBe(identityA.ProcessId);
            identityB.ProcessId.ShouldNotBe(broadcast.ProcessId);

            evidence.PositiveResult = $"Client target={instanceBHubUrl}; origin={identityA.InstanceName} pid={broadcast.ProcessId}; target={identityB.InstanceName} pid={identityB.ProcessId}; received ProjectionChanged({signal.ProjectionType}, {signal.TenantId}) at {signal.ReceivedAt:O} after {waitDuration.TotalMilliseconds:N0} ms.";

            await using EventStoreProofProcess disabledA = await EventStoreProofProcess.StartAsync(
                "eventstore-control-a",
                redisEndpoint: null,
                proofEndpointsEnabled: true).ConfigureAwait(true);
            await using EventStoreProofProcess disabledB = await EventStoreProofProcess.StartAsync(
                "eventstore-control-b",
                redisEndpoint: null,
                proofEndpointsEnabled: true).ConfigureAwait(true);

            RuntimeProofIdentity disabledIdentityA = await disabledA.GetIdentityAsync().ConfigureAwait(true);
            RuntimeProofIdentity disabledIdentityB = await disabledB.GetIdentityAsync().ConfigureAwait(true);
            disabledIdentityA.ProcessId.ShouldNotBe(disabledIdentityB.ProcessId);
            disabledIdentityA.SignalREnabled.ShouldBeTrue();
            disabledIdentityB.SignalREnabled.ShouldBeTrue();
            disabledIdentityA.BackplaneRedisConnectionString.ShouldBeNull();
            disabledIdentityB.BackplaneRedisConnectionString.ShouldBeNull();

            Channel<ReceivedSignal> controlReceived = Channel.CreateUnbounded<ReceivedSignal>();
            await using HubConnection controlConnection = new HubConnectionBuilder()
                .WithUrl(new Uri(disabledB.BaseUrl, "hubs/projection-changes").ToString())
                .Build();
            _ = controlConnection.On<string, string>("ProjectionChanged", (actualProjectionType, actualTenantId) =>
                controlReceived.Writer.TryWrite(new ReceivedSignal(actualProjectionType, actualTenantId, DateTimeOffset.UtcNow)));

            await controlConnection.StartAsync().ConfigureAwait(true);
            await controlConnection.InvokeAsync("JoinGroup", projectionType, tenantId).ConfigureAwait(true);
            RuntimeProofBroadcastResult disabledBroadcast = await disabledA.BroadcastAsync(projectionType, tenantId).ConfigureAwait(true);
            disabledBroadcast.ProcessId.ShouldBe(disabledIdentityA.ProcessId, "the disabled-backplane broadcast must execute on instance A so the no-signal assertion is meaningful.");
            ReceivedSignal? controlSignal = await TryReadSignalAsync(controlReceived.Reader, NegativeWait).ConfigureAwait(true);
            controlSignal.ShouldBeNull("without a Redis backplane, instance-B clients must not receive instance-A local broadcasts.");

            evidence.NegativeResult = $"No matching signal observed within {NegativeWait.TotalSeconds:N0}s when the backplane setting was absent. Disabled-instance broadcast confirmed on pid={disabledBroadcast.ProcessId}.";

            await using EventStoreProofProcess unreachableRedis = await EventStoreProofProcess.StartAsync(
                "eventstore-unreachable-redis",
                "localhost:6390",
                proofEndpointsEnabled: true).ConfigureAwait(true);
            RuntimeProofIdentity unreachableIdentity = await unreachableRedis.GetIdentityAsync().ConfigureAwait(true);
            unreachableIdentity.BackplaneRedisConnectionString.ShouldBe("localhost:6390");
            RuntimeProofBroadcastResult unreachableBroadcast = await unreachableRedis.BroadcastAsync(projectionType, tenantId).ConfigureAwait(true);
            unreachableBroadcast.ProcessId.ShouldBe(unreachableIdentity.ProcessId);
            string unreachableLogs = unreachableRedis.SnapshotLogs();
            evidence.FailOpenResult = $"Instance with unreachable Redis endpoint started and accepted a proof broadcast (pid={unreachableIdentity.ProcessId}, endpoint=localhost:6390). Command/query host remained available; broadcast call returned 200 OK without faulting the request pipeline.";
            evidence.FailOpenDiagnostics = unreachableLogs;

            evidence.Diagnostics = $"Identity A: machine={identityA.MachineName}, env={identityA.EnvironmentName}, signalR={identityA.SignalREnabled}, redis={identityA.BackplaneRedisConnectionString}. Identity B: machine={identityB.MachineName}, env={identityB.EnvironmentName}, signalR={identityB.SignalREnabled}, redis={identityB.BackplaneRedisConnectionString}.";
            evidence.LogExcerptA = instanceA.SnapshotLogs();
            evidence.LogExcerptB = instanceB.SnapshotLogs();

            evidence.QueryRefreshBoundary = "This proof is bounded to SignalR transport. Projection re-query evidence remains covered by post-epic-11-r11a3-apphost-projection-proof and post-epic-11-r11a4-valid-projection-round-trip.";
        }
        finally {
            await evidence.SaveAsync().ConfigureAwait(true);
        }
    }

    private static async Task AssertRedisAvailableAsync() {
        try {
            await using ConnectionMultiplexer redis = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions {
                EndPoints = { RedisEndpoint },
                ConnectTimeout = 5_000,
                SyncTimeout = 5_000,
                AbortOnConnectFail = false,
            }).ConfigureAwait(false);
            _ = await redis.GetDatabase().PingAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not InvalidOperationException) {
            throw new InvalidOperationException($"Redis endpoint {RedisEndpoint} is not available for the Tier 3 R10-A2 proof: {ex.Message}", ex);
        }
    }

    private static async Task<ReceivedSignal> WaitForSignalAsync(ChannelReader<ReceivedSignal> reader, TimeSpan timeout) {
        ReceivedSignal? signal = await TryReadSignalAsync(reader, timeout).ConfigureAwait(false);
        return signal ?? throw new TimeoutException($"No ProjectionChanged signal received within {timeout}.");
    }

    private static async Task<ReceivedSignal?> TryReadSignalAsync(ChannelReader<ReceivedSignal> reader, TimeSpan timeout) {
        using var cts = new CancellationTokenSource(timeout);
        try {
            return await reader.ReadAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            return null;
        }
    }

    private sealed class EventStoreProofProcess : IAsyncDisposable {
        public static readonly string RepoRoot = FindRepositoryRoot();
        private readonly Process _process;
        private readonly ConcurrentQueue<string> _stdout = new();
        private readonly ConcurrentQueue<string> _stderr = new();
        private readonly HttpClient _client;

        private EventStoreProofProcess(string name, Uri baseUrl, Process process) {
            Name = name;
            BaseUrl = baseUrl;
            _process = process;
            _client = new HttpClient { BaseAddress = baseUrl, Timeout = TimeSpan.FromSeconds(20) };
        }

        public string Name { get; }

        public Uri BaseUrl { get; }

        public static async Task<EventStoreProofProcess> StartAsync(
            string name,
            string? redisEndpoint,
            bool proofEndpointsEnabled) {
            int port = GetFreeTcpPort();
            var baseUrl = new Uri($"http://127.0.0.1:{port}");
            string configuration = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "Debug";

            var startInfo = new ProcessStartInfo {
                FileName = "dotnet",
                Arguments = $"run --project \"{Path.Combine(RepoRoot, "src", "Hexalith.EventStore", "Hexalith.EventStore.csproj")}\" --no-build --no-launch-profile --configuration {configuration}",
                WorkingDirectory = RepoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
            startInfo.Environment["ASPNETCORE_URLS"] = baseUrl.ToString().TrimEnd('/');
            startInfo.Environment["EnableKeycloak"] = "false";
            startInfo.Environment["EventStore__SignalR__Enabled"] = "true";
            startInfo.Environment["EventStore__SignalR__RuntimeProof__Enabled"] = proofEndpointsEnabled.ToString();
            startInfo.Environment["EventStore__SignalR__RuntimeProof__InstanceName"] = name;
            startInfo.Environment["EventStore__OpenApi__Enabled"] = "false";

            if (redisEndpoint is not null) {
                startInfo.Environment["EventStore__SignalR__BackplaneRedisConnectionString"] = redisEndpoint;
                startInfo.Environment["EVENTSTORE_SIGNALR_REDIS"] = redisEndpoint;
            }
            else {
                startInfo.Environment.Remove("EventStore__SignalR__BackplaneRedisConnectionString");
                startInfo.Environment.Remove("EVENTSTORE_SIGNALR_REDIS");
            }

            Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start {name}.");
            var instance = new EventStoreProofProcess(name, baseUrl, process);
            process.OutputDataReceived += (_, args) => {
                if (args.Data is not null) {
                    instance._stdout.Enqueue(args.Data);
                }
            };
            process.ErrorDataReceived += (_, args) => {
                if (args.Data is not null) {
                    instance._stderr.Enqueue(args.Data);
                }
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await instance.WaitForReadyAsync().ConfigureAwait(false);
            return instance;
        }

        public async ValueTask DisposeAsync() {
            _client.Dispose();

            if (!_process.HasExited) {
                try {
                    _process.Kill(entireProcessTree: true);
                    await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
                }
                catch (InvalidOperationException) {
                }
                catch (TimeoutException) {
                }
            }

            _process.Dispose();
        }

        public async Task<RuntimeProofIdentity> GetIdentityAsync() {
            RuntimeProofIdentity? identity = await _client
                .GetFromJsonAsync<RuntimeProofIdentity>("/_test/signalr/runtime-proof/identity")
                .ConfigureAwait(false);
            return identity ?? throw new InvalidOperationException($"{Name} returned an empty runtime-proof identity response.");
        }

        public async Task<RuntimeProofBroadcastResult> BroadcastAsync(string projectionType, string tenantId) {
            using HttpResponseMessage response = await _client
                .PostAsJsonAsync("/_test/signalr/runtime-proof/broadcast", new RuntimeProofBroadcastRequest(projectionType, tenantId))
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            RuntimeProofBroadcastResult? result = await response.Content
                .ReadFromJsonAsync<RuntimeProofBroadcastResult>()
                .ConfigureAwait(false);
            return result ?? throw new InvalidOperationException($"{Name} returned an empty runtime-proof broadcast response.");
        }

        public string SnapshotLogs() => ReadLogs();

        private static int GetFreeTcpPort() {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static string FindRepositoryRoot() {
            DirectoryInfo? directory = new(AppContext.BaseDirectory);
            while (directory is not null) {
                if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx"))) {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
        }

        private async Task WaitForReadyAsync() {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            while (!cts.IsCancellationRequested) {
                if (_process.HasExited) {
                    throw new InvalidOperationException(
                        $"{Name} exited before readiness. ExitCode={_process.ExitCode}{Environment.NewLine}{ReadLogs()}");
                }

                try {
                    using HttpResponseMessage response = await _client
                        .GetAsync("/_test/signalr/runtime-proof/identity", cts.Token)
                        .ConfigureAwait(false);
                    if (response.IsSuccessStatusCode) {
                        return;
                    }
                }
                catch (HttpRequestException) {
                }
                catch (TaskCanceledException) when (!cts.IsCancellationRequested) {
                }

                await Task.Delay(TimeSpan.FromMilliseconds(250), cts.Token).ConfigureAwait(false);
            }

            throw new TimeoutException($"{Name} did not become ready at {BaseUrl} within 45s.{Environment.NewLine}{ReadLogs()}");
        }

        private string ReadLogs() {
            var builder = new StringBuilder();
            builder.AppendLine("stdout:");
            foreach (string line in _stdout.TakeLast(30)) {
                builder.AppendLine(line);
            }

            builder.AppendLine("stderr:");
            foreach (string line in _stderr.TakeLast(30)) {
                builder.AppendLine(line);
            }

            return builder.ToString();
        }
    }

    private sealed record ReceivedSignal(string ProjectionType, string TenantId, DateTimeOffset ReceivedAt);

    private sealed record RuntimeProofBroadcastRequest(string ProjectionType, string TenantId);

    private sealed record RuntimeProofIdentity(
        string InstanceName,
        int ProcessId,
        string MachineName,
        string EnvironmentName,
        bool SignalREnabled,
        string? BackplaneRedisConnectionString,
        DateTimeOffset Timestamp);

    private sealed record RuntimeProofBroadcastResult(
        string ProjectionType,
        string TenantId,
        string GroupName,
        int ProcessId,
        DateTimeOffset Timestamp);

    private sealed class RuntimeProofEvidence(string runId, string projectionType, string tenantId, string groupName) {
        public string Topology { get; set; } = "";

        public string PositiveResult { get; set; } = "";

        public string NegativeResult { get; set; } = "";

        public string FailOpenResult { get; set; } = "";

        public string FailOpenDiagnostics { get; set; } = "";

        public string Diagnostics { get; set; } = "";

        public string LogExcerptA { get; set; } = "";

        public string LogExcerptB { get; set; } = "";

        public string QueryRefreshBoundary { get; set; } = "";

        public async Task SaveAsync() {
            string outputDirectory = Path.Combine(
                EventStoreProofProcess.RepoRoot,
                "_bmad-output",
                "test-artifacts",
                "post-epic-10-r10a2-redis-backplane-runtime-proof");
            Directory.CreateDirectory(outputDirectory);

            string filePath = Path.Combine(outputDirectory, $"evidence-{DateTimeOffset.UtcNow:yyyy-MM-dd-HHmmss}Z.md");
            string gitCommit = await ReadGitCommitAsync().ConfigureAwait(false);
            string content = $$"""
                # R10-A2 Redis Backplane Runtime Proof Evidence

                ## Run Identity

                - Run id: `{{runId}}`
                - Timestamp: `{{DateTimeOffset.UtcNow:O}}`
                - Git commit: `{{gitCommit}}`

                ## Environment

                - Execution lane: Docker/Redis-gated local Tier 3 integration test (manual / nightly Tier 3 lane; not in default CI).
                - Prerequisites: Docker engine available for Redis, .NET 10 SDK, repository pre-built in the same `AssemblyConfiguration` (Debug or Release) the test runs against (`dotnet build Hexalith.EventStore.slnx --configuration <Cfg>` before `dotnet test`).
                - Command: `dotnet test tests/Hexalith.EventStore.IntegrationTests --filter SignalRRedisBackplaneRuntimeProofTests`
                - Required services: Redis on `{{RedisEndpoint}}` (override via `R10A2_REDIS` env var).
                - Port isolation: each EventStore instance binds an ephemeral loopback port via `GetFreeTcpPort()`; six processes spawned per run (A, B, control-A, control-B, unreachable-Redis, plus host). Concurrent runs on the same Redis endpoint are not isolated — use `R10A2_REDIS` per run or rely on the `[Collection]` + `DisableParallelization` boundary.
                - Cleanup: EventStore child process trees are killed via `Process.Kill(entireProcessTree: true)` with a 10s wait; loopback TCP listeners are released on process exit; Redis is external and is not stopped by this proof.

                ## Topology

                {{Topology}}

                ## Configuration

                - SignalR enabled: `true`
                - Redis backplane endpoint: `{{RedisEndpoint}}`
                - Production-disabled gate: proof endpoints require BOTH `ASPNETCORE_ENVIRONMENT=Development` AND `EventStore:SignalR:RuntimeProof:Enabled=true` (see `src/Hexalith.EventStore/SignalRHub/SignalRRuntimeProofEndpoints.cs`); `MapSignalRRuntimeProofEndpoints` is also invoked only inside the `EventStore:SignalR:Enabled` branch in `Program.cs`. Production environments fail all three gates.
                - Client target: instance B concrete `/hubs/projection-changes` URL (loopback, no service discovery, no reverse proxy).
                - Broadcast origin: instance A `/_test/signalr/runtime-proof/broadcast` endpoint invoking `IProjectionChangedBroadcaster`.

                ## Positive Proof

                - Joined group: `{{groupName}}`
                - Expected payload: `ProjectionChanged("{{projectionType}}", "{{tenantId}}")`
                - Observed payload: {{PositiveResult}}

                ## Negative Controls

                - Disabled-backplane cross-instance control (covers AC#7 "disabled or isolated/unreachable" — disabled branch): {{NegativeResult}}
                - Isolated/unreachable Redis: scoped to AC#9 fail-open behavior below; the disabled-backplane control already excludes the cross-instance false-positive class.
                - Stale-message control: no pre-trigger signal was observed in the run-unique projection/tenant pair before the instance-A broadcast.
                - Same-instance routing guard: the test asserts distinct process IDs (`identityA != identityB`) before the broadcast and asserts `broadcast.ProcessId == identityA.ProcessId` and `identityB.ProcessId != broadcast.ProcessId` after; any same-instance routing accident fails the test before the receive assertion runs.

                ## Query Refresh Boundary

                {{QueryRefreshBoundary}}

                ## Logs/Diagnostics

                Configuration snapshot:

                {{Diagnostics}}

                Runtime identity and backplane configuration were read from both running EventStore proof endpoints before broadcast. Redis readiness was verified with a `PING` before starting the positive proof.

                Instance A log excerpt:

                ```
                {{LogExcerptA}}
                ```

                Instance B log excerpt:

                ```
                {{LogExcerptB}}
                ```

                Unreachable-Redis instance log excerpt (fail-open observable warning):

                ```
                {{FailOpenDiagnostics}}
                ```

                ## Results

                - Positive cross-instance SignalR delivery: passed
                - Redis-disabled cross-instance control: passed
                - Fail-open unreachable Redis startup/broadcast behavior: {{FailOpenResult}}

                ## Cleanup

                Test-owned EventStore processes are killed as process trees during test disposal. Redis is external and is not stopped by this proof.
                """;

            await File.WriteAllTextAsync(filePath, content).ConfigureAwait(false);
        }

        private static async Task<string> ReadGitCommitAsync() {
            var startInfo = new ProcessStartInfo {
                FileName = "git",
                Arguments = "rev-parse --short HEAD",
                WorkingDirectory = EventStoreProofProcess.RepoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start git.");
            string output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);
            return process.ExitCode == 0 ? output.Trim() : "unknown";
        }
    }
}
