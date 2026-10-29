using System.Net;
using System.Text.Json;

using Hexalith.EventStore.ErrorHandling;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogBackendHttpTests
{
    [Fact]
    public async Task ConcreteStateTransportPreservesCallerCancellationWithoutMutation()
    {
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var backend = builder.Build(); int mutations = 0;
        backend.MapGet("/v1.0/state/route-catalog/route-catalog-authority-v1", async (HttpContext context) =>
        { await Task.Delay(TimeSpan.FromSeconds(10), context.RequestAborted); return Results.NoContent(); });
        backend.MapPost("/v1.0/state/route-catalog", () => { mutations++; return Results.NoContent(); });
        await backend.StartAsync();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var clients = Substitute.For<IHttpClientFactory>(); clients.CreateClient("route-catalog-state").Returns(client);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DAPR_HTTP_ENDPOINT"] = backend.Urls.Single() }).Build();
        var store = new DaprRouteCatalogStateStore(clients, configuration, Options.Create(new RouteCatalogHostOptions()));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Should.ThrowAsync<OperationCanceledException>(() => store.ReadAsync(cancellation.Token));
        cancellation.IsCancellationRequested.ShouldBeTrue(); mutations.ShouldBe(0);
    }

    [Theory]
    [InlineData("healthy")]
    [InlineData("read-500")]
    [InlineData("mutation-500")]
    [InlineData("unusable")]
    [InlineData("missing-fields")]
    [InlineData("invalid-catalog")]
    [InlineData("timeout")]
    public async Task ConcreteAuthorityBackendFailuresBecomeNonsecretGatewayProblemDetails(string fault)
    {
        const string secret = "backend-payload-private-coordinates-proof-secret";
        var backendBuilder = WebApplication.CreateBuilder(); backendBuilder.Logging.ClearProviders(); backendBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var backend = backendBuilder.Build(); RouteCatalogActivationState persisted = RouteCatalogActivationState.Empty; int epoch = 1, stateWrites = 0;
        backend.MapGet("/v1.0/state/route-catalog/route-catalog-authority-v1", async (HttpContext context) =>
        {
            if (fault == "timeout") await Task.Delay(TimeSpan.FromSeconds(10), context.RequestAborted);
            context.Response.Headers.ETag = epoch.ToString();
            return fault == "read-500" ? Results.Text(secret, statusCode: 500)
                : fault == "missing-fields" ? Results.Text("{}", "application/json")
                : fault == "invalid-catalog" ? Results.Json(persisted with { Committed = persisted.Committed! with { CanonicalCatalog = "{}"u8.ToArray() } })
                : fault == "unusable" ? Results.Text(secret, "application/json") : Results.Json(persisted);
        });
        backend.MapPost("/v1.0/state/route-catalog", async (HttpContext context) =>
        {
            if (fault == "mutation-500") return Results.Text(secret, statusCode: 500);
            using var document = await JsonDocument.ParseAsync(context.Request.Body);
            persisted = document.RootElement[0].GetProperty("value").Deserialize<RouteCatalogActivationState>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            epoch++; stateWrites++; return Results.NoContent();
        });
        await backend.StartAsync(); string endpoint = backend.Urls.Single() + "/";
        var fixture = new RouteCatalogTestFixture(new("route-catalog", "route-catalog-authority-v1", endpoint)); await fixture.ActivateAsync(fixture.Candidate());
        persisted = (await fixture.Store.ReadAsync(default)).State;
        var options = Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts[0], Profile = "Development", DevelopmentFixture = true });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DAPR_HTTP_ENDPOINT"] = endpoint }).Build();
        using var stateClient = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = fault == "timeout" ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(10) };
        var clients = Substitute.For<IHttpClientFactory>(); clients.CreateClient("route-catalog-state").Returns(stateClient);
        var store = new DaprRouteCatalogStateStore(clients, configuration, options);
        var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns("Development");
        var authority = new RouteCatalogAuthority(store, new DevelopmentRouteCatalogTrustValidator(options, environment), options,
            new ObservedRouteCatalogHost(_ => Task.FromResult(fixture.Observations[0])));
        var gatewayBuilder = WebApplication.CreateBuilder(); gatewayBuilder.Logging.ClearProviders(); gatewayBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        gatewayBuilder.Services.AddExceptionHandler<RouteCatalogUnavailableExceptionHandler>(); gatewayBuilder.Services.AddProblemDetails();
        await using var gateway = gatewayBuilder.Build(); gateway.UseExceptionHandler(); int effects = 0;
        gateway.MapGet("/request", async () => { await using var lease = await authority.AcquireAsync(); effects++; return Results.NoContent(); });
        await gateway.StartAsync(); using var caller = new HttpClient(); using var response = await caller.GetAsync(gateway.Urls.Single() + "/request");
        response.StatusCode.ShouldBe(fault == "healthy" ? HttpStatusCode.NoContent : HttpStatusCode.ServiceUnavailable);
        if (fault != "healthy")
        {
            response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
            string body = await response.Content.ReadAsStringAsync(); body.ShouldNotContain(secret); body.ShouldNotContain(endpoint); body.ShouldNotContain("correlationId");
            using var problem = JsonDocument.Parse(body); problem.RootElement.GetProperty("status").GetInt32().ShouldBe(503);
        }
        effects.ShouldBe(fault == "healthy" ? 1 : 0); stateWrites.ShouldBe(fault == "healthy" ? 2 : 0);
        persisted.Leases.ShouldBeEmpty(); persisted.Committed!.RootDigest.ShouldBe(fixture.Candidate().RootDigest);
    }
}
