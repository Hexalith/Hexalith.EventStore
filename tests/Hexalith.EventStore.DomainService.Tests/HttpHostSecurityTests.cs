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
