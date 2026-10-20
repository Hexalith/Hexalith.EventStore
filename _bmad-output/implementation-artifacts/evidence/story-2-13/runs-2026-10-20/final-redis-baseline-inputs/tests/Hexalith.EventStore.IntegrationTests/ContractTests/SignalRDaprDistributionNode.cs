using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hexalith.EventStore.IntegrationTests.ContractTests;

/// <summary>Owns one real EventStore process and its independent Dapr sidecar for the qualification fixture.</summary>
public sealed class SignalRDaprDistributionNode : IAsyncDisposable
{
    private readonly ConcurrentQueue<string> _applicationLog = new();
    private readonly ConcurrentQueue<string> _sidecarLog = new();
    private Process? _application;
    private Process? _sidecar;
    private ProcessStartInfo? _applicationStartInfo;

    /// <summary>Initializes one isolated process pair.</summary>
    public SignalRDaprDistributionNode(string name)
    {
        Name = name;
        AppPort = ReservePort();
        DaprHttpPort = ReservePort();
        DaprGrpcPort = ReservePort();
        DaprInternalGrpcPort = ReservePort();
        MetricsPort = ReservePort();
        ProfilePort = ReservePort();
        AppChannelToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    }

    /// <summary>Gets the fixture-local node label.</summary>
    public string Name { get; }

    /// <summary>Gets the application HTTP port.</summary>
    public int AppPort { get; }

    /// <summary>Gets the sidecar HTTP port.</summary>
    public int DaprHttpPort { get; }

    /// <summary>Gets the sidecar gRPC port.</summary>
    public int DaprGrpcPort { get; }

    /// <summary>Gets the sidecar internal gRPC port.</summary>
    public int DaprInternalGrpcPort { get; }

    /// <summary>Gets the sidecar metrics port.</summary>
    public int MetricsPort { get; }

    /// <summary>Gets the sidecar profile port.</summary>
    public int ProfilePort { get; }

    /// <summary>Gets the application-channel secret, shared only with this node's sidecar.</summary>
    public string AppChannelToken { get; }

    /// <summary>Gets the application HTTP endpoint.</summary>
    public Uri AppEndpoint => new($"http://127.0.0.1:{AppPort}");

    /// <summary>Gets the sidecar HTTP endpoint.</summary>
    public Uri DaprHttpEndpoint => new($"http://127.0.0.1:{DaprHttpPort}");

    /// <summary>Gets the sidecar gRPC endpoint.</summary>
    public string DaprGrpcEndpoint => $"http://127.0.0.1:{DaprGrpcPort}";

    /// <summary>Gets the application process ID after startup.</summary>
    public int ApplicationProcessId => _application?.Id ?? throw new InvalidOperationException("Application has not started.");

    /// <summary>Gets the sidecar process ID after startup.</summary>
    public int SidecarProcessId => _sidecar?.Id ?? throw new InvalidOperationException("Sidecar has not started.");

