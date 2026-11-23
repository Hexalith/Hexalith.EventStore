using System.Text.Json;
using global::Aspire.Hosting;
using global::Aspire.Hosting.ApplicationModel;
using global::Aspire.Hosting.Dotnet;
using CommunityToolkit.Aspire.Hosting.Dapr;
using Hexalith.EventStore.Aspire;

namespace Hexalith.EventStore.AppHost.Tests.Configuration;

/// <summary>Exercises the published topology contracts with Aspire's path-based .NET applications.</summary>
public sealed class DotnetProjectCatalogTests
{
    /// <summary>Only the selected owner's generated sidecar process can supply its private runtime evidence.</summary>
    [Fact]
    public void AProjectOrAnotherOwnersProcessCannotServeAsTheGeneratedSidecar()
    {
        var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
        string projectPath = new EventStoreProjectMetadata().ProjectPath;
        var owner = builder.AddDotnetProject("owner", projectPath);
        var impostor = builder.AddDotnetProject("owner-dapr-cli", projectPath).WithParentRelationship(owner);
        var model = new DistributedApplicationModel([owner.Resource, impostor.Resource]);
        Should.Throw<InvalidOperationException>(() => DevelopmentRouteCatalogAspireExtensions.GetGeneratedSidecar(model, owner.Resource, impostor.Resource.Name));
        var foreignOwner = builder.AddDotnetProject("foreign", projectPath);
        var executable = builder.AddExecutable("foreign-cli", "dapr", ".").WithParentRelationship(foreignOwner);
        model = new DistributedApplicationModel([owner.Resource, executable.Resource]);
        Should.Throw<InvalidOperationException>(() => DevelopmentRouteCatalogAspireExtensions.GetGeneratedSidecar(model, owner.Resource, executable.Resource.Name));
    }

    /// <summary>The gateway and domain retain their own channel credentials and rendered catalog identities.</summary>
    [Fact]
    public async Task PathBasedProjectsKeepTheirOwnCredentialsAndCatalogReceiversAsync()
    {
        string directory = Directory.CreateTempSubdirectory("dotnet-project-catalog-").FullName;
        try
        {
            string config = Path.Combine(directory, "config.json");
            string state = Path.Combine(directory, "state.json");
            await File.WriteAllTextAsync(config, JsonSerializer.Serialize(new { spec = new
            {
                accessControl = new { defaultAction = "deny", trustDomain = "public" },
                nameResolution = new { component = "sqlite", version = "v1", configuration = new { connectionString = Path.Combine(directory, "registry.sqlite") } },
            } }), TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(state, """{"kind":"Component","spec":{"type":"state.redis","version":"v1","metadata":[{"name":"redisHost","value":"localhost:16379"}]}}""", TestContext.Current.CancellationToken);
            var builder = DistributedApplication.CreateBuilder(new DistributedApplicationOptions { DisableDashboard = true });
            builder.Environment.EnvironmentName = "Development";
            string projectPath = new EventStoreProjectMetadata().ProjectPath;
            IResourceBuilder<DotnetProjectResource> Project(string name) => builder.AddDotnetProject(name, projectPath)
                .WithDaprSidecar(sidecar => sidecar.WithOptions(new DaprSidecarOptions { AppId = name, Config = config }));
            var gateway = Project("gateway");
            var domain = Project("domain");
            var security = builder.AddHexalithEventStoreSecurity().ShouldNotBeNull();
            var workloadSecret = builder.AddParameter("workload-secret", () => "test-only", secret: true);
            gateway.WithJwtBearerSecurity(security).WithEventStoreWorkloadClientCredentials("gateway", workloadSecret).ShouldBeSameAs(gateway);
            domain.WithOpenIdConnectSecurity(security, "domain", "test-only").ShouldBeSameAs(domain);
            var assembly = typeof(DevelopmentCatalogProbeDeclarations).Assembly;
            await using var catalog = builder.AddEventStoreDevelopmentRouteCatalog(gateway,
                [new DevelopmentRouteCatalogOwner<DotnetProjectResource>(domain, assembly, assembly)],
                [new(gateway, []), new(domain, [new(gateway, "process", ["POST"])])],
                state, DevelopmentCatalogModelCustody.Create(directory), "owned-session");
            builder.AddEventStoreDevelopmentSqliteStartupOrdering(gateway, domain);
            foreach (var project in new[] { gateway, domain })
            {
                string name = project.Resource.Name;
                ParameterResource channelParameter = builder.Resources.OfType<ParameterResource>().Single(parameter => parameter.Name == name + "-app-api-token");
                channelParameter.Secret.ShouldBeTrue();
                var sidecar = project.Resource.Annotations.OfType<DaprSidecarAnnotation>().Single().Sidecar;
                var callbackContext = new EnvironmentCallbackContext(builder.ExecutionContext, sidecar, cancellationToken: TestContext.Current.CancellationToken);
                foreach (var callback in sidecar.Annotations.OfType<EnvironmentCallbackAnnotation>())
                    await callback.Callback(callbackContext);
                callbackContext.EnvironmentVariables["APP_API_TOKEN"].ShouldBeSameAs(channelParameter);
                File.Exists(Path.Combine(catalog.Directory, name + ".configuration.json")).ShouldBeTrue();
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
