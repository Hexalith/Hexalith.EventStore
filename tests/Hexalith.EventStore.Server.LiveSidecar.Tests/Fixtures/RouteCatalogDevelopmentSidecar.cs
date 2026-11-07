using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.ExceptionServices;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Owns one loopback-only Development sidecar for the separate catalog state qualification fixture.</summary>
internal sealed class RouteCatalogDevelopmentSidecar : IAsyncDisposable
{
    private readonly ProcessStartInfo _start;
    private readonly RouteCatalogPortReservations _ports;
    private Process? _process;
    private Task<string>? _stdout;
    private Task<string>? _stderr;
    public string HttpEndpoint { get; }
    public string GrpcEndpoint { get; }
    public string AppId { get; }
    public int AppPort { get; }
    public string RuntimeFingerprint { get; }
    public byte[] Arguments => System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(_start.ArgumentList.ToArray());
    /// <summary>Retains bounded private diagnostics after this owned sidecar exits.</summary>
    internal string Diagnostics { get; private set; } = "";
    /// <summary>Gets the checksum-verified executable selected for this fixture process.</summary>
    internal string Executable => _start.FileName;
    /// <summary>Gets the process identity retained for this fixture's exact-process cleanup.</summary>
    internal int ProcessId => _process?.Id ?? throw new InvalidOperationException("The owned sidecar has not started.");

