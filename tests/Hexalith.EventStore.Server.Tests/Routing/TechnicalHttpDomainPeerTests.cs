using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.DomainService;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Exercises the existing domain peer-generation guard with a real committed technical cohort and native persisted effects.</summary>
public sealed class TechnicalHttpDomainPeerTests
{
    [Theory]
    [InlineData("exact", true)]
    [InlineData("missing-root", false)]
    [InlineData("wrong-root", false)]
    [InlineData("wrong-epoch", false)]
    public Task ExistingDomainBoundaryRetainsAbsentTechnicalSchemaTwoGenerationGuard(string scenario, bool admitted)
        => ExerciseAsync(scenario, admitted, schemaThree: false);

    [Theory]
    [InlineData("exact", true)]
    [InlineData("missing", false)]
    [InlineData("tampered", false)]
    [InlineData("foreign-run-same-root-epoch", false)]
    [InlineData("duplicate", false)]
    [InlineData("missing-surface", false)]
    [InlineData("ambiguous-surface", false)]
    [InlineData("non-service", false)]
    public Task CompiledSchemaThreeDomainUsesActualWorkloadAndCompleteActivationBinding(string scenario, bool admitted)
        => ExerciseAsync(scenario, admitted, schemaThree: true);

    private static async Task ExerciseAsync(string scenario, bool admitted, bool schemaThree)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        await using var cohort = new TechnicalHttpCatalogFixture();
        if (schemaThree) await cohort.StartAsync(TestContext.Current.CancellationToken);
        var fixture = schemaThree ? cohort.Catalog : new RouteCatalogTestFixture();
        if (!schemaThree) await fixture.ActivateAsync(fixture.Candidate());
        fixture.Envelope.SchemaVersion.ShouldBe(schemaThree ? 3 : 2);
        var committed = (await fixture.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Committed!;
        int index = Array.FindIndex(fixture.Hosts, host => host.AppId == "sample");
        var guard = new DomainRouteCatalogGuard(fixture.Authorities[index], Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts[index] }));
        using var dapr = new RecordingDaprClient(); dapr.SeedByteStore("untouched", "{\"value\":3}"u8.ToArray());
        var state = new DaprReadModelBatchStateAccessor(dapr, "statestore"); int effects = 0;
        var handler = Substitute.For<IAsyncDomainProjectionHandler>();
        handler.Domain.Returns("counter"); handler.ProjectionType.Returns("counter"); handler.OrdinaryDeliveryEnabled.Returns(true);
        handler.ProjectAsync(Arg.Any<ProjectionRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            effects++;
            await state.TryWriteAsync("detail", "{\"value\":7}"u8.ToArray(), "", call.Arg<CancellationToken>());
            return DomainProjectionHandlerResult.Completed(JsonSerializer.SerializeToElement(new { value = 7 }));
        });
        var registry = new DomainProjectionCatalogRegistry(); registry.Register("state-fixture", [new ProjectionDispatchRoute("counter", "counter")]);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" }); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Services.AddSingleton(handler);
        if (schemaThree)
        {
            cohort.ConfigureWorkloadReceiver(builder.Configuration);
            builder.Services.AddEventStoreHttpHostSecurity();
            builder.Services.AddAuthentication().AddEventStoreWorkloadScheme(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme, "Authentication:Workload",
                (options, _) => { options.Audience = "technical-fixture"; options.AllowedCallers = ["eventstore"]; });
            builder.Services.AddEventStoreWorkloadPolicies(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme, [EventStoreOperationIdentifiers.DomainServiceProject]);
        }
        await using var app = builder.Build();
        if (schemaThree) { app.UseRouting(); app.UseAuthentication(); app.UseEventStoreHttpHostSecurity(); app.UseAuthorization(); }
        var endpoint = app.MapPost("/project", async (ProjectionRequest body, HttpContext context) => Results.Json(await DomainProjectionDispatcher.DispatchAsync(context.RequestServices,
            new(body, ["counter"], "state-dispatch", "state-fixture"), new(), registry, context.RequestAborted))).AddEndpointFilter(new DomainRouteCatalogFilter(guard));
        if (schemaThree) endpoint.RequireAuthorization(EventStoreWorkloadAuthenticationDefaults.OperationPolicy(EventStoreWorkloadAuthenticationDefaults.WorkloadScheme, EventStoreOperationIdentifiers.DomainServiceProject));
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient();
        if (schemaThree) cohort.Workload(client, EventStoreOperationIdentifiers.DomainServiceProject,
            scenario == "missing-surface" ? null : scenario == "non-service" ? "ui" : "service", duplicate: scenario == "ambiguous-surface");
        using var request = new HttpRequestMessage(HttpMethod.Post, app.Urls.Single() + "/project")
        { Content = JsonContent.Create(new ProjectionRequest("tenant-a", "counter", "aggregate", [])) };
        if (scenario != "missing-root") request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.RootHeader, scenario == "wrong-root" ? new string('0', 64) : committed.RootDigest);
        request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.EpochHeader, scenario == "wrong-epoch" ? "999" : "1");
        string binding = RouteCatalogActivationBinding.ComputeDigest(committed);
        if (scenario == "tampered") binding = new string('0', 64);
        if (scenario == "foreign-run-same-root-epoch") binding = RouteCatalogActivationBinding.ComputeDigest(committed with { AttemptId = "foreign-run" });
        if (schemaThree && scenario != "missing") request.Headers.TryAddWithoutValidation(RouteCatalogInvocationHandler.BindingHeader, scenario == "duplicate" ? new[] { binding, binding } : [binding]);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(admitted ? HttpStatusCode.OK : schemaThree && scenario is "ambiguous-surface" or "non-service" ? HttpStatusCode.Unauthorized : HttpStatusCode.ServiceUnavailable);
        effects.ShouldBe(admitted ? 1 : 0); dapr.ByteStoreContains("detail").ShouldBe(admitted);
        System.Text.Encoding.UTF8.GetString(dapr.ByteStoreValue("untouched")).ShouldBe("{\"value\":3}");
        (await fixture.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
    }
}
