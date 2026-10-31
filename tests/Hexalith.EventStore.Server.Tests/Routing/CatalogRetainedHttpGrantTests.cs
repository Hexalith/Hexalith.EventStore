using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.ServiceDefaults.Authentication;
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

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Retained inert ACL declarations never lend their SDK witness to a reachable foreign endpoint.</summary>
public sealed class CatalogRetainedHttpGrantTests
{
    [Fact]
    public async Task ExistingWorkloadActionsKeepTheirExactAuthenticationPolicyAndCompiledWitness()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(CommandStatusController).Assembly);
        await using var app = builder.Build();
        app.MapControllers();
        string[] paths = ["api/v1/commands/workload", "api/v1/commands/bound/workload",
            "api/v1/commands/status/workload/{tenant}/{messageId}", "api/v1/queries/owner-authority/workload", "api/v1/streams/read/workload", "api/v1/trusted-effects"];
        var endpoints = ((Microsoft.AspNetCore.Routing.IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>().Where(endpoint => paths.Contains(endpoint.RoutePattern.RawText?.Trim('/'), StringComparer.Ordinal)).ToArray();
        endpoints.Length.ShouldBe(paths.Length);
        foreach (var endpoint in endpoints)
        {
            DevelopmentTechnicalHttpTransport.Observe(endpoint).ShouldNotBeNull();
            var withoutPolicy = new Microsoft.AspNetCore.Routing.RouteEndpoint(endpoint.RequestDelegate!, endpoint.RoutePattern, endpoint.Order,
                new Microsoft.AspNetCore.Http.EndpointMetadataCollection(endpoint.Metadata.Where(item => item is not Microsoft.AspNetCore.Authorization.IAuthorizeData)), endpoint.DisplayName);
            DevelopmentTechnicalHttpTransport.Observe(withoutPolicy).ShouldBeNull();
            var humanReplacement = new Microsoft.AspNetCore.Routing.RouteEndpoint(endpoint.RequestDelegate!, endpoint.RoutePattern, endpoint.Order,
                new Microsoft.AspNetCore.Http.EndpointMetadataCollection(endpoint.Metadata.Where(item => item is not Microsoft.AspNetCore.Authorization.IAuthorizeData)
                    .Append(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute("EventStoreHuman"))), endpoint.DisplayName);
            DevelopmentTechnicalHttpTransport.Observe(humanReplacement).ShouldBeNull();
        }
    }

    [Fact]
    public async Task DoubleScalarStatusGrantCapturesActualSdkCodeButRefusesShortFormAndIntendedPath()
    {
        string directory = Directory.CreateTempSubdirectory("catalog-retained-grant-").FullName;
        try
        {
            const string grant = "/api/v1/commands/status/workload/*/*";
            string acl = Path.Combine(directory, "acl.json");
            await File.WriteAllBytesAsync(acl, JsonSerializer.SerializeToUtf8Bytes(new
            { spec = new { accessControl = new { policies = new[] { new { operations = new[] { new { name = grant, action = "allow", httpVerb = new[] { "GET" } } } } } } } }));
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = directory;
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"] = acl;
            builder.Configuration[DaprAppChannelToken.ConfigurationKey] = "retained-grant-channel";
            builder.Services.AddSingleton<FiniteRouteCatalogReceiver>();
            builder.Services.AddControllers().AddApplicationPart(typeof(CommandStatusController).Assembly);
            builder.Services.AddAuthorization();
            await using var app = builder.Build();
            int foreignCalls = 0;
            app.MapGet("/api/v1/commands/status/workload/{tenant}", () => { foreignCalls++; return "foreign"; });
            app.MapControllers();
            app.UseRouting();
            FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(app, DevelopmentTechnicalHttpTransport.Operation, technical: true);
            app.UseAuthorization();
            await app.StartAsync(TestContext.Current.CancellationToken);
            var surface = app.Services.GetRequiredService<FiniteRouteCatalogReceiver>().Observe();
            var endpoint = surface.Endpoints.Single(endpoint => endpoint.Pattern.Trim('/') == "api/v1/commands/status/workload/{tenant}/{messageId}");
            endpoint.Admitted.ShouldBeTrue();
            endpoint.Http.ShouldNotBeNull();
            endpoint.Http.Owner.ShouldBe(typeof(CommandStatusController).FullName);
            endpoint.Http.Grants.ShouldNotBeNull().Single().Method.ShouldBe(grant);
            using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false })
            { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "retained-grant-channel");
            foreach (string path in new[] { "/api/v1/commands/status/workload/tenant-a", "/api/v1/commands/status/workload/tenant-a/command-id" })
            {
                using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
                response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            }
            foreignCalls.ShouldBe(0);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