    public RouteCatalogDevelopmentSidecar(string appId, string resources, string configuration, string? appToken = null)
    {
        AppId = appId;
        int placementPort = DaprTestInfrastructurePorts.Read("EVENTSTORE_TEST_PLACEMENT_PORT", 50005);
        int schedulerPort = DaprTestInfrastructurePorts.Read("EVENTSTORE_TEST_SCHEDULER_PORT", 50006);
        string daprd = Environment.GetEnvironmentVariable("EVENTSTORE_ROUTE_CATALOG_DAPRD")
            ?? Environment.GetEnvironmentVariable("DAPR_BIN")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dapr", "bin", OperatingSystem.IsWindows() ? "daprd.exe" : "daprd");
        using (var runtime = File.OpenRead(daprd)) RuntimeFingerprint = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(runtime));
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "deploy/dapr/qualification/runtime-lock.json"))) repository = repository.Parent;
        if (repository is null) throw new InvalidOperationException("The catalog fixture requires its checked-in runtime lock.");
        using var runtimeLock = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(Path.Combine(repository.FullName, "deploy/dapr/qualification/runtime-lock.json")));
        if (RuntimeFingerprint != runtimeLock.RootElement.GetProperty("runtime").GetProperty("binarySha256").GetString())
            throw new InvalidOperationException("Catalog fixture daprd must match runtime-lock.json. Set EVENTSTORE_ROUTE_CATALOG_DAPRD to that checksum-verified binary.");
        _ports = new RouteCatalogPortReservations();
        try
        {
            HttpEndpoint = $"http://127.0.0.1:{_ports[0]}";
            GrpcEndpoint = $"http://127.0.0.1:{_ports[1]}";
            AppPort = _ports[5];
            _start = new(daprd) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            string[] arguments = ["--app-id", appId, "--dapr-http-port", _ports[0].ToString(), "--dapr-grpc-port", _ports[1].ToString(),
                "--dapr-internal-grpc-port", _ports[2].ToString(), "--metrics-port", _ports[3].ToString(), "--profile-port", _ports[4].ToString(),
                "--resources-path", resources, "--config", configuration, "--dapr-listen-addresses", "127.0.0.1", "--log-level", "warn",
                "--placement-host-address", "localhost:" + placementPort.ToString(CultureInfo.InvariantCulture),
                "--scheduler-host-address", "localhost:" + schedulerPort.ToString(CultureInfo.InvariantCulture)];
            foreach (string argument in arguments) _start.ArgumentList.Add(argument);
            if (appToken is not null)
            {
                _start.ArgumentList.Add("--app-port");
                _start.ArgumentList.Add(AppPort.ToString());
                _start.Environment["APP_API_TOKEN"] = appToken;
            }
        }
        catch
        {
            try { _ports.Dispose(); } catch { /* Preserve the original constructor failure. */ }
            throw;
        }
    }

    /// <summary>Hands only the app reservation to its actual application startup.</summary>
    internal Task StartApplicationAsync(Func<Task> start) => _ports.StartApplicationAsync(start);

    /// <summary>Isolates real native actors in this fixture's bounded placement/scheduler namespace.</summary>
    internal void ConfigureNativeActorNamespace(string value)
    {
        if (value.Length is < 1 or > 63 || value.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
            throw new ArgumentException("A private native actor namespace must be a bounded DNS label.", nameof(value));
        _start.Environment["NAMESPACE"] = value;
    }

    public async Task StartAsync()
    {
        if (_process is not null) throw new InvalidOperationException("The owned catalog fixture sidecar is already started.");
        try
        {
            _ports.StartSidecar(() => _process = Process.Start(_start) ?? throw new InvalidOperationException("Cannot start the owned catalog fixture sidecar."));
            Process owned = _process ?? throw new InvalidOperationException("The owned catalog fixture sidecar did not start.");
            _stdout = owned.StandardOutput.ReadToEndAsync();
            _stderr = owned.StandardError.ReadToEndAsync();
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
            for (int attempt = 0; attempt < 300; attempt++)
            {
                if (owned.HasExited) throw new InvalidOperationException("Catalog fixture sidecar exited: "
                    + await _stdout.ConfigureAwait(false) + await _stderr.ConfigureAwait(false));
                try
                {
                    using var response = await client.GetAsync(HttpEndpoint + "/v1.0/healthz/outbound").ConfigureAwait(false);
                    if (response.StatusCode == HttpStatusCode.NoContent) return;
                }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) { }
                await Task.Delay(100).ConfigureAwait(false);
            }
            string diagnostics = (_stdout?.IsCompletedSuccessfully == true ? _stdout.Result : "")
                + (_stderr?.IsCompletedSuccessfully == true ? _stderr.Result : "");
            throw new InvalidOperationException("Catalog fixture sidecar did not become ready: " + diagnostics[^Math.Min(4000, diagnostics.Length)..]);
        }
        catch
        {
            try { await StopAsync().ConfigureAwait(false); } catch { /* Preserve the first startup failure. */ }
            throw;
        }
    }

    public void RequireRunning()
    {
        if (_process is null || _process.HasExited) throw new InvalidOperationException("The observed catalog fixture sidecar is unavailable.");
    }

    /// <summary>Disables control-plane enrollment for private state-store and direct app-port tests.</summary>
    internal void DisableControlPlane()
    {
        for (int index = 0; index < _start.ArgumentList.Count - 1; index++)
        {
            if (_start.ArgumentList[index] is "--placement-host-address" or "--scheduler-host-address") _start.ArgumentList[index + 1] = "";
        }
        _start.Environment.Remove("DAPR_API_TOKEN");
        _start.Environment.Remove("APP_API_TOKEN");
    }

    public void ConfigureMutualTls(RouteCatalogDevelopmentSentry sentry)
    {
        _start.ArgumentList.Add("--enable-mtls"); _start.ArgumentList.Add("--sentry-address"); _start.ArgumentList.Add(sentry.Address);
        _start.ArgumentList.Add("--control-plane-trust-domain"); _start.ArgumentList.Add("public");
        _start.ArgumentList.Add("--control-plane-namespace"); _start.ArgumentList.Add("default");
        _start.Environment["DAPR_TRUST_ANCHORS"] = File.ReadAllText(Path.Combine(sentry.CredentialsDirectory, "ca.crt"));
        _start.Environment["DAPR_CERT_CHAIN"] = File.ReadAllText(Path.Combine(sentry.CredentialsDirectory, "issuer.crt"));
        _start.Environment["DAPR_CERT_KEY"] = File.ReadAllText(Path.Combine(sentry.CredentialsDirectory, "issuer.key"));
        _start.Environment["NAMESPACE"] = "default";
        // This fixture exposes no Dapr actors/jobs; it does not join the machine's plaintext control plane.
        for (int index = 0; index < _start.ArgumentList.Count - 1; index++)
        {
            if (_start.ArgumentList[index] is "--placement-host-address" or "--scheduler-host-address") _start.ArgumentList[index + 1] = "";
            if (_start.ArgumentList[index] == "--log-level") _start.ArgumentList[index + 1] = "info";
        }
    }

    /// <summary>Enrolls the actual native actor-capable process through the public Development transport seam.</summary>
    internal void ConfigureOwnedTransport(Hexalith.EventStore.Aspire.DevelopmentRouteCatalogTransport transport)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
        transport.ConfigureSidecar(_start);
        for (int index = 0; index < _start.ArgumentList.Count - 1; index++)
            if (_start.ArgumentList[index] == "--log-level") _start.ArgumentList[index + 1] = "info";
    }

    /// <summary>Binds cleanup to the actual native sidecar rather than a launcher.</summary>
    internal void ObserveOwnedTransport(Hexalith.EventStore.Aspire.DevelopmentRouteCatalogTransport transport)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
        transport.ObserveSidecar(_process ?? throw new InvalidOperationException("Sidecar has not started."));
    }

    public async Task StopAsync()
    {
        Exception? first = null;
        try
        {
            if (_process is not null)
            {
                if (!_process.HasExited) _process.Kill(entireProcessTree: true);
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _process.WaitForExitAsync(cleanup.Token).ConfigureAwait(false);
                if (_stdout is not null) Diagnostics += await _stdout.WaitAsync(cleanup.Token).ConfigureAwait(false);
                if (_stderr is not null) Diagnostics += await _stderr.WaitAsync(cleanup.Token).ConfigureAwait(false);
            }
        }
        catch (Exception exception) { first = exception; }
        finally
        {
            try { _ports.Dispose(); }
            catch (Exception exception) { first ??= exception; }
            try
            {
                if (_process is not null && _process.HasExited)
                {
                    _process.Dispose();
                    _process = null;
                }
            }
            catch (Exception exception) { first ??= exception; }
        }
        if (first is not null) ExceptionDispatchInfo.Capture(first).Throw();
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
