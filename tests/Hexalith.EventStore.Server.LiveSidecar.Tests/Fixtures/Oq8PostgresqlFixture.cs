using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Dapr.Client;

using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Testing.Fakes;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures;

/// <summary>
/// Story 4.8 production-equivalent OQ8 fixture: two EventStore hosts and Dapr 1.18.x
/// sidecars share the production <c>state.postgresql</c> actor store and resiliency profile.
/// </summary>
public sealed class Oq8PostgresqlFixture : IAsyncLifetime
{
    private const string AppId = "eventstore";
    private const string PostgresImage = "postgres:18.4";
    private const int PlacementPort = 50005;
    private const int SchedulerPort = 50006;
    private const int HealthTimeoutSeconds = 60;
    private readonly Node[] _nodes = [new(), new()];
    private string? _componentsDirectory;
    private string? _postgresContainerName;
    private string? _postgresConnectionString;
    private string? _previousDaprHttpPort;
    private string? _previousDaprGrpcPort;

    /// <summary>Gets the isolated aggregate actor type shared by both hosts.</summary>
    public string AggregateActorTypeName { get; } = $"Oq8AggregateActor{Guid.NewGuid():N}";

    /// <summary>Gets the shared fake domain boundary.</summary>
    public FakeDomainServiceInvoker DomainServiceInvoker { get; } = new();

    /// <summary>Gets the shared fake publication boundary.</summary>
    public FakeEventPublisher EventPublisher { get; } = new();

    /// <summary>Gets host one services while the node is running.</summary>
    public IServiceProvider PrimaryServices => GetNodeServices(0);

    /// <summary>Gets host two services while the node is running.</summary>
    public IServiceProvider ReplicaServices => GetNodeServices(1);

    /// <summary>Gets host one sidecar endpoint.</summary>
    public string PrimaryDaprHttpEndpoint => _nodes[0].DaprHttpEndpoint;

