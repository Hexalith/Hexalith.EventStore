using System.Net;

using Hexalith.EventStore.Admin.UI;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Admin.UI.Tests.Services;

/// <summary>Exercises the real UI registration, bearer handler and SDK invocation path.</summary>
public sealed class AdminUiDaprTargetTests
{
    [Theory]
    [InlineData(null, "eventstore-admin")]
    [InlineData("eventstore-admin-session-a", "eventstore-admin-session-a")]
    public async Task RegisteredAdminClientUsesConfiguredTargetAndBearer(string? configured, string expected)
    {
        WebApplicationBuilder builder = Builder(configured);
        builder.AddAdminUI();
        builder.Services.AddHttpClient("AdminApi").ConfigurePrimaryHttpMessageHandler(() => new AdminUiDaprTargetHandler(expected));
        await using WebApplication app = builder.Build();
        using HttpClient client = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("AdminApi");
        using HttpResponseMessage response = await client.GetAsync("api/v1/admin/dapr/components", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("eventstore admin")]
    [InlineData("eventstore/admin")]
    [InlineData("eventstore:admin")]
    public void InvalidTargetFailsDuringRegistration(string invalid)
        => Should.Throw<ArgumentException>(() => Builder(invalid).AddAdminUI());

    private static WebApplicationBuilder Builder(string? target)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventStore:AdminServer:AppId"] = target,
            ["DAPR_HTTP_ENDPOINT"] = "http://127.0.0.1:3500", ["DAPR_API_TOKEN"] = "synthetic-sidecar-token",
            ["EventStore:Authentication:Issuer"] = "synthetic-issuer", ["EventStore:Authentication:Audience"] = "synthetic-audience",
            ["EventStore:Authentication:SigningKey"] = "synthetic-development-signing-key-32-bytes",
            ["EventStore:Authentication:Subject"] = "synthetic-user", ["EventStore:Authentication:Tenants:0"] = "tenant-a",
            ["EventStore:Authentication:Domains:0"] = "counter", ["EventStore:Authentication:Permissions:0"] = "admin:read",
        });
        return builder;
    }
}
