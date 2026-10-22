using System.Security.Cryptography;

using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.ServiceDefaults.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Configuration;

/// <summary>Proves custom SDK hosts using Dapr's original mapper receive actor isolation without private metadata.</summary>
public sealed class CustomHostActorCallbackRateLimitTests
{
    [Fact]
    public async Task RawDaprMappingUsesTrustedDelegateProvenanceForIndependentBoundedQuotasAsync()
    {
        string ChannelToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();
        // Unused controller/command services are outside this minimal mapping-only host.
        builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [DaprAppChannelToken.ConfigurationKey] = ChannelToken,
            ["Authentication:JwtBearer:Issuer"] = "custom-host-fixture",
            ["Authentication:JwtBearer:Audience"] = "hexalith-eventstore",
            ["Authentication:JwtBearer:SigningKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            ["Authentication:JwtBearer:RequireHttpsMetadata"] = "false",
            ["EventStore:RateLimiting:PermitLimit"] = "2",
            ["EventStore:RateLimiting:ConsumerPermitLimit"] = "2",
            ["EventStore:RateLimiting:SegmentsPerWindow"] = "1",
            ["EventStore:RateLimiting:ConsumerSegmentsPerWindow"] = "1",
            ["EventStore:RateLimiting:ConsumerWindowSeconds"] = "60",
        });
        builder.Services.AddEventStore();
        builder.Services.AddActors(options => options.Actors.RegisterActor<IdempotencyAdmissionActor>());
        // This custom host exercises HTTP mapping and middleware only; it owns no application background workers.
        builder.Services.RemoveAll<IHostedService>();
        await using WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapActorsHandlers();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(DaprAppChannelToken.HeaderName, ChannelToken);
        using HttpResponseMessage first = await client.DeleteAsync("/actors/IdempotencyAdmissionActor/custom-a");
        using HttpResponseMessage second = await client.DeleteAsync("/actors/IdempotencyAdmissionActor/custom-a");
        using HttpResponseMessage saturated = await client.DeleteAsync("/actors/IdempotencyAdmissionActor/custom-a");
        using HttpResponseMessage other = await client.DeleteAsync("/actors/IdempotencyAdmissionActor/custom-b");
        first.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        second.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        saturated.StatusCode.ShouldBe(System.Net.HttpStatusCode.TooManyRequests);
        other.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        RouteEndpoint[] genuine = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Where(endpoint => endpoint.RoutePattern.Parameters.Any(parameter => parameter.Name == "actorId")).ToArray();
        genuine.Length.ShouldBe(4);
        var context = new DefaultHttpContext { RequestServices = app.Services };
        context.Request.RouteValues["actorTypeName"] = "IdempotencyAdmissionActor";
        context.Request.RouteValues["actorId"] = "custom-c";
        foreach (RouteEndpoint endpoint in genuine)
        {
            endpoint.Metadata.GetMetadata<EventStoreActorCallbackEndpointMetadata>().ShouldBeNull();
            context.Request.Method = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Single();
            context.Request.Headers[DaprAppChannelToken.HeaderName] = ChannelToken;
            context.SetEndpoint(endpoint);
            EventStoreRateLimitPartition.TrustedActorKey(context).ShouldNotBeNull();
            context.Request.Headers.Remove(DaprAppChannelToken.HeaderName);
            EventStoreRateLimitPartition.TrustedActorKey(context).ShouldBeNull();
            context.Request.Headers[DaprAppChannelToken.HeaderName] = ChannelToken;
            context.SetEndpoint(new RouteEndpoint(_ => Task.CompletedTask,
                RoutePatternFactory.Parse(endpoint.RoutePattern.RawText!), endpoint.Order, endpoint.Metadata, endpoint.DisplayName));
            EventStoreRateLimitPartition.TrustedActorKey(context).ShouldBeNull();
        }
    }
}