    /// <summary>Gets host two sidecar endpoint.</summary>
    public string ReplicaDaprHttpEndpoint => _nodes[1].DaprHttpEndpoint;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        _previousDaprHttpPort = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT");
        _previousDaprGrpcPort = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT");
        try
        {
            await VerifyPrerequisitesAsync().ConfigureAwait(false);
            await StartPostgresAsync().ConfigureAwait(false);
            _componentsDirectory = CreateProductionProfileResources();

            foreach (Node node in _nodes)
            {
                int[] ports = GetAvailablePorts(6);
                node.AppPort = ports[0];
                node.DaprHttpPort = ports[1];
                node.DaprGrpcPort = ports[2];
                node.DaprInternalGrpcPort = ports[3];
                node.DaprMetricsPort = ports[4];
                node.DaprProfilePort = ports[5];
                await StartHostAsync(node).ConfigureAwait(false);
            }

            foreach (Node node in _nodes)
            {
                StartSidecar(node);
                await WaitForSidecarHealthAsync(node).ConfigureAwait(false);
            }

            await Task.Delay(3000).ConfigureAwait(false);
        }
        catch
        {
            await DisposeResourcesAsync().ConfigureAwait(false);
            RestoreDaprEnvironment();
            throw;
        }
    }

    /// <summary>Removes the primary EventStore host and sidecar without touching PostgreSQL.</summary>
    public Task StopPrimaryNodeAsync() => StopNodeAsync(_nodes[0]);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await DisposeResourcesAsync().ConfigureAwait(false);
        RestoreDaprEnvironment();
    }

    private IServiceProvider GetNodeServices(int index)
        => _nodes[index].Host?.Services
            ?? throw new InvalidOperationException($"OQ8 EventStore node {index + 1} is not running.");

    private async Task StartPostgresAsync()
    {
        int postgresPort = GetAvailablePorts(1)[0];
        _postgresContainerName = $"eventstore-oq8-{Guid.NewGuid():N}";
        string password = $"oq8-{Guid.NewGuid():N}";
        _postgresConnectionString =
            $"host=127.0.0.1 port={postgresPort} user=postgres password={password} dbname=eventstore sslmode=disable connect_timeout=10";

        string containerId = await RunProcessAsync(
            "docker",
            [
                "run", "--rm", "-d",
                "--name", _postgresContainerName,
                "-e", $"POSTGRES_PASSWORD={password}",
                "-e", "POSTGRES_DB=eventstore",
                "-p", $"127.0.0.1:{postgresPort}:5432",
                PostgresImage,
            ]).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(containerId))
        {
            throw new InvalidOperationException("The OQ8 PostgreSQL container did not return an identity.");
        }

        Exception? lastError = null;
        for (int attempt = 0; attempt < 60; attempt++)
        {
            try
            {
                // Force the TCP listener check (-h/-p) rather than the default Unix socket: the
                // Unix socket accepts connections before the TCP listener does, and during that
                // gap Docker's published port still completes the client handshake then resets it,
                // surfacing as a Dapr "connection reset by peer" state-store init failure.
                _ = await RunProcessAsync(
                    "docker",
                    ["exec", _postgresContainerName, "pg_isready", "-h", "127.0.0.1", "-p", "5432", "-U", "postgres", "-d", "eventstore"])
                    .ConfigureAwait(false);
                return;
            }
            catch (Exception exception)
            {
                lastError = exception;
                await Task.Delay(500).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("The OQ8 PostgreSQL container did not become ready.", lastError);
    }

    private string CreateProductionProfileResources()
    {
        string root = FindRepositoryRoot();
        string temporary = Path.Combine(Path.GetTempPath(), $"eventstore-oq8-components-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(temporary);
        string stateStore = File.ReadAllText(Path.Combine(root, "deploy", "dapr", "statestore-postgresql.yaml"));
        const string connectionStringPlaceholder = "{env:POSTGRES_CONNECTION_STRING}";
        if (!stateStore.Contains(connectionStringPlaceholder, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The production PostgreSQL component no longer contains the expected secret placeholder.");
        }

        File.WriteAllText(
            Path.Combine(temporary, "statestore-postgresql.yaml"),
            stateStore.Replace(connectionStringPlaceholder, _postgresConnectionString!, StringComparison.Ordinal));
        File.Copy(
            Path.Combine(root, "deploy", "dapr", "resiliency.yaml"),
            Path.Combine(temporary, "resiliency.yaml"));
        File.WriteAllText(
            Path.Combine(temporary, "pubsub.yaml"),
            """
            apiVersion: dapr.io/v1alpha1
            kind: Component
            metadata:
              name: pubsub
            spec:
              type: pubsub.redis
              version: v1
              metadata:
                - name: redisHost
                  value: "localhost:6379"
                - name: redisPassword
                  value: ""
            scopes:
              - eventstore
            """);
        return temporary;
    }

    private async Task StartHostAsync(Node node)
    {
        Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", node.DaprHttpPort.ToString());
        Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", node.DaprGrpcPort.ToString());
        try
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions());
            builder.Configuration["DAPR_HTTP_PORT"] = node.DaprHttpPort.ToString();
            builder.Configuration["DAPR_GRPC_PORT"] = node.DaprGrpcPort.ToString();
            builder.Configuration["Dapr:HttpPort"] = node.DaprHttpPort.ToString();
            builder.Configuration["Dapr:GrpcPort"] = node.DaprGrpcPort.ToString();
            builder.Configuration["EventStore:Actors:AggregateActorTypeName"] = AggregateActorTypeName;
            builder.Configuration["EventStore:ProjectionDispatch:RetryWorkerInterval"] = "00:10:00";
            builder.Configuration["EventStore:IdempotencyAdmission:Enabled"] = "true";
            builder.Configuration["EventStore:IdempotencyAdmission:ActiveDigestKeyVersion"] = "oq8-v1";
            builder.Configuration["EventStore:IdempotencyAdmission:DigestKeySource"] = "Configuration";
            builder.Configuration["EventStore:IdempotencyAdmission:DigestKeys:oq8-v1"] =
                "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";
            _ = builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(
                node.AppPort,
                listen => listen.Protocols = HttpProtocols.Http1));

            _ = builder.Services.AddSingleton(
                new DaprClientBuilder()
                    .UseHttpEndpoint(node.DaprHttpEndpoint)
                    .UseGrpcEndpoint(node.DaprGrpcEndpoint)
                    .Build());
            _ = builder.Services.AddSingleton<IIdempotencyIntentAdapter, LiveIncrementCounterIdempotencyIntentAdapter>();
            _ = builder.Services.AddEventStoreServer(builder.Configuration);
            _ = builder.Services.AddSingleton<IDomainServiceInvoker>(DomainServiceInvoker);
            _ = builder.Services.AddSingleton<IEventPublisher>(EventPublisher);
            _ = builder.Services.AddSingleton<IDeadLetterPublisher, FakeDeadLetterPublisher>();
            _ = builder.Services.AddSingleton<ICommandStatusStore, InMemoryCommandStatusStore>();

            node.Host = builder.Build();
            _ = node.Host.MapActorsHandlers();
            _ = node.Host.MapGet("/healthz", () => Microsoft.AspNetCore.Http.Results.Ok("healthy"));
            await node.Host.StartAsync().ConfigureAwait(false);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", _previousDaprHttpPort);
            Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", _previousDaprGrpcPort);
        }
    }

    private void StartSidecar(Node node)
    {
        string daprd = ResolveDaprdPath();
        var startInfo = new ProcessStartInfo
        {
            FileName = daprd,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in new[]
        {
            "--app-id", AppId,
            "--app-port", node.AppPort.ToString(),
            "--app-protocol", "http",
            "--app-channel-address", "127.0.0.1",
            "--dapr-http-port", node.DaprHttpPort.ToString(),
            "--dapr-grpc-port", node.DaprGrpcPort.ToString(),
            "--dapr-internal-grpc-port", node.DaprInternalGrpcPort.ToString(),
            "--metrics-port", node.DaprMetricsPort.ToString(),
            "--profile-port", node.DaprProfilePort.ToString(),
            "--resources-path", _componentsDirectory!,
            "--log-level", "info",
            "--placement-host-address", $"localhost:{PlacementPort}",
            "--scheduler-host-address", $"localhost:{SchedulerPort}",
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["POSTGRES_CONNECTION_STRING"] = _postgresConnectionString!;
        node.Sidecar = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        node.Sidecar.OutputDataReceived += (_, eventArgs) => Append(node.Stdout, eventArgs.Data);
        node.Sidecar.ErrorDataReceived += (_, eventArgs) => Append(node.Stderr, eventArgs.Data);
        _ = node.Sidecar.Start();
        node.Sidecar.BeginOutputReadLine();
        node.Sidecar.BeginErrorReadLine();
    }

    private static void Append(StringBuilder builder, string? value)
    {
        if (value is null)
        {
            return;
        }

        lock (builder)
        {
            _ = builder.AppendLine(value);
        }
    }

    private static string Captured(StringBuilder builder)
    {
        lock (builder)
        {
            return builder.ToString();
        }
    }

    private static async Task WaitForSidecarHealthAsync(Node node)
    {
        using var client = new HttpClient();
        string health = $"{node.DaprHttpEndpoint}/v1.0/healthz/outbound";
        for (int attempt = 0; attempt < HealthTimeoutSeconds; attempt++)
        {
            if (node.Sidecar?.HasExited == true)
            {
                throw new InvalidOperationException(
                    $"OQ8 daprd exited with {node.Sidecar.ExitCode}.\n" +
                    $"stdout:\n{Captured(node.Stdout)}\nstderr:\n{Captured(node.Stderr)}");
            }

            try
            {
                HttpResponseMessage response = await client.GetAsync(health).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"OQ8 daprd did not become healthy.\nstdout:\n{Captured(node.Stdout)}\nstderr:\n{Captured(node.Stderr)}");
    }

    private async Task DisposeResourcesAsync()
    {
        foreach (Node node in _nodes)
        {
            await StopNodeAsync(node).ConfigureAwait(false);
        }

        if (_componentsDirectory is not null && Directory.Exists(_componentsDirectory))
        {
            Directory.Delete(_componentsDirectory, recursive: true);
        }

        _componentsDirectory = null;
        if (_postgresContainerName is not null)
        {
            try
            {
                _ = await RunProcessAsync("docker", ["rm", "-f", _postgresContainerName]).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort exact-container cleanup; preserve the original test failure.
            }

            _postgresContainerName = null;
        }
    }

    private static async Task StopNodeAsync(Node node)
    {
        if (node.Sidecar is not null && !node.Sidecar.HasExited)
        {
            node.Sidecar.Kill(entireProcessTree: true);
            await node.Sidecar.WaitForExitAsync().ConfigureAwait(false);
        }

        node.Sidecar?.Dispose();
        node.Sidecar = null;
        if (node.Host is not null)
        {
            await node.Host.StopAsync().ConfigureAwait(false);
            await node.Host.DisposeAsync().ConfigureAwait(false);
            node.Host = null;
        }
    }

    private void RestoreDaprEnvironment()
    {
        Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", _previousDaprHttpPort);
        Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", _previousDaprGrpcPort);
    }

    private static async Task VerifyPrerequisitesAsync()
    {
        foreach ((int Port, string Name) prerequisite in new[]
        {
            (PlacementPort, "Dapr placement"),
            (SchedulerPort, "Dapr scheduler"),
        })
        {
            using var client = new TcpClient();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                await client.ConnectAsync(IPAddress.Loopback, prerequisite.Port, cancellation.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"{prerequisite.Name} is unavailable on localhost:{prerequisite.Port}.",
                    exception);
            }
        }

        _ = await RunProcessAsync("docker", ["image", "inspect", PostgresImage]).ConfigureAwait(false);
    }

    private static async Task<string> RunProcessAsync(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        _ = process.Start();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        string output = await stdout.ConfigureAwait(false);
        string error = await stderr.ConfigureAwait(false);
        return process.ExitCode == 0
            ? output.Trim()
            : throw new InvalidOperationException(
                $"Process '{fileName}' exited with code {process.ExitCode}: {error.Trim()}");
    }

    private static string ResolveDaprdPath()
    {
        string candidate = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".dapr",
            "bin",
            "daprd" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));
        return File.Exists(candidate)
            ? candidate
            : OperatingSystem.IsWindows() ? "daprd.exe" : "daprd";
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Hexalith.EventStore.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The EventStore repository root could not be located.");
    }

    private static int[] GetAvailablePorts(int count)
    {
        var listeners = new TcpListener[count];
        int[] ports = new int[count];
        for (int index = 0; index < count; index++)
        {
            listeners[index] = new TcpListener(IPAddress.Loopback, 0);
            listeners[index].Start();
            ports[index] = ((IPEndPoint)listeners[index].LocalEndpoint).Port;
        }

        foreach (TcpListener listener in listeners)
        {
            listener.Stop();
        }

        return ports;
    }

    private sealed class Node
    {
        public int AppPort { get; set; }

        public int DaprHttpPort { get; set; }

        public int DaprGrpcPort { get; set; }

        public int DaprInternalGrpcPort { get; set; }

        public int DaprMetricsPort { get; set; }

        public int DaprProfilePort { get; set; }

        public WebApplication? Host { get; set; }

        public Process? Sidecar { get; set; }

        public StringBuilder Stdout { get; } = new();

        public StringBuilder Stderr { get; } = new();

        public string DaprHttpEndpoint => $"http://127.0.0.1:{DaprHttpPort}";

        public string DaprGrpcEndpoint => $"http://127.0.0.1:{DaprGrpcPort}";
    }
}
