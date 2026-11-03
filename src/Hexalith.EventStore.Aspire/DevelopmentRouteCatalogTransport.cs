using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hexalith.EventStore.Aspire;

/// <summary>Owns temporary Development trust and authenticated native Sentry, placement and scheduler processes.</summary>
/// <remarks>Dispose after every enrolled application and sidecar. Failed cleanup retains the private directory for recovery.</remarks>
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
public sealed class DevelopmentRouteCatalogTransport : IAsyncDisposable
{
    /// <summary>The existing qualified Dapr 1.18.7 native runtime checksum.</summary>
    public const string RuntimeSha256 = "e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488";
    /// <summary>The existing immutable Sentry 1.18.7 image.</summary>
    public const string SentryImage = "ghcr.io/dapr/sentry@sha256:b9fc166085289661f516150dc1c47ac3495b9e6d1e666079cb71cd2374ca7256";
    /// <summary>The existing immutable placement 1.18.7 image.</summary>
    public const string PlacementImage = "ghcr.io/dapr/placement@sha256:c9da106eddcbb625c94baea31af4ed576d32750741b442c99f51518c420e76cc";
    /// <summary>The existing immutable scheduler 1.18.7 image.</summary>
    public const string SchedulerImage = "ghcr.io/dapr/scheduler@sha256:2cc2da2f7c4ece604041e6a420de04a297afb24cea8b3c9394bd0e126a334444";
    private readonly List<DevelopmentRouteCatalogTransportProcess> _processes = [];
    private readonly Dictionary<int, string> _sidecars = [];
    private readonly Dictionary<string, string> _credentialHashes = new(StringComparer.Ordinal);
    private readonly string _custody;
    private readonly string _runtime;
    private readonly Action<DevelopmentRouteCatalogTransport, string>? _startupCheckpoint;
    private bool _disposed;
    private bool _ready;

    private DevelopmentRouteCatalogTransport(string directory, DevelopmentRouteCatalogTransportOptions options,
        Action<DevelopmentRouteCatalogTransport, string>? startupCheckpoint)
    {
        Directory = directory;
        _custody = Path.GetFullPath(options.CustodyDirectory);
        _runtime = Path.GetFullPath(options.RuntimeExecutable);
        _startupCheckpoint = startupCheckpoint;
        SentryAddress = SelectAddress(null);
        PlacementAddress = SelectAddress(options.PlacementAddress);
        SchedulerAddress = SelectAddress(options.SchedulerAddress);
        if (PlacementAddress == SchedulerAddress) throw new InvalidOperationException("Owned control-plane endpoints must be distinct.");
    }

    /// <summary>The private temporary directory; it is preserved if custody or shutdown cannot be proved.</summary>
    public string Directory { get; }
    /// <summary>The private runtime directory selected by the Dapr CLI, containing only the SDK's native launch adapter.</summary>
    public string RuntimeDirectory => Path.Combine(Directory, "runtime");
    private string RuntimeAdapterPath => Path.Combine(RuntimeDirectory, ".dapr", "bin", "daprd");
    /// <summary>The owned loopback Sentry endpoint.</summary>
    public string SentryAddress { get; private set; } = "";
    /// <summary>The owned authenticated loopback placement endpoint.</summary>
    public string PlacementAddress { get; }
    /// <summary>The owned authenticated loopback scheduler endpoint.</summary>
    public string SchedulerAddress { get; }
    /// <summary>The trust-root fingerprint; this contains no credential bytes.</summary>
    public string TrustAnchorSha256 { get; private set; } = "";

    /// <summary>Supplies time only for internal unchanged-credential validity tests.</summary>
    internal TimeProvider CredentialClock { get; set; } = TimeProvider.System;

    /// <summary>Starts an explicit Development-only owned transport. Images must already exist; global Dapr trust is never read.</summary>
    public static Task<DevelopmentRouteCatalogTransport> StartAsync(DevelopmentRouteCatalogTransportOptions options,
        string environmentName, CancellationToken cancellationToken = default)
        => StartAsync(options, environmentName, null, cancellationToken);

