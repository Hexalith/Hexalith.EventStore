// Approved Dapr Kubernetes cryptography supplement: 14eb081c36c1122b337c75ee1d341afc7db0a01ea107ed83596817ddcfd33881; section 8.
// Profile authority: 29ec85464835a52d3e4548826b1a5495ceb85e70007e1cbefdea042f852d8c44.
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Microsoft.Extensions.Configuration;

namespace Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests;

/// <summary>Owns one disposable kind cluster, lock-verified daprd 1.19.1, PostgreSQL v2 state and runtime-generated RSA-3072 Secrets.</summary>
/// <remarks>
/// Nothing is allocated until an explicit Kubernetes lane test calls <see cref="StartAsync"/>, so other lanes never touch Docker or kind.
/// The process's ambient kube context is never read: kind writes only the lane kubeconfig, kubectl runs inside the owned node, and the
/// component reads ServiceAccount kubeconfigs from the lane directory. Key material is generated per run and is never written to disk.
/// The sidecar requires a per-run API token, which the state client and the crypto transport send; once started, the fixture records
/// every unobserved task exception by type name only.
/// </remarks>
public sealed class KubernetesCryptoFixture : IAsyncLifetime
{
    internal const string KindVersion = "v0.31.0";
    internal const string NodeImage = "kindest/node:v1.35.0@sha256:452d707d4862f52530247495d180205e029056831160e22870e37e3f6c1ac31f";
    internal const string Component = "kubernetes-crypto";
    internal const string DeniedComponent = "kubernetes-crypto-denied";
    private const string DataKey = "key.pem";
    // Exception.Data key that marks the listener's positive control; no message text is ever recorded.
    private const string UnobservedControl = "pp-k8s-unobserved-control";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _identity = "pp-lifecycle-" + Guid.NewGuid().ToString("N");
    private readonly string _cluster = "pp-k8s-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
    private readonly ConcurrentQueue<string> _sidecarLog = new();
    private readonly ConcurrentQueue<string> _unobserved = new();
    private readonly string _apiToken = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    private OwnedPostgresContainer? _postgres;
    private Process? _sidecar;
    private string? _directory;
    private string? _daprd;
    private int[] _ports = [];
    private bool _clusterAttempted;
    private bool _started;
    private bool _listening;
    private int _controls;