    /// <summary>Starts the product host and then its own sidecar.</summary>
    public async Task StartAsync(
        string repositoryRoot,
        string componentsPath,
        CancellationToken cancellationToken,
        bool enablePubSubProjectionTransport = false,
        string? aggregateActorTypeName = null)
    {
        string assembly = Path.Combine(repositoryRoot, "src", "Hexalith.EventStore", "bin", "Debug", "net10.0", "Hexalith.EventStore.dll");
        if (!File.Exists(assembly))
        {
            throw new FileNotFoundException("Build the IntegrationTests project in Debug before running this proof.", assembly);
        }

        var application = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(assembly)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        application.ArgumentList.Add("exec");
        application.ArgumentList.Add(assembly);
        application.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        application.Environment["DOTNET_ENVIRONMENT"] = "Development";
        application.Environment["ASPNETCORE_URLS"] = AppEndpoint.ToString();
        application.Environment["DAPR_APP_ID"] = "eventstore";
        application.Environment["DAPR_HTTP_PORT"] = DaprHttpPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        application.Environment["DAPR_GRPC_PORT"] = DaprGrpcPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        application.Environment["DAPR_HTTP_ENDPOINT"] = DaprHttpEndpoint.ToString();
        application.Environment["DAPR_GRPC_ENDPOINT"] = DaprGrpcEndpoint;
        application.Environment["APP_API_TOKEN"] = AppChannelToken;
        application.Environment["EnableKeycloak"] = "false";
        application.Environment["Authentication__JwtBearer__Authority"] = string.Empty;
        application.Environment["Authentication__JwtBearer__Issuer"] = Helpers.TestJwtTokenGenerator.Issuer;
        application.Environment["Authentication__JwtBearer__Audience"] = Helpers.TestJwtTokenGenerator.Audience;
        application.Environment["Authentication__JwtBearer__SigningKey"] = Helpers.TestJwtTokenGenerator.SigningKey;
        application.Environment["Authentication__JwtBearer__RequireHttpsMetadata"] = "false";
        application.Environment["Authentication__JwtBearer__AllowInsecureSymmetricKey"] = "true";
        application.Environment["EventStore__SignalR__Enabled"] = "true";
        application.Environment["EventStore__OpenApi__Enabled"] = "false";
        application.Environment["Logging__LogLevel__Default"] = "Information";
        if (enablePubSubProjectionTransport)
        {
            application.Environment["EventStore__ProjectionChanges__Transport"] = "PubSub";
            ArgumentException.ThrowIfNullOrWhiteSpace(aggregateActorTypeName);
            application.Environment["EventStore__Actors__AggregateActorTypeName"] = aggregateActorTypeName;
        }
        _ = application.Environment.Remove("EVENTSTORE_SIGNALR_REDIS");
        _ = application.Environment.Remove("EventStore__SignalR__BackplaneRedisConnectionString");
        _applicationStartInfo = application;
        _application = StartCapturedProcess(application, _applicationLog);

        await StartSidecarAsync(componentsPath, cancellationToken).ConfigureAwait(false);
        await WaitForHealthyAsync(AppEndpoint, "/alive", _application, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Stops this node's sidecar so the test can exercise outage and recovery.</summary>
    public async Task StopSidecarAsync()
    {
        await StopProcessAsync(_sidecar).ConfigureAwait(false);
        _sidecar = null;
    }

    /// <summary>Stops the owned replica and sidecar, leaving its configuration available for restart.</summary>
    public async Task StopAsync()
    {
        await StopSidecarAsync().ConfigureAwait(false);
        await StopProcessAsync(_application).ConfigureAwait(false);
        _application = null;
    }

    /// <summary>Restarts the same application configuration without inheriting any old hub membership.</summary>
    public async Task RestartApplicationAsync(CancellationToken cancellationToken)
    {
        if (_application is not null || _applicationStartInfo is null)
        {
            throw new InvalidOperationException("Stop a previously started application before restarting it.");
        }

        _application = StartCapturedProcess(_applicationStartInfo, _applicationLog);
        await WaitForHealthyAsync(AppEndpoint, "/alive", _application, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Starts this node's sidecar after an outage.</summary>
    public async Task StartSidecarAsync(string componentsPath, CancellationToken cancellationToken)
    {
        if (_sidecar is not null)
        {
            throw new InvalidOperationException("Sidecar is already running.");
        }

        string daprd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dapr", "bin", "daprd");
        if (!File.Exists(daprd))
        {
            throw new FileNotFoundException("Dapr 1.18 sidecar is required for the runtime proof.", daprd);
        }

        var sidecar = new ProcessStartInfo(daprd)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        string[] arguments = [
            "--app-id", "eventstore",
            "--app-port", AppPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--app-protocol", "http",
            "--app-channel-address", "127.0.0.1",
            "--dapr-http-port", DaprHttpPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--dapr-grpc-port", DaprGrpcPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--dapr-internal-grpc-port", DaprInternalGrpcPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--metrics-port", MetricsPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--profile-port", ProfilePort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--resources-path", componentsPath,
            "--placement-host-address", "127.0.0.1:50005",
            "--scheduler-host-address", "127.0.0.1:50006",
            "--log-level", "info",
        ];
        foreach (string argument in arguments)
        {
            sidecar.ArgumentList.Add(argument);
        }

        sidecar.Environment["APP_API_TOKEN"] = AppChannelToken;
        _sidecar = StartCapturedProcess(sidecar, _sidecarLog);
        try
        {
            await WaitForHealthyAsync(DaprHttpEndpoint, "/v1.0/healthz", _sidecar, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"{Name} sidecar failed to start. {SnapshotDiagnostics()}", exception);
        }
    }

    /// <summary>Returns bounded diagnostics without credentials.</summary>
    public string SnapshotDiagnostics()
    {
        string combined = string.Join('\n', _applicationLog.Concat(_sidecarLog)
            .Where(static line => line.Contains("/projections/changed", StringComparison.OrdinalIgnoreCase)
                || line.Contains("projection change notification", StringComparison.OrdinalIgnoreCase)
                || line.Contains("SignalR broadcast", StringComparison.OrdinalIgnoreCase)
                || line.Contains("detail", StringComparison.OrdinalIgnoreCase)
                || line.Contains("subscribed to the following topics", StringComparison.OrdinalIgnoreCase)
                || line.Contains("error", StringComparison.OrdinalIgnoreCase))
            .TakeLast(40));
        return combined.Replace(AppChannelToken, "[redacted]", StringComparison.Ordinal)
            .Replace(Helpers.TestJwtTokenGenerator.SigningKey, "[redacted]", StringComparison.Ordinal);
    }

    /// <summary>Returns recent application lines for a failed composed command, with test credentials redacted.</summary>
    public string SnapshotRecentApplicationLogs()
        => string.Join('\n', _applicationLog.TakeLast(40))
            .Replace(AppChannelToken, "[redacted]", StringComparison.Ordinal)
            .Replace(Helpers.TestJwtTokenGenerator.SigningKey, "[redacted]", StringComparison.Ordinal);

    /// <summary>Returns bounded captured process logs with test keys and JWT credentials removed.</summary>
    public string SnapshotRedactedLogs()
    {
        string combined = string.Join('\n', _applicationLog.Concat(_sidecarLog))
            .Replace(AppChannelToken, "[redacted]", StringComparison.Ordinal)
            .Replace(Helpers.TestJwtTokenGenerator.SigningKey, "[redacted]", StringComparison.Ordinal);
        return Regex.Replace(combined, @"\beyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b", "[redacted-jwt]") + "\n";
    }

    /// <summary>Reads value-free runtime/component/subscription identities through the Dapr metadata API.</summary>
    public async Task<JsonElement> GetRuntimeMetadataAsync(CancellationToken cancellationToken)
    {
        using var client = new HttpClient { BaseAddress = DaprHttpEndpoint, Timeout = TimeSpan.FromSeconds(5) };
        string json = await client.GetStringAsync("/v1.0/metadata", cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        return JsonSerializer.SerializeToElement(new
        {
            id = root.GetProperty("id").GetString(),
            runtimeVersion = root.GetProperty("runtimeVersion").GetString(),
            components = root.GetProperty("components").EnumerateArray().Select(component => new
            {
                name = component.GetProperty("name").GetString(),
                type = component.GetProperty("type").GetString(),
                version = component.GetProperty("version").GetString(),
            }).ToArray(),
            subscriptions = root.GetProperty("subscriptions").EnumerateArray().Select(subscription => new
            {
                pubsubname = subscription.GetProperty("pubsubname").GetString(),
                topic = subscription.GetProperty("topic").GetString(),
                type = subscription.GetProperty("type").GetString(),
            }).ToArray(),
        });
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private static int ReservePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static Process StartCapturedProcess(ProcessStartInfo info, ConcurrentQueue<string> log)
    {
        Process process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start {info.FileName}.");
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                log.Enqueue(args.Data);
                TrimLog(log);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                log.Enqueue(args.Data);
                TrimLog(log);
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static void TrimLog(ConcurrentQueue<string> log)
    {
        while (log.Count > 2_000)
        {
            _ = log.TryDequeue(out _);
        }
    }

    private static async Task WaitForHealthyAsync(Uri endpoint, string path, Process? process, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { BaseAddress = endpoint, Timeout = TimeSpan.FromSeconds(2) };
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(60);
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process?.HasExited == true)
            {
                throw new InvalidOperationException($"Process {process.Id} exited with code {process.ExitCode} before health.");
            }

            try
            {
                using HttpResponseMessage response = await client.GetAsync(path, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }

            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Process at {endpoint} did not become healthy within 60 seconds.");
    }

    private static async Task StopProcessAsync(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            }
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            process.Dispose();
        }
    }
}
