using System.Net;

using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Shouldly;

namespace Hexalith.EventStore.ServiceDefaults.Tests;

/// <summary>Exercises finite receiver admission through the actual selected Kestrel endpoint.</summary>
public sealed class FiniteRouteCatalogReceiverTests
{
    /// <summary>Retains endpoint identity when ASP.NET Core supplies implicit routing.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CapturedEndpointAdmitsActualHttpRequest(bool explicitRouting)
    {
        string directory = Path.Combine(Path.GetTempPath(), "finite-receiver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string acl = Path.Combine(directory, "acl.json");
        await File.WriteAllTextAsync(acl,
            """{"spec":{"accessControl":{"policies":[{"operations":[{"name":"/process","action":"allow"}]}]}}}""",
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        try
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = directory;
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"] = acl;
            builder.Configuration[DaprAppChannelToken.ConfigurationKey] = "finite-receiver-channel";
            builder.Services.AddSingleton<FiniteRouteCatalogReceiver>();
            builder.Services.AddAuthorization(options => options.AddPolicy("finite-test", policy => policy.RequireAssertion(_ => true)));
            await using WebApplication app = builder.Build();
            if (explicitRouting)
            {
                app.UseRouting();
                app.UseAuthorization();
            }
            Endpoint? selected = null;
            app.Use((context, next) => { selected = context.GetEndpoint(); return next(context); });
            app.MapPost("/process", () => Results.Ok()).RequireAuthorization("finite-test");
            FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(app, _ => "domain-service:process");
            await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false })
            {
                BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()),
            };
            client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "finite-receiver-channel");
            using HttpResponseMessage response = await client.PostAsync("/process", null, TestContext.Current.CancellationToken).ConfigureAwait(true);
            Endpoint routed = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).Single();
            selected.ShouldBeSameAs(routed);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
    /// <summary>Refuses changed identities, mutable policies, anonymous replacements and newly reachable descendants before effects.</summary>
    [Theory]
    [InlineData("same")]
    [InlineData("policy")]
    [InlineData("anonymous")]
    [InlineData("descendant")]
    [InlineData("verb")]
    [InlineData("pattern")]
    public async Task ChangedSourceCannotInheritCapturedAdmission(string change)
    {
        string directory = Path.Combine(Path.GetTempPath(), "finite-receiver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string acl = Path.Combine(directory, "acl.json");
        await File.WriteAllTextAsync(acl,
            """{"spec":{"accessControl":{"policies":[{"operations":[{"name":"/project","action":"allow"}]}]}}}""",
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        try
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = directory;
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"] = acl;
            builder.Configuration[DaprAppChannelToken.ConfigurationKey] = "finite-receiver-channel";
            builder.Services.AddSingleton<FiniteRouteCatalogReceiver>();
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("finite-test", policy => policy.RequireAssertion(_ => true));
                options.AddPolicy("finite-changed", policy => policy.RequireAssertion(_ => true));
            });
            await using WebApplication app = builder.Build();
            app.UseRouting();
            app.UseAuthorization();
            int effects = 0;
            var authorization = new AuthorizeAttribute("finite-test");
            RequestDelegate effect = context => { effects++; context.Response.StatusCode = StatusCodes.Status200OK; return Task.CompletedTask; };
            RouteEndpoint initial = new(effect, RoutePatternFactory.Parse("/project"), 0,
                new EndpointMetadataCollection(authorization, new HttpMethodMetadata(["POST"])), "finite-project");
            using var source = new FiniteReceiverMutableEndpointSource(initial);
            ((IEndpointRouteBuilder)app).DataSources.Add(source);
            FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(app,
                endpoint => endpoint.RoutePattern.RawText == "/project" ? "domain-service:project" : null);
            await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false })
            {
                BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()),
            };
            client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, "finite-receiver-channel");
            using (HttpResponseMessage original = await client.PostAsync("/project", null, TestContext.Current.CancellationToken).ConfigureAwait(true))
                original.StatusCode.ShouldBe(HttpStatusCode.OK);
            string path = change is "descendant" or "pattern" ? "/project/extra" : "/project";
            if (change == "policy") authorization.Policy = "finite-changed";
            else
            {
                EndpointMetadataCollection metadata = change == "anonymous"
                    ? new EndpointMetadataCollection(authorization, new HttpMethodMetadata(["POST"]), new AllowAnonymousAttribute())
                    : new EndpointMetadataCollection(authorization, new HttpMethodMetadata([change == "verb" ? "GET" : "POST"]));
                source.Replace(new RouteEndpoint(effect, RoutePatternFactory.Parse(change == "pattern" ? "/project/{**rest}" : path), 0, metadata, "finite-replacement"));
            }
            using var request = new HttpRequestMessage(change == "verb" ? HttpMethod.Get : HttpMethod.Post, path);
            using HttpResponseMessage refused = await client.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(true);
            refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            effects.ShouldBe(1);
            app.Services.GetRequiredService<FiniteRouteCatalogReceiver>().Observe().Endpoints.Single().Admitted.ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Refuses unbounded root grants before a host can listen with incomplete prefix coverage.</summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/*")]
    [InlineData("*")]
    public async Task RootGrantIsRefusedBeforeReceiverInstallation(string grant)
    {
        string directory = Path.Combine(Path.GetTempPath(), "finite-receiver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string acl = Path.Combine(directory, "acl.json");
        await File.WriteAllTextAsync(acl,
            "{\"spec\":{\"accessControl\":{\"policies\":[{\"operations\":[{\"name\":\"" + grant + "\",\"action\":\"allow\"}]}]}}}",
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        try
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
            builder.Logging.ClearProviders();
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = directory;
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"] = acl;
            builder.Configuration[DaprAppChannelToken.ConfigurationKey] = "finite-receiver-channel";
            builder.Services.AddSingleton<FiniteRouteCatalogReceiver>();
            await using WebApplication app = builder.Build();
            Should.Throw<RouteCatalogUnavailableException>(() =>
                FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(app, _ => "domain-service:process"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>A continually changing source cannot publish a stale cache or complete receiver admission.</summary>
    [Fact]
    public async Task SourceChangingDuringMaterializationCannotPublishCapturedAdmission()
    {
        string directory = Path.Combine(Path.GetTempPath(), "finite-receiver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string acl = Path.Combine(directory, "acl.json");
        await File.WriteAllTextAsync(acl,
            """{"spec":{"accessControl":{"policies":[{"operations":[{"name":"/project","action":"allow"}]}]}}}""",
            TestContext.Current.CancellationToken).ConfigureAwait(true);
        try
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = directory;
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"] = acl;
            builder.Configuration[DaprAppChannelToken.ConfigurationKey] = "finite-receiver-channel";
            builder.Services.AddSingleton<FiniteRouteCatalogReceiver>();
            await using WebApplication app = builder.Build();
            RouteEndpoint endpoint = new(_ => Task.CompletedTask, RoutePatternFactory.Parse("/project"), 0,
                new EndpointMetadataCollection(new AuthorizeAttribute("finite-test"), new HttpMethodMetadata(["POST"])), "flapping-source");
            using var source = new FiniteReceiverMutableEndpointSource(endpoint) { InvalidateDuringRead = true };
            ((IEndpointRouteBuilder)app).DataSources.Add(source);
            FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(app, _ => "domain-service:project");
            await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            FiniteRouteCatalogReceiver fence = app.Services.GetRequiredService<FiniteRouteCatalogReceiver>();
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() =>
                fence.WaitForCaptureAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
            Should.Throw<RouteCatalogUnavailableException>(() => fence.Observe());
            source.Reads.ShouldBe(3);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

}
