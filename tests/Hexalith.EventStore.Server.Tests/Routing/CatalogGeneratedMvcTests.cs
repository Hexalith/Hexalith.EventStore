using System.Net;

using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogGeneratedMvcTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratedTenantRefusalEmitsBoundedDiagnosticBeforeDownstreamEffects(bool mismatch)
    {
        var fixture = new RouteCatalogTestFixture();
        var envelope = Contracts.Routing.RouteCatalogCodec.Seal(fixture.Envelope with
        {
            Routes = fixture.Envelope.Routes.Select(route => route.RouteId == "counter-increment-counter" && mismatch
                ? route with { ResolverTenantPattern = "tenant-b" } : route).ToArray(),
        });
        var snapshot = new CommittedRouteCatalogSnapshot(Contracts.Routing.RouteCatalogCodec.Encode(envelope), Contracts.Routing.RouteCatalogCodec.ComputeRootDigest(envelope), 1);
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ => new RouteCatalogDispatchLease(snapshot, () => Task.CompletedTask));
        using var capture = new CatalogRefusalLogCapture();
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.Logging.AddProvider(capture); builder.WebHost.UseTestServer();
        var counter = new CatalogGeneratedEffectCounter(); builder.Services.AddSingleton(counter); builder.Services.AddSingleton(authority);
        builder.Services.AddScoped<GeneratedRouteCatalogFilter>();
        builder.Services.AddControllers(options => options.Filters.AddService<GeneratedRouteCatalogFilter>())
            .AddApplicationPart(typeof(CatalogGeneratedMvcProbeController).Assembly)
            .ConfigureApplicationPartManager(parts => parts.FeatureProviders.Add(new CatalogGeneratedMvcFeature()));
        await using var app = builder.Build(); app.MapControllers(); await app.StartAsync();
        using var client = app.GetTestClient(); using var response = await client.GetAsync("/catalog-generated-probe/valid");
        response.StatusCode.ShouldBe(mismatch ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
        counter.Effects.ShouldBe(mismatch ? 0 : 1);
        var refusals = capture.Messages.Where(message => message.StartsWith("Route catalog refusal:", StringComparison.Ordinal)).ToArray();
        if (mismatch)
        {
            refusals.ShouldHaveSingleItem().ShouldBe("Route catalog refusal: Boundary=generated-receiver, Reason=catalog-disagreement");
            capture.Exceptions.ShouldAllBe(exception => exception == null);
        }
        else refusals.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("missing", HttpStatusCode.ServiceUnavailable, null)]
    [InlineData("valid", HttpStatusCode.OK, "validated")]
    [InlineData("early-bad-request", HttpStatusCode.BadRequest, "bad aggregate identity")]
    [InlineData("early-forbidden", HttpStatusCode.Forbidden, "authority denied")]
    [InlineData("early-error", HttpStatusCode.InternalServerError, "upstream failure")]
    public async Task ActualMvcRequiresTheGeneratedValidationCallbackAndPreservesEarlyFailures(string action, HttpStatusCode expected, string? content)
    {
        var fixture = new RouteCatalogTestFixture();
        var candidate = fixture.Candidate();
        var snapshot = new CommittedRouteCatalogSnapshot(candidate.CanonicalCatalog, candidate.RootDigest, 1);
        int outstanding = 0;
        IRouteCatalogAuthority authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            outstanding++;
            return new RouteCatalogDispatchLease(snapshot, () => { outstanding--; return Task.CompletedTask; });
        });
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(authority);
        builder.Services.AddScoped<GeneratedRouteCatalogFilter>();
        builder.Services.AddControllers(options => options.Filters.AddService<GeneratedRouteCatalogFilter>())
            .AddApplicationPart(typeof(CatalogGeneratedMvcProbeController).Assembly)
            .ConfigureApplicationPartManager(parts => parts.FeatureProviders.Add(new CatalogGeneratedMvcFeature()));
        await using var app = builder.Build();
        app.MapControllers();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/catalog-generated-probe/" + action, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(expected);
        if (content is not null) (await response.Content.ReadAsStringAsync()).ShouldContain(content);
        outstanding.ShouldBe(0);
        await authority.Received(1).AcquireAsync(Arg.Any<CancellationToken>());
    }
}
