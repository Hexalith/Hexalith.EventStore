using System.Net;
using Hexalith.EventStore.DomainService.Tests.Fixtures;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

/// <summary>Exercises strict SDK classification and the channel boundary with real routing and authorization.</summary>
public sealed class HttpHostSecurityTests
{
    /// <summary>Verifies unclassified Endpoints Fail Startup In Either Mapping Order.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnclassifiedEndpointsFailStartupInEitherMappingOrder(bool beforeSdk)
    {
        await using WebApplication app = Build();
        if (beforeSdk) app.MapGet("/unclassified", () => Results.Ok());
        app.UseEventStoreDomainService();
        if (!beforeSdk) app.MapGet("/unclassified", () => Results.Ok());
        await Should.ThrowAsync<InvalidOperationException>(() => app.StartAsync(TestContext.Current.CancellationToken)).ConfigureAwait(true);
    }

    /// <summary>Verifies subscription Discovery Requires Actual Channel Token.</summary>
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("wrong", HttpStatusCode.Unauthorized)]
    [InlineData("story514-sdk-channel", HttpStatusCode.OK)]
    public async Task SubscriptionDiscoveryRequiresActualChannelToken(string? token, HttpStatusCode expected)
    {
        await using WebApplication app = Build();
        app.MapSubscribeHandler().RequireEventStoreSidecarChannel();
        app.UseEventStoreDomainService();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        using HttpClient client = app.GetTestClient();
        if (token is not null) client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, token);
        using HttpResponseMessage response = await client.GetAsync("/dapr/subscribe", TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(expected);
    }

    /// <summary>Actual topic metadata cannot turn an anonymous probe route into an unguarded delivery endpoint.</summary>
    [Fact]
    public async Task TopicMarkedHealthHandlerIsDeniedBeforeExecution()
    {
        await using WebApplication app = Build();
        app.UseRouting();
        app.UseEventStoreHttpHostSecurity();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/alive", () => Results.Ok()).AllowAnonymous();
        await app.StartAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        int calls = 0;
        app.MapPost("/health", () => { calls++; return Results.Ok(); })
            .WithTopic("pubsub", "review.health").AllowAnonymous();
        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage response = await client.PostAsync("/health", new StringContent("{}"), TestContext.Current.CancellationToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        calls.ShouldBe(0);
    }

    private static WebApplication Build()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [DaprAppChannelToken.ConfigurationKey] = "story514-sdk-channel",
            ["EventStore:DomainService:AppId"] = "sample",
            ["Authentication:JwtBearer:Issuer"] = "story514", ["Authentication:JwtBearer:Audience"] = "sample",
            ["Authentication:JwtBearer:SigningKey"] = "story514-sdk-signing-key-at-least-32-bytes",
        });
        builder.AddEventStoreDomainService(typeof(WidgetAggregate).Assembly);
        return builder.Build();
    }
}
