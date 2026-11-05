using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.EventStore.Aspire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

/// <summary>Verifies actual SDK console observation and toolkit-generated startup dependencies without starting processes.</summary>
public sealed class DevelopmentSqliteDiscoveryStartupTests
{
    private const string Ready = "time=\"2026-11-04T14:00:00Z\" level=info msg=\"Initialized name resolution to sqlite\" app_id=peer scope=dapr.runtime type=log ver=1.18.7";

    /// <summary>A running resource or arbitrary text cannot release the native SQLite startup gate.</summary>
    [Fact(Timeout = 30000)]
    public async Task ActualResourceConsoleReleasesOnlyItsNativeInitializationAsync()
    {
        ExecutableResource native = new("peer-dapr-cli", "/model/daprd", ".");
        using ResourceLoggerService logs = new();
        DaprSqliteDiscoveryInitializationHealthCheck check = new(logs, new DistributedApplicationModel(new IResource[] { native }), native.Name, "peer");
        Task<HealthCheckResult> waiting = check.CheckHealthAsync(new(), TestContext.Current.CancellationToken);
        waiting.IsCompleted.ShouldBeFalse();
        logs.GetLogger(native).LogInformation("{NativeLine}", Ready);
        (await waiting).Status.ShouldBe(HealthStatus.Healthy);
    }

    /// <summary>Refuses other peers, non-native records, malformed records, missing proof and actual initialization failure.</summary>
    [Theory(Timeout = 30000)]
    [InlineData("foreign")]
    [InlineData("scope")]
    [InlineData("severity")]
    [InlineData("duplicate")]
    [InlineData("payload")]
    [InlineData("missing")]
    [InlineData("failed")]
    public async Task IncompleteOrForeignNativeEvidenceCannotReleaseStartupAsync(string scenario)
    {
        ExecutableResource native = new("peer-dapr-cli", "/model/daprd", ".");
        using ResourceLoggerService logs = new();
        string line = scenario switch
        {
            "foreign" => Ready.Replace("app_id=peer", "app_id=foreign", StringComparison.Ordinal),
            "scope" => Ready.Replace("scope=dapr.runtime", "scope=application", StringComparison.Ordinal),
            "severity" => Ready.Replace("level=info", "level=error", StringComparison.Ordinal),
            "duplicate" => Ready + " app_id=peer",
            "payload" => "application-message=" + JsonSerializer.Serialize(Ready),
            "failed" => Ready.Replace("level=info", "level=error", StringComparison.Ordinal).Replace("Initialized name resolution to sqlite", "[INIT_COMPONENT_FAILURE]: nr (sqlite/v1): SQLITE_BUSY", StringComparison.Ordinal),
            _ => "unrelated native startup message",
        };
        logs.GetLogger(native).LogInformation("{NativeLine}", line);
        logs.Complete(native);
        DaprSqliteDiscoveryInitializationHealthCheck check = new(logs, new DistributedApplicationModel(new IResource[] { native }), native.Name, "peer");
        (await check.CheckHealthAsync(new(), TestContext.Current.CancellationToken)).Status.ShouldBe(HealthStatus.Unhealthy);
    }

    /// <summary>Cancellation wins over buffered success and cancels an ongoing console observation.</summary>
    [Theory(Timeout = 30000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanceledObservationNeverAcceptsBufferedOrLateSuccessAsync(bool buffered)
    {
        ExecutableResource native = new("peer-dapr-cli", "/model/daprd", ".");
        using ResourceLoggerService logs = new();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        if (buffered) logs.GetLogger(native).LogInformation("{NativeLine}", Ready);
        DaprSqliteDiscoveryInitializationHealthCheck check = new(logs, new DistributedApplicationModel(new IResource[] { native }), native.Name, "peer");
        if (buffered) await cancellation.CancelAsync();
        Task<HealthCheckResult> waiting = check.CheckHealthAsync(new(), cancellation.Token);
        await cancellation.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await waiting.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken));
        logs.GetLogger(native).LogInformation("{NativeLine}", Ready);
    }

