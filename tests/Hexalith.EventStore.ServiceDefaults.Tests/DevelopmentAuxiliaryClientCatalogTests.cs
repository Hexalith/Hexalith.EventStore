using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Shouldly;

namespace Hexalith.EventStore.ServiceDefaults.Tests;

public class DevelopmentAuxiliaryClientCatalogTests
{
    [Fact]
    public async Task ActualHumanHttpHostHasEmptyCatalogInventoryAndRecapturesLoadedInputs()
    {
        string directory = CreateInputs();
        try
        {
            var builder = Builder(directory);
            await using var app = builder.Build();
            app.MapGet("/admin/streams/{id}", () => "human-only");
            await app.StartAsync(TestContext.Current.CancellationToken);
            using var client = new HttpClient();
            string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            (await client.GetStringAsync(address + "/admin/streams/one", TestContext.Current.CancellationToken)).ShouldBe("human-only");
            var actual = await DevelopmentAuxiliaryClientCatalog.CaptureAsync(app.Services, app.Configuration, TestContext.Current.CancellationToken);
            actual.AppId.ShouldBe("admin-run-one");
            actual.Declarations.ShouldBeEmpty(); actual.Targets.ShouldBeEmpty(); actual.Operations.ShouldNotBeNull().ShouldBeEmpty();
            actual.AdmissionPolicies.ShouldNotBeNull().ShouldBeEmpty(); actual.AuxiliaryOperations.ShouldNotBeNull().ShouldBeEmpty();
            actual.ProjectionCapabilities.ShouldNotBeNull().ShouldBeEmpty();
            actual.ReceiverSurface.ShouldNotBeNull().Endpoints.ShouldContain(endpoint => endpoint.Pattern == "/admin/streams/{id}" && !endpoint.Admitted);
            actual.TopologyDocuments[1].ShouldBe(await File.ReadAllBytesAsync(Path.Combine(directory, "acl.json")));
            await File.WriteAllTextAsync(Path.Combine(directory, "acl.json"), """{"spec":{"accessControl":{"defaultAction":"allow","policies":[]}}}""");
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => DevelopmentAuxiliaryClientCatalog.CaptureAsync(app.Services, app.Configuration));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData("/process", false)]
    [InlineData("/PROCESS", false)]
    [InlineData("/actors/{type}/{id}/method/{method}", false)]
    [InlineData("/generated", true)]
    public async Task ActualMappedSdkReceiverCannotPretendToBeAuxiliary(string route, bool generated)
    {
        string directory = CreateInputs();
        try
        {
            var builder = Builder(directory);
            await using var app = builder.Build();
            var endpoint = app.MapPost(route, () => "must-not-enroll");
            if (generated) endpoint.WithMetadata(new RouteCatalogEndpointAttribute(typeof(string), "command"));
            await app.StartAsync(TestContext.Current.CancellationToken);
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => DevelopmentAuxiliaryClientCatalog.CaptureAsync(app.Services, app.Configuration));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData("operation")]
    [InlineData("policy")]
    [InlineData("declared-invocation")]
    [InlineData("sidecar-app")]
    [InlineData("production")]
    public async Task IndependentAclProfileAndAppMismatchesRefuseObservation(string change)
    {
        string directory = CreateInputs();
        try
        {
            var builder = Builder(directory, change == "production" ? "Production" : "Development");
            if (change == "sidecar-app") builder.Configuration["DAPR_APP_ID"] = "other-run";
            if (change == "declared-invocation") builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:DeclaredInvocations"] =
                """[{"CallerAppId":"peer","Method":"admin","HttpMethods":["GET"]}]""";
            if (change is "operation" or "policy")
                await File.WriteAllTextAsync(Path.Combine(directory, "acl.json"), JsonSerializer.Serialize(new
                {
                    spec = new { accessControl = new { defaultAction = "deny", policies = new[]
                    {
                        new { defaultAction = change == "policy" ? "allow" : "deny",
                            operations = change == "operation" ? new[] { new { name = "/admin", action = "allow" } } : [] },
                    } } },
                }));
            await using var app = builder.Build();
            app.MapGet("/", () => "human");
            await app.StartAsync(TestContext.Current.CancellationToken);
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => DevelopmentAuxiliaryClientCatalog.CaptureAsync(app.Services, app.Configuration));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task ExplicitHostingStartupInstallsRealAuthorityAndRemainsInertWithoutRole()
    {
        var ordinary = WebApplication.CreateBuilder();
        new DevelopmentAuxiliaryClientCatalogHostingStartup().Configure(ordinary.WebHost);
        ordinary.Services.ShouldNotContain(service => service.ServiceType == typeof(IRouteCatalogAuthority));
        string directory = CreateInputs();
        try
        {
            var builder = Builder(directory);
            new DevelopmentAuxiliaryClientCatalogHostingStartup().Configure(builder.WebHost);
            await using var app = builder.Build();
            app.Services.GetRequiredService<IRouteCatalogAuthority>().ShouldBeOfType<RouteCatalogAuthority>();
            app.Services.GetRequiredService<IRouteCatalogHostObserver>().ShouldBeOfType<ObservedRouteCatalogHost>();
            typeof(DevelopmentAuxiliaryClientCatalogHostingStartup).Assembly.GetCustomAttributes(typeof(HostingStartupAttribute), false)
                .Cast<HostingStartupAttribute>().ShouldContain(attribute => attribute.HostingStartupType == typeof(DevelopmentAuxiliaryClientCatalogHostingStartup));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData("Production", "aux-client", false)]
    [InlineData("Development", "domain", false)]
    [InlineData("Development", "aux-client", true)]
    public void HostingStartupRefusesUnsupportedOrIncompleteOptIn(string environment, string role, bool missingDirectory)
    {
        string directory = CreateInputs();
        try
        {
            var builder = Builder(directory, environment);
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Role"] = role;
            if (missingDirectory) builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = null;
            Should.Throw<InvalidOperationException>(() => new DevelopmentAuxiliaryClientCatalogHostingStartup().Configure(builder.WebHost));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static WebApplicationBuilder Builder(string directory, string environment = "Development")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment, Args = [] });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        const string prefix = "EventStore:RouteCatalog:DevelopmentBootstrap:";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [prefix + "Role"] = "aux-client", [prefix + "Directory"] = directory,
            [prefix + "HostId"] = "admin", [prefix + "CoordinatorHostId"] = "eventstore", [prefix + "AttemptId"] = "run-one",
            [prefix + "RequiredHostIds:0"] = "admin", [prefix + "RequiredHostIds:1"] = "eventstore",
            [prefix + "SelectedAppIds:0"] = "admin-run-one", [prefix + "SelectedAppIds:1"] = "eventstore-run-one",
            [prefix + "DeclaredInvocations"] = "[]", [prefix + "ComponentPath"] = Path.Combine(directory, "component.json"),
            [prefix + "AccessControlPath"] = Path.Combine(directory, "acl.json"),
            ["EventStore:DomainService:AppId"] = "admin-run-one", ["DAPR_APP_ID"] = "admin-run-one",
        });
        return builder;
    }

    private static string CreateInputs()
    {
        string directory = Path.Combine(Path.GetTempPath(), "aux-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "component.json"), """{"metadata":{"name":"route-catalog"}}""");
        File.WriteAllText(Path.Combine(directory, "acl.json"), """{"spec":{"accessControl":{"defaultAction":"deny","policies":[]}}}""");
        return directory;
    }
}
