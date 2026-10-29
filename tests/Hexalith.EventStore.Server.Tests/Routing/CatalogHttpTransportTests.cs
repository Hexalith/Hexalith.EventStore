using System.Net;

using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogHttpTransportTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FinalOwnedDaprTargetIsValidatedAtActualHttpSinkWithoutMovingSdkOwner(bool dynamicTarget, bool unknownTarget)
    {
        var fixture = new RouteCatalogTestFixture();
        var candidate = fixture.Candidate(); await fixture.ActivateAsync(candidate);
        var target = fixture.Envelope.Routes.First(r => fixture.Envelope.Declarations.Any(d => d.RouteId == r.RouteId && d.Kind == "query" && d.Execution!.TargetKind == "domain-service"));
        string selected = unknownTarget ? "undeclared-app" : target.AppId;
        string? receivedApp = null, receivedToken = null, receivedRoot = null, receivedEpoch = null;
        int effects = 0;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var sink = builder.Build();
        sink.MapPost("/" + target.Method, (HttpContext context) =>
        {
            Interlocked.Increment(ref effects);
            receivedApp = context.Request.Headers["dapr-app-id"].ToString();
            receivedToken = context.Request.Headers["dapr-api-token"].ToString();
            receivedRoot = context.Request.Headers[RouteCatalogInvocationHandler.RootHeader].ToString();
            receivedEpoch = context.Request.Headers[RouteCatalogInvocationHandler.EpochHeader].ToString();
            return Results.NoContent();
        });
        await sink.StartAsync();
        var services = new ServiceCollection();
        var capture = new CatalogHandlerOrderCapture();
        services.AddSingleton<IHttpMessageHandlerBuilderFilter>(capture);
        services.AddSingleton<IRouteCatalogAuthority>(fixture.Authorities[0]);
        services.AddEventStoreRouteCatalog(new ConfigurationBuilder().Build());
        var clientBuilder = services.AddHttpClient("final-target", client => client.BaseAddress = new Uri(sink.Urls.Single()));
        if (dynamicTarget) clientBuilder.AddEventStoreDaprServiceInvocation(_ => selected, state => state, _ => "owned-token");
        else clientBuilder.AddEventStoreDaprServiceInvocation(selected, "owned-token");
        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("final-target");
        // Framework HTTP logging already surrounds the transport; the SDK remains the last application delegating handler.
        capture.Handlers.Last(type => type.Namespace?.StartsWith("Hexalith.", StringComparison.Ordinal) == true).Name.ShouldBe("DaprServiceInvocationHandler");
        capture.Handlers.ShouldNotContain(typeof(RouteCatalogInvocationHandler));
        capture.Transport.ShouldBe(typeof(RouteCatalogInvocationHandler));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/" + target.Method);
        request.Headers.TryAddWithoutValidation("dapr-app-id", unknownTarget ? target.AppId : "caller-undeclared-app");
        request.Headers.TryAddWithoutValidation("dapr-api-token", "caller-token");
        request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.RootHeader, "caller-root");
        request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.EpochHeader, "999");
        if (unknownTarget)
        {
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => client.SendAsync(request));
            effects.ShouldBe(0);
        }
        else
        {
            using var response = await client.SendAsync(request);
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent); effects.ShouldBe(1);
            receivedApp.ShouldBe(target.AppId); receivedToken.ShouldBe("owned-token");
            receivedRoot.ShouldBe(candidate.RootDigest); receivedEpoch.ShouldBe("1");
        }
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }
}
