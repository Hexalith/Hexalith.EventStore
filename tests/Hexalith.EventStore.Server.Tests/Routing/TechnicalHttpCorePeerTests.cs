using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.ServiceDefaults.Routing;

using MediatR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Real SDK business controller ingress keeps complete peer binding while direct public human ingress remains independent.</summary>
public sealed class TechnicalHttpCorePeerTests
{
    [Theory]
    [InlineData("exact", true)]
    [InlineData("agent", false)]
    [InlineData("service", false)]
    [InlineData("missing-surface", false)]
    [InlineData("ambiguous-surface", false)]
    [InlineData("direct-human", true)]
    [InlineData("missing-all", false)]
    [InlineData("missing-binding", false)]
    [InlineData("tampered", false)]
    [InlineData("foreign-run-same-root-epoch", false)]
    public async Task RealSdkCoreControllerRefusesMissingOrForeignBindingBeforeDispatch(string scenario, bool admitted)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        await using var fixture = new TechnicalHttpCatalogFixture(compiledSchemaThree: true);
        var terminal = new TechnicalHttpCoreCommandHandler(fixture.State);
        await fixture.StartAsync(TestContext.Current.CancellationToken, services: services =>
        {
            services.AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<IMediator>();
                configuration.AddOpenBehavior(typeof(Hexalith.EventStore.Pipeline.AuthorizationBehavior<,>));
                configuration.AddOpenBehavior(typeof(Hexalith.EventStore.Pipeline.RouteCatalogEligibilityBehavior<,>));
            });
            services.RemoveAll<IRequestHandler<SubmitCommand, SubmitCommandResult>>();
            services.AddSingleton<IRequestHandler<SubmitCommand, SubmitCommandResult>>(terminal);
        }, includeBusiness: true);
        fixture.Catalog.Envelope.SchemaVersion.ShouldBe(3);
        fixture.Catalog.Envelope.Operations!.Single(operation => operation.RouteId == "counter-increment-counter").Eligibility.ShouldBe("UI-only");
        fixture.State.SeedByteStore("untouched", "{\"value\":3}"u8.ToArray());
        var committed = (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Committed!;
        if (scenario is "agent" or "service" or "missing-surface" or "ambiguous-surface")
            fixture.Human(scenario == "missing-surface" ? null : scenario == "ambiguous-surface" ? "ui" : scenario, duplicate: scenario == "ambiguous-surface");
        if (scenario == "direct-human") fixture.Human(channel: false);
        else if (scenario != "missing-all")
        {
            fixture.Client.DefaultRequestHeaders.Add(RouteCatalogInvocationHandler.RootHeader, committed.RootDigest);
            fixture.Client.DefaultRequestHeaders.Add(RouteCatalogInvocationHandler.EpochHeader, "1");
            string binding = RouteCatalogActivationBinding.ComputeDigest(committed);
            if (scenario == "tampered") binding = new string('0', 64);
            if (scenario == "foreign-run-same-root-epoch") binding = RouteCatalogActivationBinding.ComputeDigest(committed with { AttemptId = "foreign-run" });
            if (scenario != "missing-binding") fixture.Client.DefaultRequestHeaders.Add(RouteCatalogInvocationHandler.BindingHeader, binding);
        }
        using var response = await fixture.Client.PostAsJsonAsync("/api/v1/commands", new
        { messageId = Hexalith.Commons.UniqueIds.UniqueIdHelper.GenerateSortableUniqueStringId(), tenant = "tenant-a", domain = "counter", aggregateId = "counter-1", commandType = "increment-counter", payload = new { amount = 1 } }, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(admitted ? HttpStatusCode.Accepted : scenario is "agent" or "service" or "missing-surface" or "ambiguous-surface"
            ? HttpStatusCode.Forbidden : HttpStatusCode.ServiceUnavailable);
        terminal.Effects.ShouldBe(admitted ? 1 : 0); fixture.State.ByteStoreContains("business-effect").ShouldBe(admitted);
        System.Text.Encoding.UTF8.GetString(fixture.State.ByteStoreValue("untouched")).ShouldBe("{\"value\":3}");
        (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
    }
}
