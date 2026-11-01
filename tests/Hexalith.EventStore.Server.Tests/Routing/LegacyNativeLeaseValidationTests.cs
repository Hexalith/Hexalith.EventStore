using System.Net;
using System.Security.Cryptography;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Exercises the existing native caller/method validation before finite lease acquisition.</summary>
public sealed class LegacyNativeLeaseValidationTests
{
    [Theory]
    [InlineData("same-app", 503, 1)]
    [InlineData("foreign-app", 403, 0)]
    [InlineData("foreign-namespace", 403, 0)]
    [InlineData("foreign-callee", 403, 0)]
    [InlineData("unknown-method", 403, 0)]
    [InlineData("unknown-actor", 403, 0)]
    public async Task ActualNativeValidationPrecedesLeaseRegardlessOfHttpPrefixAsync(string mode, int status, int expectedAcquisitions)
    {
        string directory = Directory.CreateTempSubdirectory("native-lease-validation-").FullName;
        try
        {
            string acl = Path.Combine(directory, "acl.json");
            await File.WriteAllTextAsync(acl, """{"spec":{"accessControl":{"policies":[{"operations":[{"name":"/api/v1/commands","action":"allow","httpVerb":["POST"]}]}]}}}""");
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.UseTestServer();
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:Directory"] = directory;
            builder.Configuration["EventStore:RouteCatalog:DevelopmentBootstrap:AccessControlPath"] = acl;
            builder.Configuration["EventStore:DomainService:AppId"] = "native-owned";
            builder.Configuration["NAMESPACE"] = "native-private";
            string token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            builder.Configuration[DaprAppChannelToken.ConfigurationKey] = token;
            builder.Services.AddActors(options => options.Actors.RegisterActor<AggregateActor>());
            builder.Services.AddHealthChecks();
            builder.Services.AddEventStoreHttpHostSecurity();
            builder.Services.AddSingleton<FiniteRouteCatalogReceiver>();
            builder.Services.AddSingleton<IDevelopmentNativeActorReceiver, DevelopmentNativeActorReceiver>();
            int acquired = 0;
            var authority = Substitute.For<IRouteCatalogAuthority>();
            authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            {
                Interlocked.Increment(ref acquired);
                return Task.FromException<RouteCatalogDispatchLease>(new RouteCatalogUnavailableException("No activation is supplied by this validation test."));
            });
            builder.Services.AddSingleton(authority);
            await using var app = builder.Build();
            app.UseEventStoreHttpHostSecurity(); app.UseAuthorization();
            app.MapActorsHandlers().RequireEventStoreChannel();
            FiniteRouteCatalogReceiver.UseFiniteDevelopmentRouteCatalogReceiver(app, DevelopmentTechnicalHttpTransport.Operation, technical: true);
            await app.StartAsync(TestContext.Current.CancellationToken);
            using var client = app.GetTestClient();
            using var request = new HttpRequestMessage(HttpMethod.Put, "/actors/" + (mode == "unknown-actor" ? "UnknownActor" : "AggregateActor")
                + "/owned/method/" + (mode == "unknown-method" ? "UnknownMethod" : "ProcessAuthorizedCommandAsync"));
            request.Headers.Add(DaprAppChannelToken.HeaderName, token);
            request.Headers.Add("dapr-caller-app-id", mode == "foreign-app" ? "other-app" : "native-owned");
            request.Headers.Add("dapr-caller-namespace", mode == "foreign-namespace" ? "other-namespace" : "native-private");
            request.Headers.Add("dapr-callee-app-id", mode == "foreign-callee" ? "other-app" : "native-owned");
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe((HttpStatusCode)status);
            acquired.ShouldBe(expectedAcquisitions);
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
