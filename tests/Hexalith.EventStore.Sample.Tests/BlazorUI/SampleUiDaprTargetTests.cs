extern alias BlazorUI;

using System.Net;

using BlazorUI::Hexalith.EventStore.Sample.BlazorUI.Services;
using Hexalith.EventStore.Client.Gateway;
using Shouldly;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Sample.Tests.BlazorUI;

/// <summary>Exercises the real UI registration, bearer handler and SDK invocation path.</summary>
public sealed class SampleUiDaprTargetTests
{
    [Theory]
    [InlineData(null, "eventstore")]
    [InlineData("eventstore-session-a", "eventstore-session-a")]
    public async Task RegisteredSampleClientUsesConfiguredTargetAndBearer(string? configured, string expected)
    {
        WebApplicationBuilder builder = Builder(configured);
        builder.Services.AddSampleUiGatewayClient(builder.Configuration).ConfigurePrimaryHttpMessageHandler(() => new SampleUiDaprTargetHandler(expected, builder.Configuration["DAPR_API_TOKEN"]!));
        await using WebApplication app = builder.Build();
        IEventStoreGatewayClient client = app.Services.GetRequiredService<IEventStoreGatewayClient>();
        (await client.GetCommandStatusAsync("synthetic-command", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("eventstore admin")]
    [InlineData("eventstore/admin")]
    [InlineData("eventstore:admin")]
    public void InvalidTargetFailsDuringRegistration(string invalid)
    {
        WebApplicationBuilder builder = Builder(invalid);
        Should.Throw<ArgumentException>(() => builder.Services.AddSampleUiGatewayClient(builder.Configuration));
    }

    private static WebApplicationBuilder Builder(string? target)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventStore:GatewayAppId"] = target,
            ["DAPR_HTTP_ENDPOINT"] = "http://127.0.0.1:3500", ["DAPR_API_TOKEN"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            ["EventStore:Authentication:Issuer"] = "synthetic-issuer", ["EventStore:Authentication:Audience"] = "synthetic-audience",
            ["EventStore:Authentication:SigningKey"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            ["EventStore:Authentication:Subject"] = "synthetic-user", ["EventStore:Authentication:Tenants:0"] = "tenant-a",
            ["EventStore:Authentication:Domains:0"] = "counter", ["EventStore:Authentication:Permissions:0"] = "admin:read",
        });
        return builder;
    }
}
