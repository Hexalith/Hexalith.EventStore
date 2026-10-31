using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Hexalith.EventStore.Admin.Server.Tests.Routing;

/// <summary>Actual factory socket reads retain independent owner credentials and never borrow the Admin channel.</summary>
public sealed class DevelopmentActorStateOwnerBindingTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("configured-token")]
    [InlineData("effective-token")]
    [InlineData("effective-endpoint")]
    [InlineData("missing-receipt")]
    [InlineData("changed-receipt")]
    [InlineData("extra-owner")]
    [InlineData("native-endpoint")]
    public async Task IndependentOwnerHandoffBindsMetadataBeforeTransportAndContainsNoCredential(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        string secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        string replacement = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var owner = builder.Build(); int calls = 0;
        owner.MapGet("/v1.0/metadata", (HttpContext context) =>
        {
            context.Request.Headers["dapr-api-token"].ToString().ShouldBe(secret); Interlocked.Increment(ref calls);
            return Results.Json(new { id = "domain-test" });
        });
        await owner.StartAsync(TestContext.Current.CancellationToken);
        string endpoint = owner.Urls.Single();
        await using var host = new TechnicalAdminHost(); await host.StartAsync(ownerEndpoint: endpoint, ownerToken: secret);
        var observation = await DevelopmentRouteCatalogInputs.CaptureAsync(host.Configuration, "admin-test", [], [], [], [], [], TestContext.Current.CancellationToken);
        string authentication = Encoding.UTF8.GetString(observation.AuthenticationProfile);
        authentication.ShouldContain("actorStateOwners"); authentication.ShouldContain(endpoint); authentication.ShouldNotContain(secret);
        var before = (await host.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Committed;
        if (scenario == "configured-token") host.Configuration["EventStore:RouteCatalog:DevelopmentActorStateOwners:0:ApiToken"] = replacement;
        if (scenario == "effective-token") host.Options.DevelopmentActorStateOwners[0].ApiToken = replacement;
        if (scenario == "effective-endpoint") host.Options.DevelopmentActorStateOwners[0].HttpEndpoint = "http://localhost:1/";
        if (scenario == "missing-receipt") File.Delete(Path.Combine(host.DirectoryPath, "admin.actor-state-owners.json"));
        if (scenario == "changed-receipt") await File.WriteAllBytesAsync(Path.Combine(host.DirectoryPath, "admin.actor-state-owners.json"),
            JsonSerializer.SerializeToUtf8Bytes(new[] { new DevelopmentActorStateOwnerObservation("domain-test", "http://localhost:1/", true) }), TestContext.Current.CancellationToken);
        if (scenario == "extra-owner")
        {
            var additional = new DevelopmentActorStateOwner { AppId = "foreign", HttpEndpoint = endpoint };
            additional.ApiToken = secret;
            host.Options.DevelopmentActorStateOwners = [.. host.Options.DevelopmentActorStateOwners, additional];
        }
        if (scenario == "native-endpoint") host.Configuration["AdminServer:EventStoreDaprHttpEndpoint"] = "http://localhost:1/";
        using var client = host.CreateOwnerClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint + "/v1.0/metadata");
        if (scenario == "valid")
        {
            using var response = await DevelopmentActorStateTransport.SendAsync(client, request, "domain-test", TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK); calls.ShouldBe(1);
        }
        else
        {
            var refusal = await Should.ThrowAsync<RouteCatalogUnavailableException>(() => DevelopmentActorStateTransport.SendAsync(client, request, "domain-test", TestContext.Current.CancellationToken));
            refusal.Message.ShouldContain("owner"); calls.ShouldBe(0);
        }
        var after = (await host.Store.ReadAsync(TestContext.Current.CancellationToken)).State;
        after.Committed.ShouldBe(before); after.Leases.ShouldBeEmpty(); after.Pending.ShouldBeNull();
    }
}
