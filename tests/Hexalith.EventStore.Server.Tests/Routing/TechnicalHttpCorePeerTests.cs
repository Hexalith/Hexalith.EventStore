using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Dapr.Actors.Runtime;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Pipeline.Commands;
using Hexalith.EventStore.Server.Routing;
using Hexalith.EventStore.ServiceDefaults.Authentication;
using Hexalith.EventStore.ServiceDefaults.Routing;

using MediatR;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Real SDK business controller ingress keeps complete peer binding while direct public human ingress remains independent.</summary>
public sealed class TechnicalHttpCorePeerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommandAndNativeActorReceiversShareTheExactFence(bool ambiguousActorResponse)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using var fixture = new TechnicalHttpCatalogFixture();
        var commandEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commandResume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rpcEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rpcResume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = Substitute.For<IRequestHandler<SubmitCommand, SubmitCommandResult>>();
        handler.Handle(Arg.Any<SubmitCommand>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            commandEntered.SetResult();
            await commandResume.Task.WaitAsync(call.Arg<CancellationToken>());
            return new SubmitCommandResult(call.Arg<SubmitCommand>().CorrelationId);
        });
        await fixture.StartAsync(token, services: services =>
        {
            services.AddMediatR(configuration => configuration.RegisterServicesFromAssemblyContaining<IMediator>());
            services.RemoveAll<IRequestHandler<SubmitCommand, SubmitCommandResult>>();
            services.AddSingleton(handler);
            services.AddActors(options => options.Actors.RegisterActor<AggregateActor>());
            services.AddSingleton<IDevelopmentNativeActorReceiver, DevelopmentNativeActorReceiver>();
        }, endpoints: app =>
        {
            app.MapActorsHandlers().RequireEventStoreChannel();
            // End the authenticated Dapr callback before the test would need a live actor sidecar.
            app.Use(async (context, next) =>
            {
                if (context.Request.RouteValues["actorTypeName"] is not "AggregateActor")
                {
                    await next(context);
                    return;
                }
                RouteCatalogRemoteExecution.Current.ShouldNotBeNull();
                if (context.Request.RouteValues["methodName"] is "ProcessCommandAsync")
                {
                    rpcEntered.SetResult();
                    await rpcResume.Task.WaitAsync(context.RequestAborted);
                    if (ambiguousActorResponse) context.Response.Headers["X-Daprerrorresponseheader"] = "test-ambiguity";
                }
                context.Response.StatusCode = 204;
            });
        }, includeBusiness: true, activate: false);
        RouteCatalogReceiverSurface surface = fixture.App.Services.GetRequiredService<TechnicalHttpRouteCatalogReceiver>().Observe();
        var auxiliary = DevelopmentTechnicalHttpTransport.Auxiliary(surface, "eventstore", "gateway")
            .Where(operation => operation.NativeActor is not null).ToArray();
        fixture.Catalog.UpdateObservation(0, fixture.Catalog.Observations[0] with { AuxiliaryOperations = auxiliary });
        var activation = fixture.Catalog.Candidate() with { AuxiliaryOperations = auxiliary };
        string binding = RouteCatalogActivationBinding.ComputeDigest(activation);
        await fixture.Catalog.ActivateAsync(activation with { TrustProof = binding });
        fixture.Client.DefaultRequestHeaders.Add(RouteCatalogInvocationHandler.RootHeader, activation.RootDigest);
        fixture.Client.DefaultRequestHeaders.Add(RouteCatalogInvocationHandler.EpochHeader, "1");
        fixture.Client.DefaultRequestHeaders.Add(RouteCatalogInvocationHandler.BindingHeader, binding);
        using var nativeClient = new HttpClient(new SocketsHttpHandler { UseProxy = false }) { BaseAddress = fixture.Client.BaseAddress };
        Task<HttpResponseMessage> command = fixture.Client.PostAsJsonAsync("/api/v1/commands", new
        {
            messageId = Hexalith.Commons.UniqueIds.UniqueIdHelper.GenerateSortableUniqueStringId(),
            tenant = "tenant-a", domain = "counter", aggregateId = "counter-1", commandType = "increment-counter", payload = new { amount = 1 },
        }, token);
        Task<HttpResponseMessage>? rpc = null;
        try
        {
            (await Task.WhenAny(commandEntered.Task, command).WaitAsync(TimeSpan.FromSeconds(10), token)).ShouldBeSameAs(commandEntered.Task);
            var (initial, etag) = await fixture.Catalog.Store.ReadAsync(token);
            var fence = initial.Leases.Single();
            fence.SharedAdmissionSealed.ShouldBe(false);

            using var read = NativeRequest("InspectSourceProofAsync");
            using var readResponse = await nativeClient.SendAsync(read, token);
            readResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            (await fixture.Catalog.Store.ReadAsync(token)).ETag.ShouldBe(etag);

            using var mutation = NativeRequest("ProcessCommandAsync");
            rpc = nativeClient.SendAsync(mutation, token);
            (await Task.WhenAny(rpcEntered.Task, rpc).WaitAsync(TimeSpan.FromSeconds(10), token)).ShouldBeSameAs(rpcEntered.Task);
            var (overlapping, overlappingEtag) = await fixture.Catalog.Store.ReadAsync(token);
            overlapping.Leases.ShouldBe([fence]);
            overlappingEtag.ShouldBe(etag);
            commandResume.SetResult();
            using var commandResponse = await command;
            commandResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            (await fixture.Catalog.Store.ReadAsync(token)).State.Leases.ShouldBe([fence]);
            rpcResume.SetResult();
            using var rpcResponse = await rpc;
            rpcResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            (await fixture.Catalog.Store.ReadAsync(token)).State.Leases.ShouldBe(ambiguousActorResponse ? [fence] : []);
        }
        finally
        {
            commandResume.TrySetResult();
            rpcResume.TrySetResult();
            using var commandResponse = await command;
            if (rpc is not null) { using var rpcResponse = await rpc; }
        }

        HttpRequestMessage NativeRequest(string method)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"/actors/AggregateActor/actor-1/method/{method}");
            request.Headers.Add(DaprAppChannelToken.HeaderName, fixture.App.Configuration[DaprAppChannelToken.ConfigurationKey]);
            return request;
        }
    }

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
