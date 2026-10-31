using System.Net;

using Hexalith.EventStore.Authentication;
using Hexalith.EventStore.Contracts.Authorization;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Controllers;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Method-marked workload status retains its actual assertion, resource binding and full peer activation.</summary>
public sealed class TechnicalHttpWorkloadActionTests
{
    [Theory]
    [InlineData("policy")]
    [InlineData("policy-metadata")]
    public async Task CanonicalCoreWorkloadPolicyCannotBecomeAWeakenedPreCaptureBaseline(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        await using var fixture = new TechnicalHttpCatalogFixture();
        var refusal = await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.StartAsync(TestContext.Current.CancellationToken, includeBusiness: true,
            services: services =>
            {
                if (scenario == "policy") services.PostConfigure<AuthorizationOptions>(options => options.AddPolicy(DaprInternalAuthenticationOptions.GatewayCommandSubmitPolicy,
                    new AuthorizationPolicyBuilder(DaprInternalAuthenticationOptions.SchemeName).RequireAuthenticatedUser()
                        .RequireClaim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, EventStoreOperationIdentifiers.GatewayCommandSubmit).Build()));
            }, endpoints: app =>
            {
                if (scenario != "policy-metadata") return;
                var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
                    .Single(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()?.MethodInfo.Name == nameof(CommandsController.SubmitWorkload));
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Single().Policy = DaprInternalAuthenticationOptions.GatewayCommandStatusPolicy;
            }));
        refusal.Message.ShouldContain(nameof(CommandsController.SubmitWorkload));
        (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Committed.ShouldBeNull();
    }

    [Theory]
    [InlineData("exact", 200)]
    [InlineData("missing-binding", 503)]
    [InlineData("partial-binding", 503)]
    [InlineData("foreign-binding", 503)]
    [InlineData("human", 401)]
    [InlineData("wrong-domain", 404)]
    public async Task ActualWorkloadStatusCannotUseTheExternalHumanTechnicalException(string scenario, int expected)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        await using var fixture = new TechnicalHttpCatalogFixture(); var store = Substitute.For<ICommandStatusStore>(); int reads = 0;
        store.ReadStatusAsync("tenant-a", "message-one", Arg.Any<CancellationToken>()).Returns(_ =>
        {
            reads++; return new CommandStatusRecord(CommandStatus.Completed, DateTimeOffset.UtcNow, "aggregate-one", 1, null, null, null,
                MessageId: "message-one") { Domain = "counter" };
        });
        await fixture.StartAsync(TestContext.Current.CancellationToken, services: services => services.AddSingleton(store), fullInventory: true, inventoryAssembly: "Hexalith.EventStore.Gateway",
            invocations: [new("eventstore-admin-server", "/api/v1/commands/status", ["GET"])]);
        if (scenario != "human") fixture.Workload(fixture.Client, EventStoreOperationIdentifiers.GatewayCommandStatus,
            caller: "eventstore-admin-server", tenant: "tenant-a", domain: scenario == "wrong-domain" ? "other" : "counter");
        var committed = (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Committed!;
        if (scenario != "missing-binding")
        {
            fixture.Client.DefaultRequestHeaders.Add(RouteCatalogInvocationHandler.RootHeader, committed.RootDigest);
            fixture.Client.DefaultRequestHeaders.Add(RouteCatalogInvocationHandler.EpochHeader, "1");
            if (scenario != "partial-binding") fixture.Client.DefaultRequestHeaders.Add(RouteCatalogInvocationHandler.BindingHeader,
                scenario == "foreign-binding" ? new string('0', 64) : RouteCatalogActivationBinding.ComputeDigest(committed));
        }
        using var response = await fixture.Client.GetAsync("/api/v1/commands/status/workload/tenant-a/message-one", TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe((HttpStatusCode)expected, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        reads.ShouldBe(scenario is "exact" or "wrong-domain" ? 1 : 0);
        fixture.State.TrySaveByteOperations.ShouldBeEmpty();
        (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("policy")]
    [InlineData("method-descriptor")]
    public async Task MethodMarkedActionCannotDisappearOrFallBackToCoreAfterPreCaptureWeakening(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        await using var fixture = new TechnicalHttpCatalogFixture();
        var refusal = await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.StartAsync(TestContext.Current.CancellationToken, fullInventory: true,
            inventoryAssembly: "Hexalith.EventStore.Gateway", services: services =>
            {
                if (scenario == "policy") services.PostConfigure<AuthorizationOptions>(options => options.AddPolicy(DaprInternalAuthenticationOptions.GatewayCommandStatusPolicy,
                    new AuthorizationPolicyBuilder(DaprInternalAuthenticationOptions.SchemeName).RequireAuthenticatedUser()
                        .RequireClaim(EventStoreWorkloadAuthenticationDefaults.OperationClaimType, EventStoreOperationIdentifiers.GatewayCommandStatus).Build()));
            }, endpoints: app =>
            {
                if (scenario != "method-descriptor") return;
                var action = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
                    .Single(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()?.MethodInfo.Name == nameof(CommandStatusController.GetWorkloadStatus))
                    .Metadata.GetMetadata<ControllerActionDescriptor>()!;
                action.Parameters.OfType<ControllerParameterDescriptor>().Single(parameter => parameter.Name == "tenant").ParameterType = typeof(int);
            }));
        refusal.Message.ShouldContain(nameof(CommandStatusController.GetWorkloadStatus));
        (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Committed.ShouldBeNull();
    }
}
