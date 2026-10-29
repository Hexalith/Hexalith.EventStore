using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Owns one loopback-only Development sidecar for the separate catalog state qualification fixture.</summary>
internal sealed class RouteCatalogDevelopmentSidecar : IAsyncDisposable
{
    private readonly ProcessStartInfo _start;
    private Process? _process;
    private Task<string>? _stdout;
    private Task<string>? _stderr;
    public string HttpEndpoint { get; }
    public string GrpcEndpoint { get; }
    public string AppId { get; }
    public int AppPort { get; }
    public string RuntimeFingerprint { get; }
    public byte[] Arguments => System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(_start.ArgumentList.ToArray());

    public RouteCatalogDevelopmentSidecar(string appId, string resources, string configuration, string? appToken = null)
    {
        AppId = appId;
        var reservations = Enumerable.Range(0, 6).Select(_ => new TcpListener(IPAddress.Loopback, 0)).ToArray();
        foreach (TcpListener listener in reservations) listener.Start();
        int[] ports = [.. reservations.Select(listener => ((IPEndPoint)listener.LocalEndpoint).Port)];
        foreach (TcpListener listener in reservations) listener.Stop();
        HttpEndpoint = $"http://127.0.0.1:{ports[0]}";
        GrpcEndpoint = $"http://127.0.0.1:{ports[1]}";
        AppPort = ports[5];
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
        _start = new(daprd) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        string[] arguments = ["--app-id", appId, "--dapr-http-port", ports[0].ToString(), "--dapr-grpc-port", ports[1].ToString(),
            "--dapr-internal-grpc-port", ports[2].ToString(), "--metrics-port", ports[3].ToString(), "--profile-port", ports[4].ToString(),
            "--resources-path", resources, "--config", configuration, "--dapr-listen-addresses", "127.0.0.1", "--log-level", "warn",
            "--placement-host-address", "localhost:50005", "--scheduler-host-address", "localhost:50006"];
        foreach (string argument in arguments) _start.ArgumentList.Add(argument);
        if (appToken is not null)
        {
            _start.ArgumentList.Add("--app-port");
            _start.ArgumentList.Add(AppPort.ToString());
            _start.Environment["APP_API_TOKEN"] = appToken;
        }
    }

    public async Task StartAsync()
    {
        _process = Process.Start(_start) ?? throw new InvalidOperationException("Cannot start the owned catalog fixture sidecar.");
        _stdout = _process.StandardOutput.ReadToEndAsync();
        _stderr = _process.StandardError.ReadToEndAsync();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        for (int attempt = 0; attempt < 300; attempt++)
        {
            if (_process.HasExited) throw new InvalidOperationException("Catalog fixture sidecar exited: " + await _stderr.ConfigureAwait(false));
            try
            {
                using var response = await client.GetAsync(HttpEndpoint + "/v1.0/healthz/outbound").ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NoContent) return;
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            await Task.Delay(100).ConfigureAwait(false);
        }
        await StopAsync().ConfigureAwait(false);
        string diagnostics = (_stdout?.Result ?? "") + (_stderr?.Result ?? "");
        throw new InvalidOperationException("Catalog fixture sidecar did not become ready: " + diagnostics[^Math.Min(4000, diagnostics.Length)..]);
    }

    public void RequireRunning()
    {
        if (_process is null || _process.HasExited) throw new InvalidOperationException("The observed catalog fixture sidecar is unavailable.");
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

    public async Task StopAsync()
    {
        if (_process is null) return;
        if (!_process.HasExited) _process.Kill(entireProcessTree: true);
        await _process.WaitForExitAsync().ConfigureAwait(false);
        if (_stdout is not null) await _stdout.ConfigureAwait(false);
        if (_stderr is not null) await _stderr.ConfigureAwait(false);
        _process.Dispose();
        _process = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
