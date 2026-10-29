using System.Net;

using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogControlTransportTests
{
    [Theory]
    [InlineData("GET", "internal/routing/catalog-generation", true)]
    [InlineData("POST", "internal/routing/catalog/prepare", true)]
    [InlineData("POST", "internal/routing/catalog/ready", true)]
    [InlineData("POST", "internal/routing/catalog/commit", true)]
    [InlineData("POST", "internal/routing/catalog/rollback", true)]
    [InlineData("POST", "internal/routing/catalog/reconcile", true)]
    [InlineData("POST", "internal/routing/catalog-generation", false)]
    [InlineData("GET", "internal/routing/catalog/prepare", false)]
    [InlineData("POST", "internal/routing/catalog/unknown", false)]
    [InlineData("POST", "internal/routing/catalog/commit/extra", false)]
    [InlineData("POST", "process", false)]
    public async Task StandardFactoryPipelineAllowsOnlyCanonicalControlMethodsBeforeActivation(string verb, string method, bool allowed)
    {
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromException<RouteCatalogDispatchLease>(new RouteCatalogUnavailableException("No active generation, or pending activation.")));
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var sink = builder.Build(); int requests = 0;
        sink.MapMethods("/{**method}", ["GET", "POST"], (HttpContext context) =>
        {
            requests++;
            context.Request.Headers["dapr-app-id"].ToString().ShouldBe("configured-host");
            context.Request.Headers["dapr-api-token"].ToString().ShouldBe("sdk-owned-test-token");
            context.Request.Headers.ContainsKey(RouteCatalogInvocationHandler.RootHeader).ShouldBeFalse();
            return Results.NoContent();
        });
        await sink.StartAsync();
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton(authority);
        services.AddEventStoreRouteCatalog(new ConfigurationBuilder().Build());
        services.Configure<RouteCatalogHostOptions>(options => options.ControlPlaneAppIds = ["configured-host"]);
        services.AddHttpClient("control", client => client.BaseAddress = new Uri(sink.Urls.Single())).AddEventStoreDaprServiceInvocation("configured-host", "sdk-owned-test-token");
        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("control");
        using var request = new HttpRequestMessage(new HttpMethod(verb), method);
        request.Headers.TryAddWithoutValidation("dapr-app-id", "untrusted-app"); request.Headers.TryAddWithoutValidation("dapr-api-token", "untrusted-token");
        request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.RootHeader, "stale-root");
        if (allowed)
        {
            using var response = await client.SendAsync(request); response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            authority.ReceivedCalls().ShouldBeEmpty();
        }
        else await Should.ThrowAsync<RouteCatalogUnavailableException>(() => client.SendAsync(request));
        requests.ShouldBe(allowed ? 1 : 0);
    }

    [Fact]
    public async Task CanonicalControlMethodCannotReachAnUnconfiguredRecipient()
    {
        var authority = Substitute.For<IRouteCatalogAuthority>();
        using var handler = new RouteCatalogInvocationHandler(authority, new SocketsHttpHandler());
        using var client = new HttpClient(handler);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => client.GetAsync("http://127.0.0.1:1/v1.0/invoke/unknown-host/method/internal/routing/catalog-generation"));
        authority.ReceivedCalls().ShouldBeEmpty();
    }
}
