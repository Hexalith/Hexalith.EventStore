using System.Net;
using System.Text.Json;

using Hexalith.EventStore.Client.Discovery;
using Hexalith.EventStore.Client.Registration;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed partial class DomainServiceTrustBoundaryTests
{
    /// <summary>The finite SDK fence and bootstrap observation admit a nested callback only with an exact POST grant.</summary>
    [Theory]
    [InlineData("GET", false)]
    [InlineData("POST", true)]
    public async Task ExactDevelopmentPostGrantControlsNestedProjectionReceiver(string childVerb, bool admitted)
    {
        DirectoryInfo workspace = Directory.CreateTempSubdirectory("domain-finite-grants-");
        try
        {
            RouteCatalogDeclaredInvocation[] grants =
            [
                new(Caller, "project", ["POST"]),
                new(Caller, "project/v2", [childVerb]),
            ];
            string aclPath = Path.Combine(workspace.FullName, "access-control.json");
            await File.WriteAllTextAsync(aclPath, JsonSerializer.Serialize(new
            {
                spec = new
                {
                    accessControl = new
                    {
                        policies = new[]
                        {
                            new { operations = grants.Select(grant => new { name = grant.Method, httpVerb = grant.HttpMethods, action = "allow" }) },
                        },
                    },
                },
            }), TestContext.Current.CancellationToken);
            string componentPath = Path.Combine(workspace.FullName, "statestore.json");
            await File.WriteAllTextAsync(componentPath, "{\"kind\":\"Component\"}", TestContext.Current.CancellationToken);
            const string section = "EventStore:RouteCatalog:DevelopmentBootstrap:";
            Dictionary<string, string?> settings = new()
            {
                [section + "Directory"] = workspace.FullName,
                [section + "AccessControlPath"] = aclPath,
                [section + "ComponentPath"] = componentPath,
                [section + "HostId"] = AppId,
                [section + "CoordinatorHostId"] = "coordinator",
                [section + "AttemptId"] = "test-finite-grants",
                [section + "RequiredHostIds:0"] = AppId,
                [section + "RequiredHostIds:1"] = "coordinator",
                [section + "DeclaredInvocations"] = JsonSerializer.Serialize(grants, JsonSerializerOptions.Web),
            };
            await using var app = await StartAsync(settings: settings, configureServices: services =>
            {
                var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
                services.AddEventStoreDevelopmentDomainCatalog(configuration, restrictAuxiliaryToInvocations: true,
                    typeof(DomainServiceTrustBoundaryTests).Assembly);
                // A sidecar-originated domain-event delivery has no POST grant and stays admitted.
                services.AddEventStoreDomainEvents(typeof(DomainServiceTrustBoundaryTests).Assembly,
                    options => options.SubscriptionRoute = "/widget/events");
                // This fixture compiles no owner declarations, so the observed host keeps only undeclared-safe legacy projection domains.
                services.AddSingleton(new DiscoveryResult([], []));
                services.RemoveAll<IDomainQueryHandler>();
                services.RemoveAll<IAsyncDomainProjectionHandler>();
                // Keep the real finite receiver. There is no shared catalog backend in this endpoint test.
                services.Remove(services.Last(service => service.ServiceType == typeof(IHostedService)));
            });
            RouteCatalogReceiverSurface receiver = app.Services.GetRequiredService<FiniteRouteCatalogReceiver>().Observe();
            receiver.Endpoints.Single(endpoint => endpoint.Pattern == "/project").Admitted.ShouldBeTrue();
            receiver.Endpoints.Single(endpoint => endpoint.Pattern == "/project/v2").Admitted.ShouldBe(admitted);
            receiver.Endpoints.Single(endpoint => endpoint.Pattern == "/project/rebuild/v1").Admitted.ShouldBeFalse();
            receiver.Endpoints.Single(endpoint => endpoint.Pattern == "/widget/events").Admitted.ShouldBeTrue();

            RouteCatalogHostObservation observed = await DevelopmentDomainRouteCatalog.ObserveAsync(app.Services, app.Configuration,
                [typeof(DomainServiceTrustBoundaryTests).Assembly], ([], []), TestContext.Current.CancellationToken);
            var auxiliary = observed.AuxiliaryOperations.ShouldNotBeNull();
            auxiliary.ShouldContain(operation => operation.Method == "project");
            auxiliary.Any(operation => operation.Method == "project/v2").ShouldBe(admitted);
            auxiliary.ShouldNotContain(operation => operation.Method == "project/rebuild/v1");
            auxiliary.ShouldContain(operation => operation.Method == "widget/events" && operation.Sidecar != null);

            using var client = app.GetTestClient();
            using var request = JsonPost("/project/rebuild/v1", "{}");
            AddInternalHeaders(request, Assertion([EventStoreWorkloadOperations.DomainServiceProject]), [ChannelToken]);
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
        finally
        {
            workspace.Delete(recursive: true);
        }
    }
}