    /// <summary>Exposes deterministic startup observation/failure points only to owning tests.</summary>
    internal static async Task<DevelopmentRouteCatalogTransport> StartAsync(DevelopmentRouteCatalogTransportOptions options,
        string environmentName, Action<DevelopmentRouteCatalogTransport, string>? startupCheckpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!OperatingSystem.IsLinux() || environmentName != "Development")
            throw new InvalidOperationException("Development transport requires Linux, Development and the checksum-pinned native runtime.");
        await ValidatePrerequisitesAsync(options.RuntimeExecutable, "/usr/bin/python3", cancellationToken).ConfigureAwait(false);
        options = options with { PlacementAddress = SelectAddress(options.PlacementAddress), SchedulerAddress = SelectAddress(options.SchedulerAddress) };
        if (options.PlacementAddress == options.SchedulerAddress) throw new InvalidOperationException("Owned control-plane endpoints must be distinct.");
        string custody = Path.GetFullPath(options.CustodyDirectory);
        if (!Path.IsPathFullyQualified(options.CustodyDirectory) || System.IO.Directory.Exists(custody) || File.Exists(custody))
            throw new InvalidOperationException("Development transport requires new private custody.");
        for (DirectoryInfo? parent = new(Path.GetDirectoryName(custody)!); parent is not null; parent = parent.Parent)
            if (parent.LinkTarget is not null || System.IO.Directory.Exists(Path.Combine(parent.FullName, ".git")) || File.Exists(Path.Combine(parent.FullName, ".git")))
                throw new InvalidOperationException("Development transport custody must be outside checkouts and links.");
        System.IO.Directory.CreateDirectory(custody, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        string directory = System.IO.Directory.CreateTempSubdirectory("eventstore-development-transport-").FullName;
        File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        DevelopmentRouteCatalogTransport? transport = null;
        try
        {
            transport = new(directory, options, startupCheckpoint);
            WritePrivate(Path.Combine(custody, "startup.json"), JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1, directory, runtimeExecutable = transport._runtime, runtimeSha256 = RuntimeSha256,
                sentryAddress = transport.SentryAddress, placementAddress = transport.PlacementAddress,
                schedulerAddress = transport.SchedulerAddress, environment = "Development", trustDomain = "public", @namespace = "default",
            }));
            await transport.StartCoreAsync(cancellationToken).ConfigureAwait(false);
            return transport;
        }
        catch
        {
            if (transport is not null) await transport.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException("Owned Development transport startup refused; inspect its private custody receipt.");
        }
    }

    /// <summary>Configures an exact native sidecar for the owned trust and control plane, preserving application-channel credentials.</summary>
    public void ConfigureSidecar(ProcessStartInfo start)
    {
        ArgumentNullException.ThrowIfNull(start);
        RequireReady();
        if (Path.GetFullPath(start.FileName) != _runtime || Hash(start.FileName) != RuntimeSha256)
            throw new InvalidOperationException("Development transport refuses an unpinned sidecar executable.");
        SetFlag(start, "--enable-mtls", null);
        SetFlag(start, "--sentry-address", SentryAddress);
        SetFlag(start, "--control-plane-trust-domain", "public");
        SetFlag(start, "--control-plane-namespace", "default");
        SetFlag(start, "--placement-host-address", PlacementAddress);
        SetFlag(start, "--scheduler-host-address", SchedulerAddress);
        foreach (var pair in ReadSidecarEnvironment()) start.Environment[pair.Key] = pair.Value;
    }

    /// <summary>Registers one native sidecar for shutdown verification without transferring its application lifecycle.</summary>
    public void ObserveSidecar(Process process)
        => ObserveSidecar(process, ProcessStart);

    /// <summary>Allows owning tests to reproduce disappearance between executable inspection and kernel identity capture.</summary>
    internal void ObserveSidecar(Process process, Func<int, string?> observeStart)
    {
        ArgumentNullException.ThrowIfNull(process);
        RequireReady();
        if (process.HasExited || Path.GetFullPath(process.MainModule!.FileName) != _runtime)
            throw new InvalidOperationException("Development transport requires the actual enrolled native sidecar.");
        string identity = observeStart(process.Id)
            ?? throw new InvalidOperationException("The native sidecar vanished before its exact identity could be captured.");
        _sidecars[process.Id] = identity;
    }

    /// <summary>Returns owned trust to the native sidecar environment only. Never log this dictionary.</summary>
    internal IReadOnlyDictionary<string, string> ReadSidecarEnvironment()
    {
        RequireReady();
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DAPR_TRUST_ANCHORS"] = File.ReadAllText(Credential("ca.crt")),
            ["DAPR_CERT_CHAIN"] = File.ReadAllText(Credential("issuer.crt")),
            ["DAPR_CERT_KEY"] = File.ReadAllText(Credential("issuer.key")),
            ["NAMESPACE"] = "default",
        };
    }

    /// <summary>Retains a configuration with mTLS enabled, without changing any ACL, scope or grant.</summary>
    internal string ConfigureReceiver(string configuration, string name)
    {
        RequireReady();
        JsonObject value = DevelopmentRouteCatalogAspireExtensions.ReadYamlOrJson(configuration);
        if (value["spec"]?["accessControl"]?["defaultAction"]?.GetValue<string>() != "deny"
            || value["spec"]?["accessControl"]?["trustDomain"]?.GetValue<string>() != "public")
            throw new InvalidOperationException("Owned Development transport requires the existing public-trust default-deny receiver.");
        value["spec"]!["mtls"] = new JsonObject { ["enabled"] = true, ["workloadCertTTL"] = "1h", ["allowedClockSkew"] = "15s" };
        string path = Path.Combine(Directory, name + ".transport.json");
        WritePrivate(path, JsonSerializer.SerializeToUtf8Bytes(value));
        return path;
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        var docker = await DevelopmentRouteCatalogTransportDocker.CaptureAsync(cancellationToken).ConfigureAwait(false);
        foreach (var (name, image) in new[] { ("sentry", SentryImage), ("placement", PlacementImage), ("scheduler", SchedulerImage) })
            await docker.ExtractAsync(image, name, Directory, cancellationToken).ConfigureAwait(false);
        System.IO.Directory.CreateDirectory(Path.Combine(Directory, "credentials"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        string configuration = Path.Combine(Directory, "sentry.json");
        WritePrivate(configuration, JsonSerializer.SerializeToUtf8Bytes(new { apiVersion = "dapr.io/v1alpha1", kind = "Configuration",
            metadata = new { name = "owned-development-sentry", @namespace = "default" },
            spec = new { mtls = new { enabled = true, workloadCertTTL = "1h", allowedClockSkew = "15s" } } }));
        int sentryHealth = Port();
        await StartServiceAsync("sentry", ["--config", configuration, "--issuer-credentials", Path.Combine(Directory, "credentials"),
            "--trust-domain", "public", "--listen-address", "127.0.0.1", "--port", SentryAddress.Split(':')[1]], sentryHealth, false, cancellationToken).ConfigureAwait(false);
        foreach (string name in new[] { "ca.crt", "issuer.crt", "issuer.key" })
        {
            File.SetUnixFileMode(Credential(name), UnixFileMode.UserRead | UnixFileMode.UserWrite);
            _credentialHashes[name] = Hash(Credential(name));
        }
        TrustAnchorSha256 = Hash(Credential("ca.crt"));
        int raft = Port();
        await StartServiceAsync("placement", ["--mode=standalone", "--tls-enabled=true", "--inmem-store-enabled=true",
            "--id=owned-placement", "--initial-cluster=owned-placement=127.0.0.1:" + raft,
            "--listen-address=127.0.0.1", "--port=" + PlacementAddress.Split(':')[1]], Port(), true, cancellationToken).ConfigureAwait(false);
        int etcdClient = Port(), etcdPeer = Port();
        await StartServiceAsync("scheduler", ["--mode=standalone", "--tls-enabled=true", "--listen-address=127.0.0.1", "--port=" + SchedulerAddress.Split(':')[1],
            "--override-broadcast-host-port=" + SchedulerAddress, "--etcd-data-dir=" + Path.Combine(Directory, "scheduler-data"),
            "--etcd-client-listen-address=127.0.0.1", "--etcd-client-port=" + etcdClient,
            "--id=dapr-scheduler-server-0", "--etcd-initial-cluster=dapr-scheduler-server-0=http://127.0.0.1:" + etcdPeer,
            "--identity-directory-write=" + Path.Combine(Directory, "scheduler-identity")], Port(), true, cancellationToken).ConfigureAwait(false);
        ValidateReadyInputs();
        WriteRuntimeAdapter();
        _startupCheckpoint?.Invoke(this, "publish-ready");
        cancellationToken.ThrowIfCancellationRequested();
        WritePrivate(Path.Combine(_custody, "ready.json"), JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1, ready = true, environment = "Development", trustDomain = "public", @namespace = "default",
            directory = Directory, runtimeSha256 = RuntimeSha256, trustAnchorSha256 = TrustAnchorSha256,
            sentryAddress = SentryAddress, placementAddress = PlacementAddress, schedulerAddress = SchedulerAddress,
            images = new { sentry = SentryImage, placement = PlacementImage, scheduler = SchedulerImage },
            processes = _processes.Select(process => new { pid = process.Id, startTimeUtc = process.StartTime, start = process.NativeStart }).ToArray(),
            binaries = new[] { "sentry", "placement", "scheduler" }.ToDictionary(name => name, name => Hash(Path.Combine(Directory, name))),
            adapterSha256 = Hash(RuntimeAdapterPath),
        }));
        _ready = true;
    }

    private async Task StartServiceAsync(string name, string[] arguments, int healthPort, bool trust, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(Path.Combine(Directory, name)) { WorkingDirectory = Directory };
        // These services never inherit a global certificate, token, namespace or scheduler identity path.
        foreach (string variable in start.Environment.Keys.Where(key => key.StartsWith("DAPR_", StringComparison.Ordinal) || key is "NAMESPACE" or "APP_API_TOKEN").ToArray())
            start.Environment.Remove(variable);
        start.Environment["NAMESPACE"] = "default";
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        foreach (string argument in new[] { "--healthz-listen-address", "127.0.0.1", "--healthz-port", healthPort.ToString(), "--enable-metrics=false", "--log-level", "warn" }) start.ArgumentList.Add(argument);
        if (trust)
            foreach (string argument in new[] { "--trust-domain", "public", "--trust-anchors-file", Credential("ca.crt"), "--sentry-address", SentryAddress }) start.ArgumentList.Add(argument);
        WritePrivate(Path.Combine(_custody, name + ".launch.json"), JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1, directory = Directory, executable = start.FileName,
            executableSha256 = Hash(start.FileName), arguments = start.ArgumentList.ToArray(),
        }));
        var process = new DevelopmentRouteCatalogTransportProcess(start, Path.Combine(Directory, name + ".log"),
            Path.Combine(_custody, name));
        _processes.Add(process);
        _startupCheckpoint?.Invoke(this, name + "-started");
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        for (int attempt = 0; attempt < 150; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            process.RequireRunning();
            try
            {
                using var response = await client.GetAsync($"http://127.0.0.1:{healthPort}/healthz", cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
        throw new InvalidOperationException("Owned Development control-plane readiness was not established.");
    }

    private void WriteRuntimeAdapter()
    {
        System.IO.Directory.CreateDirectory(RuntimeDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(RuntimeAdapterPath)!, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        // Dapr CLI 1.18.2 RunConfig.getArgs/mtlsEndpoint hardcodes localhost:50001.
        // Replace exactly that one proven CLI-generated native flag; all other arguments retain their bytes/order.
        string script = "#!/usr/bin/python3\nimport hashlib, os, sys\n"
            + "runtime = " + JsonSerializer.Serialize(_runtime) + "\n"
            + "target = " + JsonSerializer.Serialize(SentryAddress) + "\n"
            + "args = sys.argv[1:]\n"
            + "def refuse():\n    sys.exit('Owned Development sidecar identity is incomplete; launch refused')\n"
            + "if hashlib.sha256(open(runtime, 'rb').read()).hexdigest() != '" + RuntimeSha256 + "': refuse()\n"
            + "if args == ['--version']: os.execv(runtime, [runtime] + args)\n"
            + "if args.count('--sentry-address') != 1 or args.count('--enable-mtls') != 1: refuse()\n"
            + "index = args.index('--sentry-address')\n"
            + "if index + 1 >= len(args) or args[index + 1] != 'localhost:50001': refuse()\n"
            + "if any(x.startswith(('--enable-mtls=', '--sentry-address=', '--control-plane-trust-domain', '--control-plane-namespace')) for x in args): refuse()\n"
            + "if os.environ.get('NAMESPACE') != 'default' or any(not os.environ.get(x) for x in ('DAPR_TRUST_ANCHORS','DAPR_CERT_CHAIN','DAPR_CERT_KEY')): refuse()\n"
            + "if hashlib.sha256(os.environ['DAPR_TRUST_ANCHORS'].encode()).hexdigest() != '" + TrustAnchorSha256 + "': refuse()\n"
            + "for flag, expected in " + JsonSerializer.Serialize(new[] { new[] { "--placement-host-address", PlacementAddress }, new[] { "--scheduler-host-address", SchedulerAddress } }) + ":\n"
            + "    if args.count(flag) != 1 or any(x.startswith(flag + '=') for x in args): refuse()\n"
            + "    pos = args.index(flag)\n    if pos + 1 >= len(args) or args[pos + 1] != expected: refuse()\n"
            + "for pid, expected in " + JsonSerializer.Serialize(_processes.Select(process => new[] { process.Id.ToString(), ProcessStart(process.Id)! }).ToArray()) + ":\n"
            + "    try: fields = open('/proc/' + pid + '/stat').read().rsplit(')', 1)[1].split()\n    except OSError: refuse()\n"
            + "    if fields[0] == 'Z' or fields[19] != expected: refuse()\n"
            + "args[index + 1] = target\n"
            + "pid = os.getpid()\n"
            + "stat = open('/proc/' + str(pid) + '/stat').read().rsplit(')', 1)[1].split()[19]\n"
            + "path = " + JsonSerializer.Serialize(Path.Combine(Directory, "native-")) + " + str(pid) + '.json'\n"
            + "import json\n"
            + "with os.fdopen(os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600), 'w') as receipt: json.dump({'pid':pid, 'start':stat}, receipt)\n"
            + "os.execv(runtime, [runtime] + args + ['--control-plane-trust-domain', 'public', '--control-plane-namespace', 'default'])\n";
        WritePrivate(RuntimeAdapterPath, System.Text.Encoding.UTF8.GetBytes(script));
        File.SetUnixFileMode(RuntimeAdapterPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private void RequireReady()
    {
        if (!_ready || _disposed) throw new InvalidOperationException("Owned Development transport is not ready.");
        ValidateReadyInputs();
    }

    private void ValidateReadyInputs()
    {
        foreach (var process in _processes) process.RequireRunning();
        foreach (string name in new[] { "ca.crt", "issuer.crt", "issuer.key" })
            if (!File.Exists(Credential(name)) || new FileInfo(Credential(name)).LinkTarget is not null
                || (File.GetUnixFileMode(Credential(name)) & (UnixFileMode)0x3f) != 0 || Hash(Credential(name)) != _credentialHashes[name])
                throw new InvalidOperationException("Owned Development trust custody is incomplete.");
        if (Hash(Credential("ca.crt")) != TrustAnchorSha256) throw new InvalidOperationException("Owned Development trust changed.");
        using var issuer = X509Certificate2.CreateFromPem(File.ReadAllText(Credential("issuer.crt")));
        DateTime now = CredentialClock.GetUtcNow().UtcDateTime;
        if (issuer.NotAfter.ToUniversalTime() <= now || issuer.NotBefore.ToUniversalTime() > now)
            throw new InvalidOperationException("Owned Development trust is expired or unavailable.");
    }

    /// <summary>Stops exact owned control processes after sidecars exit, writes a cleanup receipt, and removes temporary trust only on complete custody.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        foreach (string path in System.IO.Directory.Exists(Directory) ? System.IO.Directory.EnumerateFiles(Directory, "native-*.json") : [])
        {
            using var receipt = JsonDocument.Parse(File.ReadAllBytes(path));
            _sidecars[receipt.RootElement.GetProperty("pid").GetInt32()] = receipt.RootElement.GetProperty("start").GetString()!;
        }
        if (_sidecars.Any(process => ProcessStart(process.Key) == process.Value))
        {
            RetainCleanup(false, false, false);
            throw new InvalidOperationException("Owned native sidecars still run; retaining Development trust and control plane.");
        }
        bool stopped = true;
        foreach (var process in _processes.AsEnumerable().Reverse())
            try { await process.DisposeAsync().ConfigureAwait(false); }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or OperationCanceledException or System.ComponentModel.Win32Exception) { stopped = false; }
        bool removed = false;
        bool processesExited = stopped;
        if (stopped && _ready)
        {
            try
            {
                if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, recursive: true);
                removed = !System.IO.Directory.Exists(Directory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { stopped = false; }
        }
        RetainCleanup(stopped && _ready && removed, processesExited, removed);
        _disposed = stopped;
        if (!stopped) throw new InvalidOperationException("Owned transport shutdown is incomplete; private trust was retained.");
    }

    private void RetainCleanup(bool complete, bool processesExited, bool removed)
    {
        byte[] receiptBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1, complete, startupQualified = _ready,
            ownedProcessesExited = processesExited, trustAnchorSha256 = TrustAnchorSha256,
            temporaryDirectoryRemoved = removed,
            directory = Directory,
            processes = _processes.Select(process => new { pid = process.Id, start = process.NativeStart }).ToArray(),
        });
        string attempt = Path.Combine(_custody, "cleanup-attempt-" + Guid.NewGuid().ToString("N") + ".json");
        WritePrivate(attempt, receiptBytes);
        string latest = Path.Combine(_custody, "cleanup-next-" + Guid.NewGuid().ToString("N") + ".json");
        WritePrivate(latest, receiptBytes);
        File.Move(latest, Path.Combine(_custody, "cleanup.json"), overwrite: true);
    }

    private string Credential(string name) => Path.Combine(Directory, "credentials", name);
    private static string? ProcessStart(int pid)
    {
        try
        {
            string stat = File.ReadAllText("/proc/" + pid + "/stat");
            string[] fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return fields[0] == "Z" ? null : fields[19];
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }
    private static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    private static int Port()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
    private static string SelectAddress(string? selected)
    {
        if (selected is null) return "127.0.0.1:" + Port();
        if (!IPEndPoint.TryParse(selected, out var endpoint) || !endpoint.Address.Equals(IPAddress.Loopback)
            || selected != "127.0.0.1:" + endpoint.Port || endpoint.Port is < 1024 or 50005 or 50006 or 6050 or 6060)
            throw new InvalidOperationException("Owned control-plane addresses must select distinct unused private loopback ports.");
        return selected;
    }
    private static void SetFlag(ProcessStartInfo start, string flag, string? value)
    {
        if (start.ArgumentList.Any(argument => argument.StartsWith(flag + "=", StringComparison.Ordinal))
            || start.ArgumentList.Count(argument => argument == flag) > 1)
            throw new InvalidOperationException("Alternate or duplicate native transport flags refuse enrollment.");
        int index = start.ArgumentList.IndexOf(flag);
        if (index >= 0)
        {
            if (value is not null)
            {
                if (index + 1 >= start.ArgumentList.Count || string.IsNullOrEmpty(start.ArgumentList[index + 1])
                    || start.ArgumentList[index + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new InvalidOperationException("Missing native transport flag values refuse enrollment.");
                start.ArgumentList[index + 1] = value;
            }
        }
        else { start.ArgumentList.Add(flag); if (value is not null) start.ArgumentList.Add(value); }
    }
    /// <summary>Persists one new owner-only durable receipt, including pre-readiness native process custody.</summary>
    internal static void WritePrivate(string path, byte[] value)
    {
        using var stream = new FileStream(path, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write,
            Share = FileShare.None, UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite });
        stream.Write(value); stream.Flush(flushToDisk: true);
    }

    /// <summary>Refuses noncanonical native paths or an unavailable Python 3 adapter prerequisite before creating custody/resources.</summary>
    internal static async Task ValidatePrerequisitesAsync(string runtime, string interpreter, CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(runtime) || Path.GetFullPath(runtime) != runtime || !File.Exists(runtime))
            throw new InvalidOperationException("Development transport requires a canonical native runtime executable.");
        for (FileSystemInfo? path = new FileInfo(runtime); path is not null;
            path = path is FileInfo file ? file.Directory : ((DirectoryInfo)path).Parent)
            if (path.LinkTarget is not null || (path.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Development transport refuses linked native runtime paths.");
        if (Hash(runtime) != RuntimeSha256 || (File.GetUnixFileMode(runtime) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
            throw new InvalidOperationException("Development transport requires the checksum-pinned executable runtime.");
        if (!File.Exists(interpreter) || (File.GetUnixFileMode(interpreter) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0)
            throw new InvalidOperationException("Development transport requires executable Python 3 at /usr/bin/python3.");
        var start = new ProcessStartInfo(interpreter) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-c"); start.ArgumentList.Add("import sys; sys.exit(0 if sys.version_info.major == 3 else 1)");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("The owned adapter Python 3 prerequisite is unavailable.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            if (process.ExitCode != 0) throw new InvalidOperationException("The owned adapter requires Python 3 at /usr/bin/python3.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync(cleanup.Token).ConfigureAwait(false);
            }
        }
    }
}
