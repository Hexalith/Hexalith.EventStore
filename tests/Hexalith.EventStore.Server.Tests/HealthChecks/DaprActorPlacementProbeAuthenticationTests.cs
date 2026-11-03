using System.Net;
using System.Security.Cryptography;
using System.Text;

using Hexalith.EventStore.HealthChecks;
using Hexalith.EventStore.Server.Tests.Streams;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.HealthChecks;

public class DaprActorPlacementProbeAuthenticationTests
{
    private const string ConnectedMetadata =
        """{"actorRuntime":{"runtimeStatus":"RUNNING","hostReady":true,"placement":"placement: connected"}}""";

    [Fact]
    public async Task AuthenticatedMetadataRequiresTheConfiguredSidecarCredential()
    {
        string sidecarToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using WebApplication app = builder.Build();
        app.MapGet("/v1.0/metadata", (HttpRequest request) =>
            request.Headers["dapr-api-token"].ToString() == sidecarToken
                ? Results.Content(ConnectedMetadata, "application/json")
                : Results.StatusCode(StatusCodes.Status401Unauthorized));
        await app.StartAsync(TestContext.Current.CancellationToken);
        string endpoint = app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient();

        foreach (string? token in new[] { null, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), sidecarToken })
        {
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DAPR_HTTP_ENDPOINT"] = endpoint,
                ["DAPR_API_TOKEN"] = token,
            }).Build();
            var probe = new DaprActorPlacementProbe(client, configuration);
            var healthCheck = new DaprActorPlacementHealthCheck(probe);

            var context = new HealthCheckContext
            {
                Registration = new HealthCheckRegistration("dapr-actor-placement", healthCheck, HealthStatus.Unhealthy, ["ready"]),
            };
            HealthCheckResult result = await healthCheck.CheckHealthAsync(context, TestContext.Current.CancellationToken);

            if (token == sidecarToken)
            {
                result.Status.ShouldBe(HealthStatus.Healthy);
            }
            else
            {
                result.Status.ShouldBe(HealthStatus.Unhealthy);
                result.Exception.ShouldBeOfType<HttpRequestException>().StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            }
        }

        client.DefaultRequestHeaders.Contains("dapr-api-token").ShouldBeFalse();
    }

    [Fact]
    public async Task ConfiguredCredentialOverridesClientDefaultsOnlyForTheMetadataRequest()
    {
        using var handler = new NativeSourceMetadataHttpHandler(Encoding.UTF8.GetBytes(ConnectedMetadata));
        using var client = new HttpClient(handler);
        string defaultToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        string sidecarToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        client.DefaultRequestHeaders.Add("dapr-api-token", defaultToken);
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DAPR_API_TOKEN"] = sidecarToken,
        }).Build();

        DaprActorPlacementStatus status = await new DaprActorPlacementProbe(client, configuration)
            .CheckAsync(TestContext.Current.CancellationToken);

        status.HostReady.ShouldBeTrue();
        handler.ApiToken.ShouldBe(sidecarToken);
        client.DefaultRequestHeaders.GetValues("dapr-api-token").ShouldBe([defaultToken]);
    }

    [Theory]
    [InlineData("http://127.0.0.1:4800/", "4801", "http://127.0.0.1:4800/v1.0/metadata")]
    [InlineData(null, "4801", "http://localhost:4801/v1.0/metadata")]
    [InlineData(null, null, "http://localhost:3500/v1.0/metadata")]
    public async Task CredentialUsesOnlyTheConfiguredMetadataEndpoint(string? endpoint, string? port, string expected)
    {
        string sidecarToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        using var handler = new NativeSourceMetadataHttpHandler(Encoding.UTF8.GetBytes(ConnectedMetadata));
        using var client = new HttpClient(handler);
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DAPR_HTTP_ENDPOINT"] = endpoint,
            ["DAPR_HTTP_PORT"] = port,
            ["DAPR_API_TOKEN"] = sidecarToken,
        }).Build();

        await new DaprActorPlacementProbe(client, configuration).CheckAsync(TestContext.Current.CancellationToken);

        handler.LastUri.ShouldBe(new Uri(expected));
        handler.ApiToken.ShouldBe(sidecarToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task MissingCredentialLeavesUnauthenticatedSidecarRequestsUnchanged(string? token)
    {
        using var handler = new NativeSourceMetadataHttpHandler(Encoding.UTF8.GetBytes(ConnectedMetadata));
        using var client = new HttpClient(handler);
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DAPR_API_TOKEN"] = token,
        }).Build();

        DaprActorPlacementStatus status = await new DaprActorPlacementProbe(client, configuration)
            .CheckAsync(TestContext.Current.CancellationToken);

        status.HostReady.ShouldBeTrue();
        handler.ApiToken.ShouldBeNull();
    }
}