    /// <summary>Registers after actual toolkit rendering and adds only the declared native readiness chain.</summary>
    [Theory(Timeout = 30000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ToolkitGeneratedNativeExecutablesRespectCatalogOwnerPreparationDependenciesAsync(bool tenantsLast)
    {
        string directory = Directory.CreateTempSubdirectory("sqlite-startup-model-").FullName;
        try
        {
            string path = Path.Combine(directory, "discovery.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { spec = new { nameResolution = new
            { component = "sqlite", version = "v1", configuration = new { connectionString = Path.Combine(directory, "registry.sqlite") } } } }), TestContext.Current.CancellationToken);
            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true, AllowUnsecuredTransport = true });
            builder.Environment.EnvironmentName = "Development";
            builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(directory, "model-only-dcp-not-started");
            builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(directory, "model-only-dashboard-not-started");
            builder.AddDapr(options => options.DaprPath = Path.Combine(directory, "daprd"));
            string[] names = ["eventstore", "projects", "projects-workers", "folders", "folders-workers", "tenants"];
            var peers = names.Select(name => builder.AddProject<EventStoreProjectMetadata>(name)
                .WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions { AppId = name, Config = path }))).ToArray();
            // These are the supported profile's selected owner waits after canonical catalog composition.
            peers[0].WaitForStart(peers[5]);
            peers[1].WaitForStart(peers[0]).WaitForStart(peers[5]);
            peers[3].WaitForStart(peers[5]);
            var ordered = tenantsLast ? peers : peers.TakeLast(1).Concat(peers.Take(5)).ToArray();
            builder.AddEventStoreDevelopmentSqliteStartupOrdering(ordered);
            await using var application = builder.Build();
            var eventing = application.Services.GetRequiredService<IDistributedApplicationEventing>();
            foreach (var subscriber in application.Services.GetServices<IDistributedApplicationEventingSubscriber>().Where(subscriber =>
                subscriber.GetType().Name == "DaprDistributedApplicationLifecycleHook" || subscriber is DevelopmentSqliteDiscoveryStartupSubscriber))
                await subscriber.SubscribeAsync(eventing, builder.ExecutionContext, TestContext.Current.CancellationToken);
            var beforeStart = new BeforeStartEvent(application.Services, application.Services.GetRequiredService<DistributedApplicationModel>());
            if (tenantsLast)
            {
                (await Should.ThrowAsync<InvalidOperationException>(() => eventing.PublishAsync(beforeStart, TestContext.Current.CancellationToken)))
                    .Message.ShouldContain("owner-preparation dependency cycle");
                return;
            }
            await eventing.PublishAsync(beforeStart, TestContext.Current.CancellationToken);
            var model = application.Services.GetRequiredService<DistributedApplicationModel>();
            var native = ordered.Select(peer => model.Resources.OfType<ExecutableResource>().Single(resource => resource.Name == peer.Resource.Name + "-dapr-cli")).ToArray();
            for (int index = 0; index < native.Length; index++)
            {
                native[index].Annotations.OfType<HealthCheckAnnotation>().Single().Key.ShouldBe("sqlite-discovery-startup-" + ordered[index].Resource.Name);
                var prior = native[index].Annotations.OfType<WaitAnnotation>().Where(wait => native.Contains(wait.Resource)).ToArray();
                prior.Length.ShouldBe(index == 0 ? 0 : 1);
                if (index > 0) { prior[0].Resource.ShouldBeSameAs(native[index - 1]); prior[0].WaitType.ShouldBe(WaitType.WaitUntilHealthy); }
            }
            native[4].Annotations.OfType<WaitAnnotation>().ShouldContain(wait => ReferenceEquals(wait.Resource, peers[5].Resource) && wait.WaitType == WaitType.WaitUntilStarted);
            var edges = model.Resources.Select(resource => new { resource.Name, Waits = resource.Annotations.OfType<WaitAnnotation>()
                .Select(wait => new { wait.Resource.Name, Type = wait.WaitType.ToString() }).ToArray() }).ToArray();
            TestContext.Current.TestOutputHelper!.WriteLine(JsonSerializer.Serialize(edges));
            HashSet<IResource> finished = [], visiting = [];
            foreach (var resource in model.Resources) Visit(resource);
            void Visit(IResource resource)
            {
                if (finished.Contains(resource)) return;
                visiting.Add(resource).ShouldBeTrue("The actual toolkit-generated wait graph must be acyclic.");
                foreach (var wait in resource.Annotations.OfType<WaitAnnotation>()) Visit(wait.Resource);
                if (resource is ProjectResource owner && ordered.Any(peer => ReferenceEquals(peer.Resource, owner)))
                    foreach (var wait in native.Single(cli => cli.Name == owner.Name + "-dapr-cli").Annotations.OfType<WaitAnnotation>()) Visit(wait.Resource);
                visiting.Remove(resource);
                finished.Add(resource);
            }
            File.Exists(Path.Combine(directory, "registry.sqlite")).ShouldBeFalse();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>Unselected profiles cannot silently gain the new gate.</summary>
    [Fact]
    public void ProductionCannotEnableDevelopmentSqliteOrdering()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        builder.Environment.EnvironmentName = "Production";
        Should.Throw<InvalidOperationException>(() => builder.AddEventStoreDevelopmentSqliteStartupOrdering([]));
        builder.Resources.ShouldBeEmpty();
    }
}
