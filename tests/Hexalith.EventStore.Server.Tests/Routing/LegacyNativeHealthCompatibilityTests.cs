using System.Security.Cryptography;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Qualifies only the actual SDK health delegate's closed finite method exception, not distributed activation.</summary>
public sealed class LegacyNativeHealthCompatibilityTests
{
    [Fact]
    public async Task ActualNativeHealthWithoutMethodMetadataRetainsGetOnlyAndRefusesCopiedDelegates()
    {
        string directory = Directory.CreateTempSubdirectory("legacy-native-health-").FullName;
        try
        {
            string path = Path.Combine(directory, "acl.json");
            await File.WriteAllTextAsync(path,
                """{"spec":{"accessControl":{"policies":[{"operations":[{"name":"/healthz","action":"allow","httpVerb":["GET"]}]}]}}}""");
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = directory;
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"] = path;
            builder.Configuration[DaprAppChannelToken.ConfigurationKey] = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            builder.Services.AddActors(options => options.Actors.RegisterActor<AggregateActor>());
            builder.Services.AddHealthChecks();
            builder.Services.AddEventStoreHttpHostSecurity();
            builder.Services.AddSingleton<FiniteRouteCatalogReceiver>();
            builder.Services.AddSingleton<IDevelopmentNativeActorReceiver, DevelopmentNativeActorReceiver>();
            await using var app = builder.Build();
            app.MapActorsHandlers().RequireEventStoreChannel();
            FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(app, DevelopmentTechnicalHttpTransport.Operation, technical: true);
            var health = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
                .Single(endpoint => endpoint.RoutePattern.RawText == "/healthz");
            health.Metadata.GetMetadata<IHttpMethodMetadata>().ShouldBeNull();
            var native = app.Services.GetRequiredService<IDevelopmentNativeActorReceiver>().Observe(health).ShouldNotBeNull();
            native.Kind.ShouldBe("health"); native.Verbs.ShouldBe("GET");
            var finite = app.Services.GetRequiredService<FiniteRouteCatalogReceiver>();
            finite.Capture([health], path, DevelopmentTechnicalHttpTransport.Operation);
            finite.Observe().Endpoints.ShouldHaveSingleItem().Methods.ShouldBe(["GET"]);
            finite.Allows("/healthz", "GET", health).ShouldBeTrue();
            foreach (string method in new[] { "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" }) finite.Allows("/healthz", method, health).ShouldBeFalse();
            var copied = new RouteEndpoint(_ => Task.CompletedTask, health.RoutePattern, health.Order, health.Metadata, health.DisplayName);
            finite.Allows("/healthz", "GET", copied).ShouldBeFalse();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
