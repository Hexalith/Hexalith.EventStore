using System.Net;
using System.Security.Cryptography;

using Dapr;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Extensions;
using Hexalith.EventStore.Routing;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Both technical profiles refuse substituted interface observers before installing any startup handshake.</summary>
public sealed class LegacyTechnicalObserverEnrollmentTests
{
    [Theory]
    [InlineData(false, "native")]
    [InlineData(false, "callback")]
    [InlineData(false, "both")]
    [InlineData(true, "native")]
    [InlineData(true, "callback")]
    [InlineData(true, "both")]
    public async Task EnrollmentRequiresTheExistingClosedSdkObservers(bool composite, string substitution)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = Path.GetTempPath();
        builder.Configuration[DaprAppChannelToken.ConfigurationKey] = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        if (composite) builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:ReceiverProfile"] = RouteCatalogTechnicalHttpPolicy.FenceVersion;
        builder.Services.AddSingleton<FiniteRouteCatalogReceiver>();
        var native = new SubstitutedNativeCatalogObserver();
        var callback = new SubstitutedCallbackCatalogObserver();
        if (substitution is "native" or "both") builder.Services.AddSingleton<IDevelopmentNativeActorReceiver>(native);
        if (substitution is "callback" or "both") builder.Services.AddSingleton<IDevelopmentDaprCallbackReceiver>(callback);
        await using var app = builder.Build();
        var refusal = Should.Throw<RouteCatalogUnavailableException>(() =>
            FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(app, DevelopmentTechnicalHttpTransport.Operation, technical: true));
        refusal.Category.ShouldBe(RouteCatalogRefusalReason.Inventory);
        refusal.Message.ShouldContain("actual closed SDK implementations");
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/dapr/config")]
    [InlineData("/dapr/subscribe")]
    public async Task ActualSdkHandshakeStillRunsBeforeCatalogActivationWithItsChannelGuard(string path)
    {
        string directory = Directory.CreateTempSubdirectory("legacy-sdk-handshake-").FullName;
        try
        {
            string acl = Path.Combine(directory, "acl.json");
            await File.WriteAllTextAsync(acl, """{"spec":{"accessControl":{"policies":[{"operations":[{"name":"/dapr/**","action":"allow","httpVerb":["GET"]},{"name":"/healthz","action":"allow","httpVerb":["GET"]}]}]}}}""");
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.UseTestServer();
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = directory;
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"] = acl;
            string token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            builder.Configuration[DaprAppChannelToken.ConfigurationKey] = token;
            builder.Services.AddActors(options => options.Actors.RegisterActor<AggregateActor>());
            builder.Services.AddHealthChecks();
            builder.Services.AddEventStoreHttpHostSecurity();
            builder.Services.AddOptions<ProjectionChangeNotifierOptions>();
            builder.Services.AddControllers().AddApplicationPart(typeof(ProjectionNotificationController).Assembly).AddDapr()
                .ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new LegacyHandshakeControllerFeature()));
            builder.Services.AddSingleton<FiniteRouteCatalogReceiver>();
            builder.Services.AddSingleton<IDevelopmentNativeActorReceiver, DevelopmentNativeActorReceiver>();
            builder.Services.AddSingleton<IDevelopmentDaprCallbackReceiver, DevelopmentDaprCallbackReceiver>();
            await using var app = builder.Build();
            app.UseEventStoreHttpHostSecurity(); app.UseAuthorization();
            app.MapActorsHandlers().RequireEventStoreChannel();
            app.MapControllers().WithEventStoreProjectionNotifications(app.Services, false);
            app.MapSubscribeHandler().RequireEventStoreChannel();
            FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(app, DevelopmentTechnicalHttpTransport.Operation, technical: true);
            await app.StartAsync(TestContext.Current.CancellationToken);
            // No authority is registered: these are the real preactivation handshake delegates.
            using var client = app.GetTestClient();
            foreach (string? supplied in new string?[] { null, "unusable-channel-marker", token })
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, path);
                if (supplied is not null) request.Headers.Add(DaprAppChannelToken.HeaderName, supplied);
                using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
                response.StatusCode.ShouldBe(supplied == token ? HttpStatusCode.OK : HttpStatusCode.Unauthorized);
            }
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

}
