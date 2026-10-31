using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;
using Hexalith.EventStore.Sharding.TestHost;

namespace Hexalith.EventStore.Sharding.Tests;

/// <summary>Owns one bounded, disposable, synthetic two-host Dapr/PostgreSQL topology.</summary>
internal sealed class ShardingFixture : IAsyncDisposable
{
    private const string PostgresImage = "postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636";
    private const string PlacementImage = "ghcr.io/dapr/placement@sha256:6caf20016d115d4a7f133b9206b739a10abd9f558d76683b27be9ab60f759e26";
    private const string SchedulerImage = "ghcr.io/dapr/scheduler@sha256:c9bb9ada0cd6a63cd92c26470da1985124e423432af4e39f09b96979fd1059c0";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly List<string> _containers = [];
    private readonly Dictionary<string, string?> _containerPlans = new(StringComparer.Ordinal);
    private readonly List<Process> _processes = [];
    private readonly List<Process> _commandProcesses = [];
    private readonly List<object> _observations = [];
    private readonly List<string> _commands = [];
    private readonly HashSet<int> _ports = [];
    private readonly Dictionary<string, Oq8BoundedLog> _logs = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly RunActivityTracker _activities = new();
    private readonly CancellationTokenSource _work = new();
    private readonly CancellationTokenSource _stopWatchdog = new();
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly OwnedNode[] _nodes = [new(), new()];
    private readonly string _runId = "sharding-" + Guid.NewGuid().ToString("N");
    private string _root = string.Empty;
    private string _scratch = string.Empty;
    private string _artifacts = string.Empty;
    private string _hostDirectory = string.Empty;
    private string _daprd = string.Empty;
    private string _postgres = string.Empty;
    private string _password = string.Empty;
    private string _bindingPath = string.Empty;
    private string _dockerEndpoint = string.Empty;
    private DateTimeOffset _expiry;
    private int _placementPort;
    private int _schedulerPort;
    private int _postgresPort;
    private Task? _watchdog;
    private bool _disposed;
    private bool _fenced;
    private bool _expiryTriggered;
    private Exception? _watchdogFailure;
    private readonly List<string> _cleanupHistory = [];
    private int _recoveredContainerCount;

    /// <summary>Gets the retained evidence location outside deleted runtime scratch.</summary>
    internal string Artifacts => _artifacts;

    /// <summary>Gets whether disposal verified every owned resource absent.</summary>
    internal bool CleanupVerified { get; private set; }

    /// <summary>Gets the number of exact container identities recovered without a CID file.</summary>
    internal int RecoveredContainerCount => _recoveredContainerCount;

    /// <summary>Gets the number of external commands started by this fixture.</summary>
    internal int CommandCount => _commands.Count;

    /// <summary>Gets the current exact application and sidecar process identities.</summary>
    internal int[] ProcessIds => _nodes.SelectMany(node => new[] { node.Application?.Id, node.Sidecar?.Id }).OfType<int>().ToArray();

