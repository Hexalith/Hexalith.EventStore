// Effective authority: eb45653553efe53ba83e6dd6c9796b47dfb6fb0bba0628ff95f6bc1b96b054c9; base PF-02/section 10; amendment section 7.
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Dapr.Client;

namespace Hexalith.EventStore.PayloadProtection.LiveSidecar.Tests;

/// <summary>Owns an isolated PostgreSQL container, independent Dapr process, unique namespace and temporary credentials.</summary>
public sealed class PayloadProtectionDaprFixture : IAsyncLifetime
{
    private const string PostgresImage = "postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636";
    private readonly string _identity = "pp-lifecycle-" + Guid.NewGuid().ToString("N");
    private Process? _sidecar;
    private Task<string>? _stdout;
    private Task<string>? _stderr;
    private string? _directory;
    private bool _containerStarted;
    private DaprClient? _client;

    /// <summary>Gets the exact Dapr-backed state primitive, initially unqualified.</summary>
    internal DaprPayloadProtectionStateStore Store { get; private set; } = null!;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        try
        {
            int[] ports = Ports(5);
            string password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            _ = await RunAsync("docker", ["image", "inspect", "--format", "{{.RepoDigests}}", PostgresImage]).ConfigureAwait(false);
            await RunAsync("docker", ["run", "--detach", "--rm", "--name", _identity, "--publish", $"127.0.0.1:{ports[0]}:5432", "--env", "POSTGRES_PASSWORD", PostgresImage], password).ConfigureAwait(false);
            _containerStarted = true;
            _directory = Path.Combine(Path.GetTempPath(), _identity);
            Directory.CreateDirectory(_directory);
            string component = $"""
                apiVersion: dapr.io/v1alpha1
                kind: Component
                metadata:
                  name: statestore
                spec:
                  type: state.postgresql
                  version: v2
                  metadata:
                    - name: connectionString
                      value: "host=127.0.0.1 port={ports[0]} user=postgres password={password} dbname=postgres sslmode=disable"
                    - name: timeout
                      value: "10s"
                    - name: keyPrefix
                      value: "name"
                """;
            await File.WriteAllTextAsync(Path.Combine(_directory, "statestore.yaml"), component).ConfigureAwait(false);
            await WaitPostgresAsync().ConfigureAwait(false);
            string daprd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dapr", "bin", OperatingSystem.IsWindows() ? "daprd.exe" : "daprd");
            string version = await RunAsync(daprd, ["--version"]).ConfigureAwait(false);
            if (version.Trim() != "1.18.7") { throw new InvalidOperationException("PF-02 requires the selected Dapr runtime 1.18.7."); }
            _sidecar = Start(daprd, ["--app-id", _identity, "--resources-path", _directory,
                "--dapr-http-port", ports[1].ToString(), "--dapr-grpc-port", ports[2].ToString(),
                "--dapr-internal-grpc-port", ports[3].ToString(), "--metrics-port", ports[4].ToString(), "--log-level", "warn"]);
            _stdout = _sidecar.StandardOutput.ReadToEndAsync();
            _stderr = _sidecar.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using var http = new HttpClient();
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (_sidecar.HasExited) { throw new InvalidOperationException("The isolated PF-02 sidecar exited before readiness."); }
                try
                {
                    using HttpResponseMessage response = await http.GetAsync($"http://127.0.0.1:{ports[1]}/v1.0/healthz/outbound", deadline.Token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode) { break; }
                }
                catch (HttpRequestException) { }
                await Task.Delay(200, deadline.Token).ConfigureAwait(false);
            }

            _client = new DaprClientBuilder().UseGrpcEndpoint($"http://127.0.0.1:{ports[2]}").Build();
            Store = new(_client);
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_sidecar is not null)
        {
            if (!_sidecar.HasExited) { _sidecar.Kill(entireProcessTree: true); }
            await _sidecar.WaitForExitAsync().ConfigureAwait(false);
            if (_stdout is not null) { _ = await _stdout.ConfigureAwait(false); }
            if (_stderr is not null) { _ = await _stderr.ConfigureAwait(false); }
            _sidecar.Dispose();
            _sidecar = null;
        }

        if (_containerStarted)
        {
            await RunAsync("docker", ["rm", "--force", _identity]).ConfigureAwait(false);
            _containerStarted = false;
        }

        if (_directory is not null && Directory.Exists(_directory)) { Directory.Delete(_directory, recursive: true); }
    }

    private async Task WaitPostgresAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            // The bootstrap server can accept Unix-socket checks before the final TCP listener starts.
            try { _ = await RunAsync("docker", ["exec", _identity, "pg_isready", "-h", "127.0.0.1", "-U", "postgres"]).ConfigureAwait(false); return; }
            catch (InvalidOperationException) { }
            await Task.Delay(200, deadline.Token).ConfigureAwait(false);
        }
    }

    private static Process Start(string command, string[] arguments, string? postgresPassword = null)
    {
        var start = new ProcessStartInfo(command) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        if (postgresPassword is not null) { start.Environment["POSTGRES_PASSWORD"] = postgresPassword; }
        return Process.Start(start) ?? throw new InvalidOperationException("A required isolated PF-02 process could not start.");
    }

    private static async Task<string> RunAsync(string command, string[] arguments, string? postgresPassword = null)
    {
        using Process process = Start(command, arguments, postgresPassword);
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false); }
        catch { if (!process.HasExited) { process.Kill(entireProcessTree: true); } throw; }
        string output = await stdout.ConfigureAwait(false);
        string error = await stderr.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"A required PF-02 process failed with exit {process.ExitCode}; stderrSha256={Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(error)))}.");
        }

        return output;
    }

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
