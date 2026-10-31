using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Tests.Integration;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.SignalRHub;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.SignalR;

/// <summary>Checks that the Development-only proof receipt describes the actual bounded broadcast.</summary>
public sealed class SignalRRuntimeProofMetadataTests
{
    /// <summary>Includes JSON overhead when a reduced cap permits only one metadata entry.</summary>
    [Fact]
    public async Task BroadcastReceipt_ReducedSerializedCap_MatchesDeliveredMetadata()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
        });
        _ = builder.WebHost.UseTestServer();
        _ = builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventStore:SignalR:RuntimeProof:Enabled"] = "true",
        });
        _ = builder.Services.AddSingleton<IOptions<SignalROptions>>(Options.Create(new SignalROptions
        {
            MaxDetailMetadataBytes = 9,
        }));
        IProjectionChangedBroadcaster broadcaster = Substitute.For<IProjectionChangedBroadcaster>();
        _ = builder.Services.AddSingleton(broadcaster);
        _ = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new()
            {
                ValidIssuer = "hexalith-dev",
                ValidAudience = "hexalith-eventstore",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthenticationTestEnvironment.SigningKey)),
            };
        });
        _ = builder.Services.AddAuthorization(options => options.AddPolicy(
            EventStoreHttpHostSecurityExtensions.HumanPolicy,
            EventStoreHttpHostSecurityExtensions.CreateHumanPolicy()));
        await using WebApplication application = builder.Build();
        _ = application.UseAuthentication();
        _ = application.UseAuthorization();
        _ = application.MapSignalRRuntimeProofEndpoints();
        await application.StartAsync();
        using HttpClient client = application.GetTestClient();
        var request = new SignalRRuntimeProofBroadcastRequest("counter", "tenant-a", "scope", new Dictionary<string, string>
        {
            ["a"] = "1",
            ["b"] = "2",
        });

        using HttpResponseMessage anonymous = await client.PostAsJsonAsync("/_test/signalr/runtime-proof/broadcast", request);
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtHelper.GenerateToken());
        using HttpResponseMessage response = await client.PostAsJsonAsync("/_test/signalr/runtime-proof/broadcast", request);

        _ = response.EnsureSuccessStatusCode();
        SignalRRuntimeProofBroadcastResult? result = await response.Content.ReadFromJsonAsync<SignalRRuntimeProofBroadcastResult>();
        result.ShouldNotBeNull();
        result.MetadataCount.ShouldBe(1);
        await broadcaster.Received(1).BroadcastChangedAsync(Arg.Is<ProjectionChangedDetail>(detail =>
            detail.Metadata.Count == result.MetadataCount && detail.Metadata["a"] == "1"
            && JsonSerializer.SerializeToUtf8Bytes(detail.Metadata, (JsonSerializerOptions?)null).Length == 9), Arg.Any<CancellationToken>());
    }
}