    /// <summary>Prepares a binding before creating any live resources.</summary>
    /// <param name="failAfter">An optional deterministic partial-startup failure point.</param>
    /// <returns>A task completing after independent actor runtimes are ready.</returns>
    internal async Task InitializeAsync(string? failAfter = null, Func<string?>? readExpiry = null,
        Func<CancellationToken, Task>? beforePreflight = null, Func<Task<string>>? resolveDockerEndpoint = null)
    {
        // Fail before directories, child processes, Docker, or any workload when operator input is absent.
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset created = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds());
        _expiry = RunBinding.ParseExpiry(readExpiry is null ? Environment.GetEnvironmentVariable(RunBinding.ExpiryVariable) : readExpiry(), now);
        ArmExpiry();
        try
        {
            await _lifecycle.WaitAsync();
            try
            {
                if (beforePreflight is not null) await beforePreflight(_work.Token);
                EnsureActive();
                _dockerEndpoint = await DockerEndpointPolicy.ResolveAsync(null, null, _ => null,
                    _ => resolveDockerEndpoint is not null ? resolveDockerEndpoint() : ResolveDockerEndpointAsync());
                EnsureActive();
                _root = FindRoot();
                _hostDirectory = Path.Combine(_root, "tests", "Hexalith.EventStore.Sharding.TestHost", "bin", "Debug", "net10.0");
                if (!File.Exists(Path.Combine(_hostDirectory, "Hexalith.EventStore.Sharding.TestHost.dll")))
                    throw new InvalidOperationException("Build the test host in Debug before execution.");
                _daprd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dapr", "bin", OperatingSystem.IsWindows() ? "daprd.exe" : "daprd");
                _scratch = Path.Combine(Path.GetTempPath(), "eventstore-" + _runId);
                _artifacts = Path.Combine(_root, "artifacts", "sharding", _runId);
                Directory.CreateDirectory(_artifacts);
                Directory.CreateDirectory(Path.Combine(_scratch, "components"));
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(_scratch, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                WriteOwnershipJournal();
                EnsureActive();
                _password = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
                _placementPort = Port();
                _schedulerPort = Port();
                _postgresPort = Port();
                foreach (OwnedNode node in _nodes)
                {
                    node.AppPort = Port();
                    node.HttpPort = Port();
                    node.GrpcPort = Port();
                    node.InternalPort = Port();
                    node.MetricsPort = Port();
                    node.ProfilePort = Port();
                }

                string component = CreateComponent();
                string discovery = CreateDiscovery();
                File.WriteAllText(Path.Combine(_scratch, "components", "statestore.yaml"), component);
                File.WriteAllText(Path.Combine(_scratch, "discovery.yaml"), discovery);
                var runtime = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["dotnet-sdk"] = (await CommandAsync("dotnet", ["--version"])).Trim(),
                    ["dotnet-runtimes"] = (await CommandAsync("dotnet", ["--list-runtimes"])).Trim(),
                    ["framework"] = RuntimeInformation.FrameworkDescription,
                    ["os"] = RuntimeInformation.OSDescription,
                    ["architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
                    ["daprd-version"] = (await CommandAsync(_daprd, ["--version"])).Trim(),
                    ["daprd-sha256"] = RunBinding.HashFile(_daprd),
                    ["docker-server"] = (await CommandAsync("docker", ["version", "--format", "{{.Server.Version}}"])).Trim(),
                };
                foreach (string image in new[] { PostgresImage, PlacementImage, SchedulerImage })
                {
                    runtime[image] = (await CommandAsync("docker", ["image", "inspect", image, "--format", "{{.Id}}"])).Trim();
                }

                var configuration = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["scope"] = "isolated synthetic local development only; production_evidence_qualified=false",
                    ["authorization"] = "spec-4-6-isolated-dapr-postgresql-sharding.md; owner jpiquot; conversation 01a18ded-3ceb-7c92-b5f5-dbf1e38a4686",
                    ["machine"] = Environment.MachineName,
                    ["docker-endpoint"] = _dockerEndpoint,
                    ["app-id"] = _runId,
                    ["namespace"] = _runId,
                    ["topology"] = "2 independent dotnet hosts + 2 daprd; private PostgreSQL, placement, scheduler containers; SQLite discovery; loopback only",
                    ["ports"] = JsonSerializer.Serialize(new { _placementPort, _schedulerPort, _postgresPort, nodes = _nodes.Select(node => new { node.AppPort, node.HttpPort, node.GrpcPort, node.InternalPort, node.MetricsPort, node.ProfilePort }) }),
                    ["component-sha256"] = RunBinding.HashText(component),
                    ["discovery-sha256"] = RunBinding.HashText(discovery),
                    ["source-sha256"] = RunBinding.HashText(JsonSerializer.Serialize(SourceManifest())),
                    ["failure-injection"] = failAfter ?? "none",
                    ["lifecycle"] = "exclusive UTC fence; cancel/drain work; exact process-tree/CID teardown; absence verified",
                };
                var binding = new RunBinding(_runId, Canonical(created), Canonical(_expiry),
                    "411005b4ccb2352249c83330c54967c6a22beba1", "160331d25451928ff3c3dea2300b65cab4f97c3b",
                    "995fcecd16b3421ec9ff666d0884bfb5e436932aa49529c152fb7c439172a1fd",
                    RunBinding.HashDirectory(_hostDirectory), RunBinding.HashDirectory(AppContext.BaseDirectory), runtime, configuration);
                _bindingPath = Path.Combine(_scratch, "binding.json");
                string bindingJson = JsonSerializer.Serialize(binding, JsonOptions);
                File.WriteAllText(_bindingPath, bindingJson);
                File.WriteAllText(Path.Combine(_artifacts, "binding.json"), bindingJson);
                File.WriteAllText(Path.Combine(_artifacts, "sources.json"), JsonSerializer.Serialize(SourceManifest(), JsonOptions));
                File.WriteAllText(Path.Combine(_artifacts, "configuration.json"), JsonSerializer.Serialize(new
                {
                    component = Redact(component), discovery = Redact(discovery),
                    bindingSha256 = RunBinding.HashFile(_bindingPath),
                }, JsonOptions));
                EnsureActive();
                _postgres = await ContainerAsync("postgres", PostgresImage, ["-e", "POSTGRES_PASSWORD=" + _password, "-e", "POSTGRES_DB=sharding", "-p", $"127.0.0.1:{_postgresPort}:5432"], [], failAfter == "missing-cid");
                await WaitPostgresAsync();
                Record("postgres-runtime", new { version = (await QueryAsync("SELECT version();")).Trim(), image = PostgresImage });
                InjectFailure(failAfter, "postgres");
                string placement = await ContainerAsync("placement", PlacementImage, ["-p", $"127.0.0.1:{_placementPort}:{_placementPort}"],
                    ["/placement", "--port", _placementPort.ToString(CultureInfo.InvariantCulture), "--listen-address", "0.0.0.0", "--log-level", "info"]);
                await ObserveControlPlaneVersionAsync("placement", placement, PlacementImage);
                InjectFailure(failAfter, "placement");
                string scheduler = await ContainerAsync("scheduler", SchedulerImage, ["-p", $"127.0.0.1:{_schedulerPort}:{_schedulerPort}"],
                    ["/scheduler", "--port", _schedulerPort.ToString(CultureInfo.InvariantCulture), "--listen-address", "0.0.0.0", "--override-broadcast-host-port", "127.0.0.1:" + _schedulerPort,
                        "--etcd-data-dir", "/tmp/sharding-etcd", "--log-level", "info"]);
                await ObserveControlPlaneVersionAsync("scheduler", scheduler, SchedulerImage);
                for (int index = 0; index < _nodes.Length; index++)
                {
                    await StartNodeAsync(index);
                    if (index == 0) InjectFailure(failAfter, "node");
                }

                Record("ready", new { processIds = ProcessIds, bindingSha256 = RunBinding.HashFile(_bindingPath), createdUtc = Canonical(created) });
            }
            finally
            {
                _lifecycle.Release();
            }
        }
        catch (Exception startup)
        {
            if (startup is OperationCanceledException && DateTimeOffset.UtcNow >= _expiry && _work.IsCancellationRequested)
            {
                _expiryTriggered = true;
                Record("startup-expired", new { exclusiveExpiryUtc = Canonical(_expiry) });
            }

            Record("startup-failure", new { type = startup.GetType().Name, message = Redact(startup.Message) });
            try
            {
                await DisposeAsync();
            }
            catch (Exception cleanup)
            {
                throw new AggregateException("Startup and owned-resource cleanup failed.", startup, cleanup);
            }

            throw;
        }
    }

    /// <summary>Submits through the selected independent sidecar and observes full range identities.</summary>
    /// <param name="nodeIndex">The ingress sidecar.</param>
    /// <param name="request">The synthetic operation.</param>
    /// <param name="actorId">An optional deliberately mismatched route.</param>
    /// <returns>The complete reply.</returns>
    internal Task<ShardReply> ApplyAsync(int nodeIndex, ShardRequest request, string? actorId = null)
        => _activities.Start(async () =>
    {
        EnsureActive();
        using var handler = new RunCancellationHandler(_work.Token);
        var proxy = new ActorProxyFactory(new ActorProxyOptions
        {
            HttpEndpoint = "http://127.0.0.1:" + _nodes[nodeIndex].HttpPort,
            DaprApiToken = string.Empty,
            UseJsonSerialization = true,
            RequestTimeout = TimeSpan.FromSeconds(20),
        }, handler).CreateActorProxy<IShardActor>(new ActorId(actorId ?? ShardIdentity.Encode(request.TenantId, request.Domain)), ShardActor.ActorType);
        ShardReply result = await proxy.ApplyAsync(request);
        Record("operation", new { nodeIndex, request, result });
        return result;
    });

    /// <summary>Reads the exact actor state JSONB row from the owned PostgreSQL provider.</summary>
    /// <param name="tenant">The exact tenant.</param>
    /// <param name="domain">The exact domain.</param>
    /// <returns>The raw durable complete state, or null when no allocation state exists.</returns>
    internal async Task<string?> DurableAsync(string tenant, string domain)
    {
        EnsureActive();
        string key = _runId + "||" + ShardActor.ActorType + "||" + ShardIdentity.Encode(tenant, domain) + "||" + ShardActor.StateKey;
        string value = (await QueryAsync("SELECT value::text FROM state WHERE key = '" + key.Replace("'", "''", StringComparison.Ordinal) + "';")).Trim();
        Record("durable", new { tenant, domain, key, json = value });
        return value.Length == 0 ? null : value;
    }

    /// <summary>Deserializes a complete durable state.</summary>
    /// <param name="tenant">The exact tenant.</param>
    /// <param name="domain">The exact domain.</param>
    /// <returns>The exact persisted record.</returns>
    internal async Task<ShardState?> StateAsync(string tenant, string domain)
    {
        string? json = await DurableAsync(tenant, domain);
        return json is null ? null : JsonSerializer.Deserialize<ShardState>(json, JsonOptions);
    }

    /// <summary>Replaces both application/sidecar pairs without reconstructing provider state.</summary>
    /// <returns>The replacement process identities.</returns>
    internal async Task<int[]> RestartAsync()
    {
        await _lifecycle.WaitAsync(_work.Token);
        try
        {
            EnsureActive();
            foreach (OwnedNode node in _nodes) await StopNodeAsync(node);
            for (int index = 0; index < _nodes.Length; index++) await StartNodeAsync(index);
            Record("restart", new { processIds = ProcessIds });
            return ProcessIds;
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    /// <summary>Waits for the operator's actual expiry and verified watchdog teardown.</summary>
    /// <returns>A task completing after cleanup.</returns>
    internal async Task AwaitExpiryAsync()
    {
        if (_watchdog is null) throw new InvalidOperationException("No bound watchdog exists.");
        await _watchdog;
        if (_watchdogFailure is not null) throw new InvalidOperationException("Expiry cleanup failed.", _watchdogFailure);
        if (!_expiryTriggered || DateTimeOffset.UtcNow < _expiry) throw new InvalidOperationException("The run did not execute its operator-expiry cancellation path.");
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _fenced = true;
        _work.Cancel(); // Fence client work before waiting for startup/restart to drain.
        _stopWatchdog.Cancel();
        await _lifecycle.WaitAsync();
        try
        {
            if (!CleanupVerified)
            {
                await CleanupAsync(DateTimeOffset.UtcNow >= _expiry ? "exclusive-expiry" : "completion-or-startup-failure");
                _disposed = true;
            }
        }
        finally
        {
            _lifecycle.Release();
        }

        if (_watchdog is not null) await _watchdog;
        if (_watchdogFailure is { } watchdogFailure)
        {
            _watchdogFailure = null;
            throw new InvalidOperationException("Watchdog teardown failed; any subsequent retry is recorded in cleanup history.", watchdogFailure);
        }
    }

    private async Task WatchExpiryAsync()
    {
        try
        {
            TimeSpan remaining = _expiry - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining, _stopWatchdog.Token);
            _stopWatchdog.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _expiryTriggered = true;
        _fenced = true;
        _work.Cancel();
        await _lifecycle.WaitAsync();
        try
        {
            if (!CleanupVerified)
            {
                await CleanupAsync("exclusive-expiry");
                _disposed = true;
            }
        }
        catch (Exception exception)
        {
            _watchdogFailure = exception;
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private async Task CleanupAsync(string reason)
    {
        var failures = new List<string>();
        await DrainActivitiesAsync(failures);
        foreach (Process process in _processes.Concat(_commandProcesses).Reverse().ToArray())
        {
            try { await StopProcessAsync(process); }
            catch (Exception exception) { failures.Add("process " + process.Id + ": " + exception.Message); }
        }

        if (_artifacts.Length == 0)
        {
            CleanupVerified = _containers.Count == 0 && _processes.Concat(_commandProcesses).All(process => process.HasExited) && failures.Count == 0;
            if (!CleanupVerified) throw new InvalidOperationException("Preflight teardown failed: " + string.Join("; ", failures));
            return;
        }

        try { await ReconcileContainersAsync(failures); }
        catch (Exception exception) { failures.Add("ownership reconciliation: " + exception.Message); }

        foreach (string container in _containers.AsEnumerable().Reverse())
        {
            try
            {
                string present = await CommandAsync("docker", ["ps", "-aq", "--no-trunc", "--filter", "id=" + container], cleanup: true);
                if (present.Trim().Length != 0)
                {
                    try
                    {
                        string output = await CommandAsync("docker", ["logs", "--tail", "200", container], cleanup: true);
                        TryDiagnostic(() => File.WriteAllText(Path.Combine(_artifacts, "container-" + container + ".log"), Redact(output)), failures);
                    }
                    catch (Exception exception) { Record("container-log-error", new { container, message = Redact(exception.Message) }); }
                    _ = await CommandAsync("docker", ["rm", "--force", "--volumes", container], cleanup: true);
                }

                string remaining = await CommandAsync("docker", ["ps", "-aq", "--no-trunc", "--filter", "id=" + container], cleanup: true);
                if (remaining.Trim().Length != 0) failures.Add("container remains: " + container);
            }
            catch (Exception exception) { failures.Add("container " + container + ": " + exception.Message); }
        }

        bool labeledAbsent = _dockerEndpoint.Length == 0;
        if (_dockerEndpoint.Length != 0)
        {
            try
            {
                string remaining = await CommandAsync("docker", ["ps", "-aq", "--no-trunc", "--filter", "label=hexalith.sharding.owner=" + _runId], cleanup: true);
                labeledAbsent = remaining.Trim().Length == 0;
                if (!labeledAbsent) failures.Add("owned label still identifies containers: " + remaining.Trim());
            }
            catch (Exception exception) { failures.Add("owned-label absence check: " + exception.Message); }
        }

        foreach ((string name, Oq8BoundedLog log) in _logs)
        {
            TryDiagnostic(() => File.WriteAllText(Path.Combine(_artifacts, name + ".log"), Redact(log.ToString())), failures);
        }

        try
        {
            if (Directory.Exists(_scratch)) Directory.Delete(_scratch, recursive: true);
            if (Directory.Exists(_scratch)) failures.Add("runtime scratch remains");
        }
        catch (Exception exception) { failures.Add("scratch: " + exception.Message); }

        bool absenceVerified = labeledAbsent && _processes.Concat(_commandProcesses).All(process => process.HasExited) && !Directory.Exists(_scratch);
        TryDiagnostic(() => File.WriteAllText(Path.Combine(_artifacts, "commands.json"), JsonSerializer.Serialize(_commands, JsonOptions)), failures);
        lock (_observations)
        {
            TryDiagnostic(() => File.WriteAllText(Path.Combine(_artifacts, "observations.json"), JsonSerializer.Serialize(_observations, JsonOptions)), failures);
        }
        _cleanupHistory.AddRange(failures.Select(Redact));
        TryDiagnostic(WriteOwnershipJournal, failures);
        CleanupVerified = absenceVerified && failures.Count == 0;
        TryDiagnostic(() => File.WriteAllText(Path.Combine(_artifacts, "cleanup.json"), JsonSerializer.Serialize(new
        {
            runId = _runId, reason, atUtc = Canonical(DateTimeOffset.UtcNow), exclusiveExpiryUtc = Canonical(_expiry),
            containers = _containers, processes = _processes.Select(process => new { process.Id, absent = process.HasExited }),
            transientCommands = _commandProcesses.Select(process => new { process.Id, absent = process.HasExited }),
            scratchRemoved = !Directory.Exists(_scratch), labeledOwnedContainersAbsent = labeledAbsent,
            verified = CleanupVerified, failures = failures.Select(Redact), failureHistory = _cleanupHistory,
            productionEvidenceQualified = false,
        }, JsonOptions)), failures);
        CleanupVerified = absenceVerified && failures.Count == 0;
        foreach (string failure in failures.Select(Redact))
            if (!_cleanupHistory.Contains(failure, StringComparer.Ordinal)) _cleanupHistory.Add(failure);
        if (!CleanupVerified) throw new InvalidOperationException("Owned-resource teardown failed: " + string.Join("; ", failures.Select(Redact)));
    }

    private async Task<string> ContainerAsync(string name, string image, string[] options, string[] command, bool simulateMissingCid = false)
    {
        EnsureActive();
        string plannedName = _runId + "-" + name;
        _containerPlans.Add(plannedName, null);
        WriteOwnershipJournal(); // Durable recovery identity exists before Docker can create the engine object.
        string cidFile = Path.Combine(_scratch, name + ".cid");
        return await Oq8OwnedContainerLaunch.RunAsync(cidFile, async () =>
        {
            string result = await CommandAsync("docker", ["run", "--pull=never", "-d", "--cidfile", cidFile, "--name", plannedName,
                "--label", "hexalith.sharding.owner=" + _runId, .. options, image, .. command]);
            if (simulateMissingCid)
            {
                File.Delete(cidFile);
                throw new InvalidOperationException("Injected partial-startup failure after missing-cid.");
            }

            return result;
        }, identity => RetainContainer(plannedName, identity, recovered: false));
    }

    private async Task StartNodeAsync(int index)
    {
        EnsureActive();
        OwnedNode node = _nodes[index];
        var environment = new Dictionary<string, string>
        {
            [RunBinding.BindingVariable] = _bindingPath,
            ["ASPNETCORE_URLS"] = "http://127.0.0.1:" + node.AppPort,
            ["ASPNETCORE_ENVIRONMENT"] = "Testing",
            ["DAPR_HTTP_PORT"] = node.HttpPort.ToString(CultureInfo.InvariantCulture),
            ["DAPR_GRPC_PORT"] = node.GrpcPort.ToString(CultureInfo.InvariantCulture),
            ["DAPR_HTTP_ENDPOINT"] = "http://127.0.0.1:" + node.HttpPort,
            ["DAPR_GRPC_ENDPOINT"] = "http://127.0.0.1:" + node.GrpcPort,
            ["DAPR_APP_ID"] = _runId,
            ["Logging__LogLevel__Default"] = "Warning",
        };
        node.Application = StartProcess("host-" + index, "dotnet", [Path.Combine(_hostDirectory, "Hexalith.EventStore.Sharding.TestHost.dll")], environment);
        node.Sidecar = StartProcess("sidecar-" + index, _daprd,
            ["--app-id", _runId, "--app-port", node.AppPort.ToString(CultureInfo.InvariantCulture), "--app-channel-address", "127.0.0.1",
                "--dapr-listen-addresses", "127.0.0.1", "--dapr-http-port", node.HttpPort.ToString(CultureInfo.InvariantCulture),
                "--dapr-grpc-port", node.GrpcPort.ToString(CultureInfo.InvariantCulture), "--dapr-internal-grpc-port", node.InternalPort.ToString(CultureInfo.InvariantCulture),
                "--dapr-internal-grpc-listen-address", "127.0.0.1", "--metrics-listen-address", "127.0.0.1",
                "--metrics-port", node.MetricsPort.ToString(CultureInfo.InvariantCulture), "--profile-port", node.ProfilePort.ToString(CultureInfo.InvariantCulture),
                "--placement-host-address", "127.0.0.1:" + _placementPort, "--scheduler-host-address", "127.0.0.1:" + _schedulerPort,
                "--resources-path", Path.Combine(_scratch, "components"), "--config", Path.Combine(_scratch, "discovery.yaml"), "--log-level", "warn"],
            new Dictionary<string, string> { ["NAMESPACE"] = _runId, ["DAPR_HOST_IP"] = "127.0.0.1", ["DAPR_APP_ID"] = _runId });
        await WaitHttpAsync("http://127.0.0.1:" + node.AppPort + "/alive", node);
        await WaitHttpAsync("http://127.0.0.1:" + node.HttpPort + "/v1.0/healthz", node);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_work.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            using JsonDocument metadata = JsonDocument.Parse(await _http.GetStringAsync("http://127.0.0.1:" + node.HttpPort + "/v1.0/metadata", deadline.Token));
            if (metadata.RootElement.TryGetProperty("actorRuntime", out JsonElement runtime)
                && runtime.TryGetProperty("runtimeStatus", out JsonElement status) && status.GetString() == "RUNNING"
                && runtime.TryGetProperty("hostReady", out JsonElement ready) && ready.ValueKind == JsonValueKind.True)
            {
                Record("node-ready", new { index, hostPid = node.Application.Id, sidecarPid = node.Sidecar.Id, metadata = metadata.RootElement.Clone() });
                return;
            }

            await Task.Delay(250, deadline.Token);
        }
    }

    private Process StartProcess(string name, string executable, string[] arguments, Dictionary<string, string> environment)
    {
        EnsureActive();
        var start = CreateStart(executable, arguments);
        foreach ((string key, string value) in environment) start.Environment[key] = value;
        var log = new Oq8BoundedLog();
        _logs[name + "-" + _processes.Count] = log;
        var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, line) => log.Append(line.Data);
        process.ErrorDataReceived += (_, line) => log.Append(line.Data);
        if (!process.Start()) throw new InvalidOperationException("Owned process did not start.");
        _processes.Add(process);
        WriteOwnershipJournal();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _commands.Add(Redact(executable + " " + string.Join(" ", arguments)));
        return process;
    }

    private Task<string> CommandAsync(string executable, string[] arguments, bool cleanup = false)
        => cleanup ? CommandCoreAsync(executable, arguments, cleanup: true)
            : _activities.Start(() => CommandCoreAsync(executable, arguments, cleanup: false));

    private async Task<string> CommandCoreAsync(string executable, string[] arguments, bool cleanup)
    {
        if (!cleanup) EnsureActive();
        if (executable == "docker")
        {
            if (_dockerEndpoint.Length != 0) arguments = ["--host", _dockerEndpoint, .. arguments];
            else if (arguments.Length < 2 || arguments[0] != "context" || arguments[1] is not ("show" or "inspect"))
                throw new InvalidOperationException("Docker commands require a bound local endpoint.");
        }

        using var timeout = cleanup ? new CancellationTokenSource() : CancellationTokenSource.CreateLinkedTokenSource(_work.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var process = new Process { StartInfo = CreateStart(executable, arguments) };
        _commands.Add(Redact(executable + " " + string.Join(" ", arguments)));
        if (!process.Start()) throw new InvalidOperationException("Fixture command did not start.");
        // Retain the exact handle even if cancellation cannot kill/drain this command.
        // Teardown retries surviving command processes alongside hosts and sidecars.
        _commandProcesses.Add(process);
        Task<string> output = BoundedCommandCapture.ReadAsync(process.StandardOutput, Oq8BoundedLog.MaximumCharacters, timeout.Token);
        Task<string> error = BoundedCommandCapture.ReadAsync(process.StandardError, Oq8BoundedLog.MaximumCharacters, timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            using var drain = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(drain.Token);
            try { await Task.WhenAll(output, error); }
            catch (OperationCanceledException) { }
            Record("command-canceled-and-drained", new { executable, arguments = arguments.Select(Redact).ToArray(), process.Id, process.HasExited });
            throw;
        }

        await Task.WhenAll(output, error);
        string stdout = output.Result;
        string stderr = error.Result;
        Record("command-result", new
        {
            executable, arguments = arguments.Select(Redact).ToArray(), process.ExitCode,
            stdout = Redact(stdout.Length <= Oq8BoundedLog.MaximumCharacters ? stdout : stdout[^Oq8BoundedLog.MaximumCharacters..]),
            stderr = Redact(stderr.Length <= Oq8BoundedLog.MaximumCharacters ? stderr : stderr[^Oq8BoundedLog.MaximumCharacters..]),
        });
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(Redact(executable + " exited " + process.ExitCode + ": " + stderr));
        }

        return executable == "docker" && arguments.Contains("logs", StringComparer.Ordinal) ? stdout + stderr : stdout;
    }

    private ProcessStartInfo CreateStart(string executable, string[] arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            WorkingDirectory = Directory.Exists(_scratch) ? _scratch : Environment.CurrentDirectory,
        };
        foreach (string key in new[] { "DAPR_HTTP_ENDPOINT", "DAPR_GRPC_ENDPOINT", "DAPR_API_TOKEN", "APP_API_TOKEN" }) start.Environment.Remove(key);
        if (executable == "docker")
        {
            foreach (string key in new[] { "DOCKER_CONTEXT", "DOCKER_HOST", "DOCKER_TLS_VERIFY", "DOCKER_CERT_PATH" }) start.Environment.Remove(key);
        }

        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        return start;
    }

    private Task<string> QueryAsync(string query)
        => CommandAsync("docker", ["exec", _postgres, "psql", "-U", "postgres", "-d", "sharding", "-At", "-c", query]);

    private async Task WaitPostgresAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_work.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();
            try
            {
                _ = await CommandAsync("docker", ["exec", _postgres, "pg_isready", "-h", "127.0.0.1", "-p", "5432", "-U", "postgres", "-d", "sharding"]);
                string result = await CommandAsync("docker", ["exec", "-e", "PGPASSWORD=" + _password, _postgres, "psql", "-h", "127.0.0.1", "-p", "5432",
                    "-U", "postgres", "-d", "sharding", "-At", "-c", "SELECT current_database() || ':' || 1;"]);
                if (result.Trim() == "sharding:1") return;
            }
            catch (InvalidOperationException) { }
            await Task.Delay(250, timeout.Token);
        }
    }

    private async Task WaitHttpAsync(string url, OwnedNode node)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_work.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();
            if (node.Application!.HasExited || node.Sidecar!.HasExited) throw new InvalidOperationException("An owned host/sidecar exited before readiness; retained logs identify the failure.");
            try
            {
                using HttpResponseMessage response = await _http.GetAsync(url, timeout.Token);
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            await Task.Delay(250, timeout.Token);
        }
    }

    private static async Task StopProcessAsync(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await process.WaitForExitAsync(timeout.Token);
        }

        if (!process.HasExited) throw new InvalidOperationException("Owned process remains alive after teardown.");
        // Flush asynchronous output events before retaining diagnostics.
        process.WaitForExit();
    }

    private static async Task StopNodeAsync(OwnedNode node)
    {
        if (node.Application is not null) await StopProcessAsync(node.Application);
        if (node.Sidecar is not null) await StopProcessAsync(node.Sidecar);
        node.Application = null;
        node.Sidecar = null;
    }

    private string CreateComponent() => $"""
        apiVersion: dapr.io/v1alpha1
        kind: Component
        metadata:
          name: sharding-state
        spec:
          type: state.postgresql
          version: v1
          metadata:
          - name: connectionString
            value: "host=127.0.0.1 port={_postgresPort} user=postgres password={_password} dbname=sharding sslmode=disable connect_timeout=5"
          - name: actorStateStore
            value: "true"
        scopes:
        - {_runId}
        """;

    private string CreateDiscovery() => $$"""
        apiVersion: dapr.io/v1alpha1
        kind: Configuration
        metadata:
          name: private-sharding-discovery
        spec:
          nameResolution:
            component: sqlite
            version: v1
            configuration:
              connectionString: {{JsonSerializer.Serialize(Path.Combine(_scratch, "discovery.sqlite"))}}
        """;

    private Dictionary<string, string> SourceManifest()
        => new[] { "Hexalith.EventStore.Sharding.TestHost", "Hexalith.EventStore.Sharding.Tests" }
            .SelectMany(name => Directory.GetFiles(Path.Combine(_root, "tests", name), "*", SearchOption.AllDirectories))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Concat(new[] { "Oq8OwnedContainerLaunch.cs", "Oq8BoundedLog.cs", "DockerPublishedPortResolver.cs" }
                .Select(name => Path.Combine(_root, "tests", "Hexalith.EventStore.Server.LiveSidecar.Tests", "Fixtures", name)))
            .Concat(new[]
            {
                Path.Combine(_root, "src", "Hexalith.EventStore.Testing", "Fakes", "InMemoryStateManager.cs"),
                Path.Combine(_root, "tests", "Hexalith.EventStore.Server.Tests", "TestUtilities", "ActorStateManagerTestHelper.cs"),
            })
            .Order(StringComparer.Ordinal).ToDictionary(path => Path.GetRelativePath(_root, path), RunBinding.HashFile, StringComparer.Ordinal);

    private int Port()
    {
        for (int attempt = 0; attempt < 128; attempt++)
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            if (_ports.Add(port)) return port;
        }

        throw new InvalidOperationException("Could not select an invocation-distinct loopback port.");
    }

    private void EnsureActive()
    {
        _work.Token.ThrowIfCancellationRequested();
        if (DateTimeOffset.UtcNow >= _expiry)
        {
            _work.Cancel();
            throw new OperationCanceledException("The local run has reached its exclusive expiry.", _work.Token);
        }

        if (_disposed || _fenced) throw new InvalidOperationException("The local run has completed or fenced new work.");
    }

    private void ArmExpiry()
    {
        TimeSpan remaining = _expiry - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            _expiryTriggered = true;
            _work.Cancel();
        }
        else _work.CancelAfter(remaining);
        _watchdog = WatchExpiryAsync();
    }

    private async Task<string> ResolveDockerEndpointAsync()
        => await DockerEndpointPolicy.ResolveAsync(null, null, Environment.GetEnvironmentVariable, async context =>
        {
            context ??= (await CommandAsync("docker", ["context", "show"])).Trim();
            return await CommandAsync("docker", ["context", "inspect", context, "--format", "{{.Endpoints.docker.Host}}"]);
        });

    private async Task ObserveControlPlaneVersionAsync(string service, string container, string image)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_work.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            string logs = await CommandAsync("docker", ["logs", "--tail", "100", container]);
            Match version = Regex.Match(logs, @"\bver=([^\s""']+)", RegexOptions.CultureInvariant);
            if (version.Success)
            {
                Record(service + "-runtime", new { version = version.Groups[1].Value, image, startupLogs = Redact(logs) });
                return;
            }

            await Task.Delay(200, deadline.Token);
        }
    }

    private void RetainContainer(string name, string identity, bool recovered, bool persist = true)
    {
        if (!_containers.Contains(identity, StringComparer.Ordinal))
        {
            _containers.Add(identity);
            if (recovered) _recoveredContainerCount++;
        }

        _containerPlans[name] = identity;
        Record(recovered ? "recovered-container-ownership" : "container-ownership", new { name, identity });
        if (persist) WriteOwnershipJournal();
    }

    private async Task ReconcileContainersAsync(List<string> diagnosticFailures)
    {
        foreach (string name in _containerPlans.Keys.ToArray())
        {
            string matches = await CommandAsync("docker", ["ps", "-aq", "--no-trunc", "--filter", "name=^/" + name + "$",
                "--filter", "label=hexalith.sharding.owner=" + _runId], cleanup: true);
            foreach (string identity in matches.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string verified = (await CommandAsync("docker", ["inspect", "--format",
                    "{{.Id}}|{{index .Config.Labels \"hexalith.sharding.owner\"}}|{{.Name}}", identity], cleanup: true)).Trim();
                if (identity.Length != 64 || !identity.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')
                    || verified != identity + "|" + _runId + "|/" + name)
                    throw new InvalidOperationException("Recovered container does not match the exact planned identity and ownership label.");
                RetainContainer(name, identity, recovered: !_containers.Contains(identity, StringComparer.Ordinal), persist: false);
                TryDiagnostic(WriteOwnershipJournal, diagnosticFailures);
            }
        }
    }

    private void WriteOwnershipJournal()
    {
        if (_artifacts.Length == 0) return;
        string target = Path.Combine(_artifacts, "ownership.json");
        string temporary = target + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(new
        {
            runId = _runId, atUtc = Canonical(DateTimeOffset.UtcNow), dockerEndpoint = _dockerEndpoint,
            label = "hexalith.sharding.owner=" + _runId,
            containers = _containerPlans.Select(plan => new { name = plan.Key, id = plan.Value }),
            processes = _processes.Select(process => new { process.Id, startedUtc = process.StartTime.ToUniversalTime(),
                executable = Path.GetFileName(process.StartInfo.FileName), exited = process.HasExited }),
            cleanupFailureHistory = _cleanupHistory,
        }, JsonOptions));
        File.Move(temporary, target, overwrite: true);
    }

    private async Task DrainActivitiesAsync(List<string> failures)
    {
        Task[] tasks = _activities.Fence();
        try
        {
            await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(35));
        }
        catch (Exception exception)
        {
            // Completed cancellation/faults are observed. Only undrained work blocks a clean receipt.
            if (tasks.Any(task => !task.IsCompleted)) failures.Add("outstanding operations did not drain: " + exception.GetType().Name);
        }

        Record("work-drained", new { count = tasks.Length, completed = tasks.Count(task => task.IsCompleted),
            canceled = tasks.Count(task => task.IsCanceled), faulted = tasks.Count(task => task.IsFaulted) });
    }

    private static void TryDiagnostic(Action write, List<string> failures)
    {
        try { write(); }
        catch (Exception exception) { failures.Add("diagnostic write: " + exception.GetType().Name + ": " + exception.Message); }
    }

    private void Record(string kind, object value)
    {
        lock (_observations) _observations.Add(new { atUtc = Canonical(DateTimeOffset.UtcNow), kind, value });
    }

    private string Redact(string value)
    {
        if (_password.Length != 0) value = value.Replace(_password, "<redacted-password>", StringComparison.Ordinal);
        if (_scratch.Length != 0) value = value.Replace(_scratch, "<owned-scratch>", StringComparison.Ordinal);
        return value;
    }

    private static void InjectFailure(string? configured, string current)
    {
        if (configured == current) throw new InvalidOperationException("Injected partial-startup failure after " + current + ".");
    }

    private static string Canonical(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string FindRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx"))) return directory.FullName;
        }

        throw new InvalidOperationException("Cannot find the owning repository root.");
    }
}
