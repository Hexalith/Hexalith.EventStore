using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.EventStore.Aspire;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.LiveSidecar.Tests.Integration;

public sealed partial class RouteCatalogHostedLiveTests
{
    /// <summary>Launches the actual pinned CLI from the public Aspire enrollment's rendered arguments and secret environment.</summary>
    [Fact(Timeout = 60000)]
    public async Task PublicDevelopmentTransportAspireCliLaunchUsesOwnedSecretInputs()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException();
        string scratch = Directory.CreateTempSubdirectory("eventstore-transport-cli-").FullName;
        string runtime = Environment.GetEnvironmentVariable("EVENTSTORE_ROUTE_CATALOG_DAPRD")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dapr/bin/daprd");
        string cli = Environment.GetEnvironmentVariable("EVENTSTORE_ROUTE_CATALOG_DAPR_CLI")
            ?? Environment.GetEnvironmentVariable("PATH")!.Split(Path.PathSeparator)
                .Select(path => Path.Combine(path, "dapr")).First(File.Exists);
        Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(cli, TestContext.Current.CancellationToken)))
            .ShouldBe(SelectedRuntimeProfile.CliSha256);
        await using var transport = await DevelopmentRouteCatalogTransport.StartAsync(new DevelopmentRouteCatalogTransportOptions(Path.Combine(scratch, "custody"), runtime) { RuntimeProfile = SelectedRuntimeProfile }, "Development", TestContext.Current.CancellationToken);
        string config = Path.Combine(scratch, "receiver.yaml");
        await File.WriteAllTextAsync(config, "apiVersion: dapr.io/v1alpha1\nkind: Configuration\nmetadata:\n  name: owned-cli-probe\nspec:\n  accessControl:\n    defaultAction: deny\n    trustDomain: public\n", TestContext.Current.CancellationToken);
        string resources = Directory.CreateDirectory(Path.Combine(scratch, "components")).FullName;
        using var httpReservation = new TcpListener(IPAddress.Loopback, 0);
        using var grpcReservation = new TcpListener(IPAddress.Loopback, 0);
        httpReservation.Start(); grpcReservation.Start();
        int http = ((IPEndPoint)httpReservation.LocalEndpoint).Port, grpc = ((IPEndPoint)grpcReservation.LocalEndpoint).Port;
        httpReservation.Stop(); grpcReservation.Stop();
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "deploy/dapr/qualification/runtime-lock.json"))) repository = repository.Parent;
        repository.ShouldNotBeNull();
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(scratch, "model-only-dcp-not-started");
        builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(scratch, "model-only-dashboard-not-started");
        builder.Environment.EnvironmentName = "Development";
        builder.AddDapr(options => options.DaprPath = cli);
        var project = builder.AddProject("owned-cli-probe", Path.Combine(repository.FullName, "samples/Hexalith.EventStore.Sample.Api/Hexalith.EventStore.Sample.Api.csproj"))
            .WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions
            {
                AppId = "owned-cli-" + Guid.NewGuid().ToString("N"), Config = config,
                DaprHttpPort = http, DaprGrpcPort = grpc, ResourcesPaths = [resources],
            }));
        project.WithEventStoreDevelopmentRouteCatalogTransport(transport);
        await using var application = builder.Build();
        var eventing = application.Services.GetRequiredService<IDistributedApplicationEventing>();
        foreach (var subscriber in application.Services.GetServices<IDistributedApplicationEventingSubscriber>()
            .Where(subscriber => subscriber.GetType().Name == "DaprDistributedApplicationLifecycleHook"))
            await subscriber.SubscribeAsync(eventing, builder.ExecutionContext, TestContext.Current.CancellationToken);
        var model = application.Services.GetRequiredService<DistributedApplicationModel>();
        await eventing.PublishAsync(new BeforeStartEvent(application.Services, model), TestContext.Current.CancellationToken);
        var executable = model.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == "owned-cli-probe-dapr-cli");
        foreach (var endpoint in executable.Annotations.OfType<EndpointAnnotation>())
        {
            using var reservation = new TcpListener(IPAddress.Loopback, 0);
            reservation.Start();
            int selected = endpoint.Port ?? ((IPEndPoint)reservation.LocalEndpoint).Port;
            endpoint.AllocatedEndpoint = new AllocatedEndpoint(endpoint, "127.0.0.1", selected,
                targetPortExpression: (endpoint.TargetPort ?? selected).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        Dictionary<string, object> environment = [];
        foreach (var callback in executable.Annotations.OfType<EnvironmentCallbackAnnotation>())
            await callback.Callback(new EnvironmentCallbackContext(builder.ExecutionContext, executable, environment, TestContext.Current.CancellationToken));
        environment["DAPR_CERT_KEY"].ShouldBeOfType<ParameterResource>().Secret.ShouldBeTrue();
        List<object> arguments = [];
        foreach (var callback in executable.Annotations.OfType<CommandLineArgsCallbackAnnotation>())
            await callback.Callback(new CommandLineArgsCallbackContext(arguments, executable, TestContext.Current.CancellationToken));
        var start = new ProcessStartInfo(executable.Command) { WorkingDirectory = scratch, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument is IValueProvider provider ? (await provider.GetValueAsync(TestContext.Current.CancellationToken))! : argument.ToString()!);
        foreach (var pair in environment)
            start.Environment[pair.Key] = pair.Value is IValueProvider provider ? await provider.GetValueAsync(TestContext.Current.CancellationToken) : pair.Value.ToString();
        using var process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync(), errors = process.StandardError.ReadToEndAsync();
        bool ready = false;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
            for (int attempt = 0; attempt < 150 && !process.HasExited; attempt++)
            {
                try
                {
                    using var response = await client.GetAsync($"http://127.0.0.1:{http}/v1.0/healthz/outbound", TestContext.Current.CancellationToken);
                    if (response.StatusCode == HttpStatusCode.NoContent) { ready = true; break; }
                }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) when (!TestContext.Current.CancellationToken.IsCancellationRequested) { }
                await Task.Delay(100, TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(deadline.Token);
            string log = await output.WaitAsync(deadline.Token) + await errors.WaitAsync(deadline.Token);
            string path = Path.Combine(scratch, "cli-private.log");
            await File.WriteAllTextAsync(path, log, deadline.Token);
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            TestContext.Current.TestOutputHelper!.WriteLine("Owned CLI private diagnostics: " + path);
        }
        ready.ShouldBeTrue("The exact public Aspire secret/argument rendering must launch a real authenticated native sidecar.");
    }
}
