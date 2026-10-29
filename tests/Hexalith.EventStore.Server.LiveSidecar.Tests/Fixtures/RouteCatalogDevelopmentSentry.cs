using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>Owns a pinned Development Sentry and temporary credentials, without touching global Dapr certificates.</summary>
internal sealed class RouteCatalogDevelopmentSentry : IAsyncDisposable
{
    private const string Image = "ghcr.io/dapr/sentry@sha256:b9fc166085289661f516150dc1c47ac3495b9e6d1e666079cb71cd2374ca7256";
    private Process? _process;
    private Task<string>? _output;
    private Task<string>? _errors;
    public string Address { get; private set; } = "";
    public string CredentialsDirectory { get; private set; } = "";

    public async Task StartAsync(string scratch, RouteCatalogDocker? docker = null, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux()) throw new InvalidOperationException("The pinned disposable Sentry qualification requires Linux.");
        docker ??= await RouteCatalogDocker.CaptureAsync(cancellationToken: cancellationToken);
        string binary = Path.Combine(scratch, "sentry");
        string container = "catalog-sentry-extract-" + Hexalith.Commons.UniqueIds.UniqueIdHelper.GenerateSortableUniqueStringId().ToLowerInvariant();
        const string label = "hexalith.route-catalog-sentry-fixture";
        string id = (await docker.RunAsync(["create", "--name", container, "--label", label + "=" + container, "--pull", "never", Image, "/sentry"], cancellationToken)).Trim();
        try { await docker.RunAsync(["cp", id + ":/sentry", binary], cancellationToken); }
        finally
        {
            using var inspection = System.Text.Json.JsonDocument.Parse(await docker.RunAsync(["inspect", id]));
            var owned = inspection.RootElement[0];
            if (owned.GetProperty("Id").GetString() != id
                || owned.GetProperty("Config").GetProperty("Labels").GetProperty(label).GetString() != container)
                throw new InvalidOperationException("Refusing to remove Sentry extraction without exact ownership.");
            await docker.RunAsync(["rm", "-f", id]);
        }
        File.SetUnixFileMode(binary, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        CredentialsDirectory = Path.Combine(scratch, "sentry-credentials");
        Directory.CreateDirectory(CredentialsDirectory);
        var reservations = new[] { new TcpListener(IPAddress.Loopback, 0), new TcpListener(IPAddress.Loopback, 0) };
        foreach (var listener in reservations) listener.Start();
        int port = ((IPEndPoint)reservations[0].LocalEndpoint).Port, health = ((IPEndPoint)reservations[1].LocalEndpoint).Port;
        foreach (var listener in reservations) listener.Stop();
        Address = "127.0.0.1:" + port;
        string configuration = Path.Combine(scratch, "sentry-configuration.json");
        await File.WriteAllBytesAsync(configuration, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
        {
            apiVersion = "dapr.io/v1alpha1", kind = "Configuration", metadata = new { name = "catalog-fixture-sentry", @namespace = "default" },
            spec = new { mtls = new { enabled = true, workloadCertTTL = "1h", allowedClockSkew = "15s" } },
        }));
        var start = new ProcessStartInfo(binary) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { "--config", configuration, "--issuer-credentials", CredentialsDirectory, "--trust-domain", "public", "--listen-address", "127.0.0.1", "--port", port.ToString(),
            "--healthz-listen-address", "127.0.0.1", "--healthz-port", health.ToString(), "--enable-metrics=false", "--log-level", "warn" }) start.ArgumentList.Add(argument);
        _process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start the owned Development Sentry.");
        _output = _process.StandardOutput.ReadToEndAsync(); _errors = _process.StandardError.ReadToEndAsync();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        for (int attempt = 0; attempt < 100; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_process.HasExited) throw new InvalidOperationException("Owned Development Sentry exited " + _process.ExitCode + ": " + await _output + await _errors);
            if (File.Exists(Path.Combine(CredentialsDirectory, "ca.crt")) && File.Exists(Path.Combine(CredentialsDirectory, "issuer.key")))
            {
                try { using var ready = await client.GetAsync($"http://127.0.0.1:{health}/healthz", cancellationToken); if (ready.IsSuccessStatusCode) return; }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) { }
            }
            await Task.Delay(100, cancellationToken);
        }
        throw new InvalidOperationException("The owned Development Sentry did not become ready.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_process is null) return;
        if (!_process.HasExited) _process.Kill(entireProcessTree: true);
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await _process.WaitForExitAsync(cleanup.Token);
        if (_output is not null) await _output;
        if (_errors is not null) await _errors;
        _process.Dispose();
        _process = null;
    }
}