    /// <summary>Creates resource-free identities only.</summary>
    public KubernetesCryptoFixture()
    {
        string suffix = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4));
        Namespace = "pp-keys-" + suffix;
        MaximumNamespace = ("pp-max-" + suffix + "-").PadRight(62, 'n') + "x";
        string maximumSecret = ("kek-max-" + suffix + "-").PadRight(127, 's') + "x";
        V1 = Address(Namespace, "kek-v1");
        V2 = Address(Namespace, "kek-v2");
        Alias = Address(Namespace, "kek-alias");
        Other = Address(Namespace, "kek-other");
        Ungranted = Address(Namespace, "kek-ungranted");
        Missing = Address(Namespace, "kek-missing");
        Wrap = Address(Namespace, "kek-wrap");
        RotationV1 = Address(Namespace, "kek-rotation-v1");
        RotationV2 = Address(Namespace, "kek-rotation-v2");
        Erasure = Address(Namespace, "kek-erasure");
        Maximum = MaximumNamespace + "/" + maximumSecret + "/" + "key-".PadRight(63, 'k');
    }

    /// <summary>Gets the lane namespace.</summary>
    internal string Namespace { get; }
    /// <summary>Gets the 63-character namespace that holds the maximum-length address.</summary>
    internal string MaximumNamespace { get; }
    /// <summary>Gets the first KEK version address.</summary>
    internal string V1 { get; }
    /// <summary>Gets the second KEK version address.</summary>
    internal string V2 { get; }
    /// <summary>Gets a test-only Secret holding exactly the V1 key bytes under another name.</summary>
    internal string Alias { get; }
    /// <summary>Gets a granted address holding an unrelated key.</summary>
    internal string Other { get; }
    /// <summary>Gets an existing Secret that the restricted ServiceAccount may not read.</summary>
    internal string Ungranted { get; }
    /// <summary>Gets a granted name for which no Secret exists.</summary>
    internal string Missing { get; }
    /// <summary>Gets a KEK used only by the wrap case, so its inventory count is exact.</summary>
    internal string Wrap { get; }
    /// <summary>Gets the rotation case's first KEK; a KEK inventory spans every scope that uses the address.</summary>
    internal string RotationV1 { get; }
    /// <summary>Gets the rotation case's second KEK.</summary>
    internal string RotationV2 { get; }
    /// <summary>Gets a KEK used only by the erasure case.</summary>
    internal string Erasure { get; }
    /// <summary>Gets the 256-character maximum address.</summary>
    internal string Maximum { get; }
    /// <summary>Gets the current SDK client to the owned sidecar, used for state and test-only legacy envelopes.</summary>
    internal DaprClient Client { get; private set; } = null!;
    /// <summary>Gets the current generated-client crypto transport, created as production creates it, with the per-run API token.</summary>
    internal DaprCryptographyGrpcTransport Transport { get; private set; } = null!;
    /// <summary>Gets the qualified-on-demand state primitive bound to the Dapr Kubernetes authority.</summary>
    internal DaprPayloadProtectionStateStore Store { get; private set; } = null!;
    /// <summary>Gets the lane-owned admin kubeconfig path.</summary>
    internal string AdminKubeconfig => Path.Combine(_directory!, "admin.kubeconfig");
    /// <summary>Gets the restricted ServiceAccount kubeconfig path.</summary>
    internal string RestrictedKubeconfig => Path.Combine(_directory!, "restricted.kubeconfig");
    /// <summary>Gets the API server endpoint the lane kubeconfig targets.</summary>
    internal string ApiServer { get; private set; } = "";
    /// <summary>Gets the owned node container's published API endpoint.</summary>
    internal string OwnedNodeEndpoint { get; private set; } = "";

    /// <inheritdoc/>
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>Allocates the owned lane once; a failed start is cleaned up and never retried within this fixture.</summary>
    internal async ValueTask StartAsync(CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            if (_started) { return; }
            if (_directory is not null) { throw new InvalidOperationException("The Kubernetes cryptography lane failed to start earlier."); }
            if (!_listening) { TaskScheduler.UnobservedTaskException += OnUnobservedTaskException; _listening = true; }
            _directory = Path.Combine(Path.GetTempPath(), "pp-k8s-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            await VerifyToolsAsync(token);
            await CreateClusterAsync(token);
            await ProvisionKeysAsync(token);
            _ = await RunAsync("docker", ["image", "inspect", "--format", "{{.Id}}", PayloadProtectionDaprFixture.PostgresImage], token: token);
            string password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            _postgres = new OwnedPostgresContainer(_identity);
            int postgresPort = await _postgres.StartAsync(password);
            await WritePrivateAsync("statestore.yaml", $"""
                apiVersion: dapr.io/v1alpha1
                kind: Component
                metadata:
                  name: statestore
                spec:
                  type: state.postgresql
                  version: v2
                  metadata:
                    - name: connectionString
                      value: "host=127.0.0.1 port={postgresPort} user=postgres password={password} dbname=postgres sslmode=disable"
                    - name: timeout
                      value: "10s"
                    - name: keyPrefix
                      value: "name"
                """, token);
            await WaitPostgresAsync(token);
            _ports = Ports(4);
            await StartSidecarAsync(token);
            _started = true;
        }
        catch
        {
            await CleanupAsync();
            throw;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Starts (or restarts) the owned sidecar on the same ports and replaces the client and unqualified store.</summary>
    internal async ValueTask StartSidecarAsync(CancellationToken token)
    {
        if (_sidecar is not null) { throw new InvalidOperationException("The lane sidecar is already running."); }
        var start = new ProcessStartInfo(_daprd!) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in new[] { "--app-id", _identity, "--resources-path", _directory!, "--dapr-http-port", Port(0), "--dapr-grpc-port", Port(1),
            "--dapr-internal-grpc-port", Port(2), "--metrics-port", Port(3), "--log-level", "warn" }) { start.ArgumentList.Add(argument); }
        start.Environment.Remove("KUBECONFIG");
        start.Environment["DAPR_API_TOKEN"] = _apiToken;
        _sidecar = Process.Start(start) ?? throw new InvalidOperationException("The lane sidecar could not start.");
        _sidecar.OutputDataReceived += (_, e) => { if (e.Data is not null && _sidecarLog.Count < 10_000) { _sidecarLog.Enqueue(e.Data); } };
        _sidecar.ErrorDataReceived += (_, e) => { if (e.Data is not null && _sidecarLog.Count < 10_000) { _sidecarLog.Enqueue(e.Data); } };
        _sidecar.BeginOutputReadLine(); _sidecar.BeginErrorReadLine();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        using var http = new HttpClient();
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            if (_sidecar.HasExited) { throw new InvalidOperationException("The lane sidecar exited before readiness."); }
            try
            {
                using var probe = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{Port(0)}/v1.0/healthz/outbound");
                probe.Headers.Add(DaprCryptographyGrpcTransport.ApiTokenHeader, _apiToken);
                using HttpResponseMessage response = await http.SendAsync(probe, deadline.Token);
                if (response.IsSuccessStatusCode) { break; }
            }
            catch (HttpRequestException) { }
            await Task.Delay(200, deadline.Token);
        }

        Client?.Dispose();
        Client = new DaprClientBuilder().UseGrpcEndpoint($"http://127.0.0.1:{Port(1)}").UseDaprApiToken(_apiToken).Build();
        Store = new DaprPayloadProtectionStateStore(Client, authorityProfile: PayloadProtectionAuthorityProfile.DaprKubernetesCrypto);
        Transport?.Dispose();
        Transport = CreateTransport(_apiToken);
    }

    /// <summary>Creates a transport to the owned sidecar through configuration, as production resolves it; a null token sends none.</summary>
    internal DaprCryptographyGrpcTransport CreateTransport(string? apiToken)
        => DaprCryptographyGrpcTransport.Create(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DAPR_GRPC_ENDPOINT"] = $"http://127.0.0.1:{Port(1)}",
            ["DAPR_GRPC_PORT"] = Port(1),
            // An explicit empty value keeps any ambient DAPR_API_TOKEN out of the lane.
            ["DAPR_API_TOKEN"] = apiToken ?? "",
        }).Build());

    /// <summary>Collects garbage until pending finalizers have run, then requires that no task exception went unobserved.</summary>
    /// <remarks>A marked, abandoned faulted task is the positive control: the listener must see it, so the empty check is not vacuous.</remarks>
    internal async ValueTask AssertNoUnobservedTaskExceptionAsync()
    {
        int controls = Volatile.Read(ref _controls);
        AbandonFaultedControlTask();
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Yield();
        }

        Volatile.Read(ref _controls).ShouldBe(controls + 1);
        _unobserved.ShouldBeEmpty();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbandonFaultedControlTask()
    {
        var control = new InvalidOperationException();
        control.Data[UnobservedControl] = true;
        _ = Task.FromException(control);
    }

    /// <summary>Stops the owned sidecar; the old client remains for stopped-sidecar assertions until the next start.</summary>
    internal async ValueTask StopSidecarAsync()
    {
        Process? sidecar = _sidecar;
        if (sidecar is null) { return; }
        try
        {
            if (!sidecar.HasExited) { sidecar.Kill(entireProcessTree: true); }
            await sidecar.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { sidecar.Dispose(); _sidecar = null; }
    }

    /// <summary>Returns the bounded daprd output captured so far, for no-leak sentinels.</summary>
    internal string SidecarLog() => string.Join('\n', _sidecarLog);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try { await CleanupAsync(); }
        finally
        {
            if (_listening) { TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException; }
            _gate.Dispose();
        }
    }

    private async ValueTask CleanupAsync()
    {
        var failed = new List<int>();
        Func<ValueTask>[] steps =
        [
            () => { Client?.Dispose(); Transport?.Dispose(); return ValueTask.CompletedTask; },
            StopSidecarAsync,
            async () => { if (_clusterAttempted) { _ = await RunAsync(Kind(), ["delete", "cluster", "--name", _cluster, "--kubeconfig", AdminKubeconfig], kind: true); } },
            async () => { if (_postgres is not null) { await _postgres.CleanupAsync(); } },
            () => { if (_directory is not null && Directory.Exists(_directory)) { Directory.Delete(_directory, recursive: true); } return ValueTask.CompletedTask; },
        ];
        for (int index = 0; index < steps.Length; index++)
        {
            try { await steps[index](); }
            catch { failed.Add(index); }
        }

        if (failed.Count > 0) { throw new InvalidOperationException($"Kubernetes cryptography lane cleanup failed at steps {string.Join(",", failed)}."); }
    }

    private async ValueTask VerifyToolsAsync(CancellationToken token)
    {
        string root = RepositoryRoot();
        using JsonDocument runtimeLock = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(root, "deploy", "dapr", "qualification", "runtime-lock-1.19.1.json"), token));
        JsonElement runtime = runtimeLock.RootElement.GetProperty("runtime");
        if (runtime.GetProperty("version").GetString() != "1.19.1") { throw new InvalidOperationException("The runtime lock does not select daprd 1.19.1."); }
        _daprd = Environment.GetEnvironmentVariable("QUALIFICATION_DAPRD") is { Length: > 0 } configured ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dapr", "bin", "daprd");
        await using (FileStream binary = File.OpenRead(_daprd))
        {
            if (Convert.ToHexStringLower(await SHA256.HashDataAsync(binary, token)) != runtime.GetProperty("binarySha256").GetString())
            { throw new InvalidOperationException("The daprd binary does not match the 1.19.1 runtime lock."); }
        }

        if ((await RunAsync(_daprd, ["--version"], token: token)).Trim() != "1.19.1") { throw new InvalidOperationException("The lane requires daprd 1.19.1."); }
        string kind = await RunAsync(Kind(), ["version"], token: token, kind: true);
        if (!kind.StartsWith("kind " + KindVersion + " ", StringComparison.Ordinal)) { throw new InvalidOperationException("The lane requires kind " + KindVersion + "."); }
        if ((await RunAsync(Kind(), ["get", "clusters"], token: token, kind: true)).Split('\n').Any(line => line.Trim() == _cluster))
        { throw new InvalidOperationException("A cluster with the lane-owned name already exists; shared clusters are refused."); }
    }

    private async ValueTask CreateClusterAsync(CancellationToken token)
    {
        _clusterAttempted = true;
        _ = await RunAsync(Kind(), ["create", "cluster", "--name", _cluster, "--image", NodeImage, "--kubeconfig", AdminKubeconfig, "--wait", "180s"],
            token: token, timeout: TimeSpan.FromMinutes(5), kind: true);
        string node = _cluster + "-control-plane";
        if ((await RunAsync("docker", ["inspect", "--format", "{{index .Config.Labels \"io.x-k8s.kind.cluster\"}}", node], token: token)).Trim() != _cluster)
        { throw new InvalidOperationException("The lane node is not owned by this fixture."); }
        // The lane kubeconfig must target exactly the owned node's published API port and nothing else.
        string[] servers = (await File.ReadAllLinesAsync(AdminKubeconfig, token)).Select(l => l.Trim()).Where(l => l.StartsWith("server: ", StringComparison.Ordinal)).ToArray();
        if (servers.Length != 1) { throw new InvalidOperationException("The lane kubeconfig must name exactly one cluster."); }
        ApiServer = servers[0]["server: ".Length..];
        OwnedNodeEndpoint = "https://" + (await RunAsync("docker", ["port", node, "6443/tcp"], token: token)).Trim();
        if (ApiServer != OwnedNodeEndpoint || !ApiServer.StartsWith("https://127.0.0.1:", StringComparison.Ordinal))
        { throw new InvalidOperationException("The lane kubeconfig does not target the owned cluster."); }
    }

    private async ValueTask ProvisionKeysAsync(CancellationToken token)
    {
        await KubectlAsync(["create", "namespace", Namespace], token: token);
        await KubectlAsync(["create", "namespace", MaximumNamespace], token: token);
        foreach (string name in new[] { "kek-v1", "kek-v2", "kek-other", "kek-ungranted", "kek-wrap", "kek-rotation-v1", "kek-rotation-v2", "kek-erasure" })
        {
            byte[] pem = NewKey();
            try
            {
                await SecretAsync(Namespace, name, DataKey, pem, token);
                if (name == "kek-v1") { await SecretAsync(Namespace, "kek-alias", DataKey, pem, token); }
            }
            finally { CryptographicOperations.ZeroMemory(pem); }
        }

        string[] maximum = Maximum.Split('/');
        byte[] maximumPem = NewKey();
        try { await SecretAsync(maximum[0], maximum[1], maximum[2], maximumPem, token); }
        finally { CryptographicOperations.ZeroMemory(maximumPem); }
        await KubectlAsync(["create", "serviceaccount", "pp-crypto", "-n", Namespace], token: token);
        await KubectlAsync(["create", "serviceaccount", "pp-denied", "-n", Namespace], token: token);
        // The restricted Role grants only get on the named Secrets; kek-ungranted exists but is not named, kek-missing is named but absent.
        await KubectlAsync(["create", "role", "pp-crypto-get", "-n", Namespace, "--verb=get", "--resource=secrets",
            "--resource-name=kek-v1", "--resource-name=kek-v2", "--resource-name=kek-alias", "--resource-name=kek-other", "--resource-name=kek-missing",
            "--resource-name=kek-wrap", "--resource-name=kek-rotation-v1", "--resource-name=kek-rotation-v2", "--resource-name=kek-erasure"], token: token);
        await KubectlAsync(["create", "rolebinding", "pp-crypto-get", "-n", Namespace, "--role=pp-crypto-get", "--serviceaccount=" + Namespace + ":pp-crypto"], token: token);
        await KubectlAsync(["create", "role", "pp-crypto-get", "-n", MaximumNamespace, "--verb=get", "--resource=secrets", "--resource-name=" + maximum[1]], token: token);
        await KubectlAsync(["create", "rolebinding", "pp-crypto-get", "-n", MaximumNamespace, "--role=pp-crypto-get", "--serviceaccount=" + Namespace + ":pp-crypto"], token: token);
        string authority = (await File.ReadAllLinesAsync(AdminKubeconfig, token)).Select(l => l.Trim())
            .Single(l => l.StartsWith("certificate-authority-data: ", StringComparison.Ordinal))["certificate-authority-data: ".Length..];
        foreach ((string account, string file, string component) in new[] { ("pp-crypto", "restricted.kubeconfig", Component), ("pp-denied", "denied.kubeconfig", DeniedComponent) })
        {
            string serviceToken = (await KubectlAsync(["create", "token", account, "-n", Namespace, "--duration=2h"], token: token)).Trim();
            await WritePrivateAsync(file, $"""
                apiVersion: v1
                kind: Config
                clusters:
                - name: lane
                  cluster:
                    server: {ApiServer}
                    certificate-authority-data: {authority}
                users:
                - name: {account}
                  user:
                    token: {serviceToken}
                contexts:
                - name: lane
                  context:
                    cluster: lane
                    user: {account}
                current-context: lane
                """, token);
            // defaultNamespace stays unset so every key name carries its namespace.
            await WritePrivateAsync(component + ".yaml", $"""
                apiVersion: dapr.io/v1alpha1
                kind: Component
                metadata:
                  name: {component}
                spec:
                  type: crypto.dapr.kubernetes.secrets
                  version: v1
                  metadata:
                    - name: kubeconfigPath
                      value: "{Path.Combine(_directory!, file)}"
                """, token);
        }
    }

    private async ValueTask SecretAsync(string space, string name, string dataKey, byte[] pem, CancellationToken token)
        => _ = await KubectlAsync(["create", "secret", "generic", name, "-n", space, "--from-file=" + dataKey + "=/dev/stdin"], pem, token);

    private async ValueTask<string> KubectlAsync(string[] arguments, byte[]? input = null, CancellationToken token = default)
        => await RunAsync("docker", ["exec", "-i", _cluster + "-control-plane", "kubectl", "--kubeconfig=/etc/kubernetes/admin.conf", .. arguments], token: token, input: input);

    private static byte[] NewKey()
    {
        using var rsa = RSA.Create(3072);
        byte[] der = rsa.ExportPkcs8PrivateKey();
        char[] pem = PemEncoding.Write("PRIVATE KEY", der);
        try { return Encoding.ASCII.GetBytes(pem); }
        finally { CryptographicOperations.ZeroMemory(der); Array.Clear(pem); }
    }

    private async ValueTask WritePrivateAsync(string name, string content, CancellationToken token)
    {
        string path = Path.Combine(_directory!, name);
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        // ServiceAccount tokens and the state password stay readable by the owner only.
        if (!OperatingSystem.IsWindows()) { options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite; }
        await using var stream = new FileStream(path, options);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(content), token);
    }

    private async ValueTask WaitPostgresAsync(CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            try { _ = await RunAsync("docker", ["exec", _identity, "pg_isready", "-h", "127.0.0.1", "-U", "postgres"], token: deadline.Token); return; }
            catch (InvalidOperationException) { }
            await Task.Delay(200, deadline.Token);
        }
    }

    private static string Kind() => Environment.GetEnvironmentVariable("HEXALITH_PP_KIND") is { Length: > 0 } configured ? configured : "kind";

    // Every child process gets an explicit environment; kind always sees the lane kubeconfig, never the ambient one.
    private async Task<string> RunAsync(string command, string[] arguments, CancellationToken token = default, byte[]? input = null,
        TimeSpan? timeout = null, bool kind = false)
    {
        var start = new ProcessStartInfo(command)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true, UseShellExecute = false,
        };
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        start.Environment.Remove("KUBECONFIG");
        if (kind) { start.Environment["KUBECONFIG"] = AdminKubeconfig; }
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("A lane process could not start.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        Task<string> stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout ?? TimeSpan.FromSeconds(60));
        try
        {
            if (input is not null) { await process.StandardInput.BaseStream.WriteAsync(input, deadline.Token); }
            process.StandardInput.Close();

            await process.WaitForExitAsync(deadline.Token);
        }
        catch
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); }
            throw;
        }

        string output = await stdout;
        string error = await stderr;
        if (process.ExitCode != 0)
        {
            // Only a digest of stderr is reported: it may name Secrets or carry ServiceAccount tokens.
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                $"A lane process failed with exit {process.ExitCode}; stderrSha256={Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(error)))}."));
        }

        return output;
    }

    private static string RepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx"))) { return directory.FullName; }
        }

        throw new DirectoryNotFoundException("The repository root was not found.");
    }

    // Only exception type names are kept: an unobserved gRPC exception may carry key addresses or sidecar error text.
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        foreach (Exception exception in args.Exception.Flatten().InnerExceptions)
        {
            if (exception.Data.Contains(UnobservedControl)) { Interlocked.Increment(ref _controls); }
            else { _unobserved.Enqueue(exception.GetType().FullName ?? "unknown"); }
        }
    }

    private static string Address(string space, string secret) => space + "/" + secret + "/" + DataKey;
    private string Port(int index) => _ports[index].ToString(CultureInfo.InvariantCulture);

    private static int[] Ports(int count)
    {
        var sockets = new List<TcpListener>();
        try
        {
            for (int i = 0; i < count; i++) { var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start(); sockets.Add(socket); }
            return sockets.Select(s => ((IPEndPoint)s.LocalEndpoint).Port).ToArray();
        }
        finally { foreach (TcpListener socket in sockets) { socket.Stop(); } }
    }
}
