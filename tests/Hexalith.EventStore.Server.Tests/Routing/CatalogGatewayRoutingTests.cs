using System.Text.Json;

using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.EventStore.Queries;
using Hexalith.EventStore.Server.Pipeline.Queries;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogGatewayRoutingTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("failure")]
    [InlineData("stale-positive")]
    [InlineData("cached-absence")]
    public async Task ActualGatewayDecoratorPreservesCommittedHandlerAndActorTargetsAcrossMetadataStates(string metadataState)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        IDomainQueryHandlerRegistry registry = Substitute.For<IDomainQueryHandlerRegistry>();
        if (metadataState == "failure") registry.SupportsQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("metadata unavailable"));
        else registry.SupportsQueryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(metadataState == "stale-positive");
        IDomainQueryInvoker handler = Substitute.For<IDomainQueryInvoker>();
        handler.InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>()).Returns(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { source = "handler" })));
        IProjectionActorInvoker actor = Substitute.For<IProjectionActorInvoker>();
        actor.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(QueryResult.FromPayload(JsonSerializer.SerializeToElement(new { source = "actor" }), "counter"));
        var inner = new QueryRouter(actor, NullLogger<QueryRouter>.Instance, Substitute.For<IProjectionLifecycleGateway>(), catalogAuthority: fixture.Authorities[0]);
        var gateway = new HandlerAwareQueryRouter(fixture.Authorities[0], inner, registry, handler, NullLogger<HandlerAwareQueryRouter>.Instance);

        var handlerResult = await gateway.RouteQueryAsync(Query("tenants", "get-tenant"));
        handlerResult.Success.ShouldBeTrue();
        handlerResult.Payload!.Value.GetProperty("source").GetString().ShouldBe("handler");
        actor.ReceivedCalls().ShouldBeEmpty();
        var actorResult = await gateway.RouteQueryAsync(Query("counter", "get-counter-status"));
        actorResult.Success.ShouldBeTrue();
        actorResult.Payload!.Value.GetProperty("source").GetString().ShouldBe("actor");
        registry.ReceivedCalls().ShouldBeEmpty();
        await handler.Received(1).InvokeAsync(Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>());
        actor.ReceivedCalls().Count().ShouldBeInRange(1, 2);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("projection-override")]
    [InlineData("actor-override")]
    public async Task ActualGatewayFallbackRefusesUndeclaredWorkAfterSuccessfulReadiness(string fault)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        (await fixture.Authorities[0].ReadbackAsync()).Serving.ShouldBeTrue();
        IProjectionActorInvoker actor = Substitute.For<IProjectionActorInvoker>();
        IDomainQueryInvoker handler = Substitute.For<IDomainQueryInvoker>();
        var inner = new QueryRouter(actor, NullLogger<QueryRouter>.Instance, Substitute.For<IProjectionLifecycleGateway>(), catalogAuthority: fixture.Authorities[0]);
        var gateway = new HandlerAwareQueryRouter(fixture.Authorities[0], inner, Substitute.For<IDomainQueryHandlerRegistry>(), handler, NullLogger<HandlerAwareQueryRouter>.Instance);
        var query = fault switch
        {
            "unknown" => Query("counter", "not-declared"),
            "projection-override" => Query("counter", "get-counter-status") with { ProjectionType = "secret-projection" },
            _ => Query("counter", "get-counter-status") with { ProjectionActorType = "SecretActor" },
        };
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => gateway.RouteQueryAsync(query));
        actor.ReceivedCalls().ShouldBeEmpty();
        handler.ReceivedCalls().ShouldBeEmpty();
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Fact]
    public async Task DirectActorRouterCannotBypassACommittedHttpHandler()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        IProjectionActorInvoker actor = Substitute.For<IProjectionActorInvoker>();
        var router = new QueryRouter(actor, NullLogger<QueryRouter>.Instance, Substitute.For<IProjectionLifecycleGateway>(), catalogAuthority: fixture.Authorities[0]);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => router.RouteQueryAsync(Query("tenants", "get-tenant")));
        actor.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AmbiguousActorCompletionRetainsFenceUntilOwnerVerifiesRemoteQuiescence(bool timeout)
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        var remoteExecution = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IProjectionActorInvoker actor = Substitute.For<IProjectionActorInvoker>();
        actor.InvokeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<QueryEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<QueryResult>(timeout ? new TimeoutException("ambiguous actor RPC") : new OperationCanceledException()));
        var router = new QueryRouter(actor, NullLogger<QueryRouter>.Instance, Substitute.For<IProjectionLifecycleGateway>(), catalogAuthority: fixture.Authorities[0]);
        if (timeout) (await router.RouteQueryAsync(Query("counter", "get-counter-status"))).Success.ShouldBeFalse();
        else await Should.ThrowAsync<OperationCanceledException>(() => router.RouteQueryAsync(Query("counter", "get-counter-status")));
        var fence = (await fixture.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
        var second = fixture.Candidate(2, first.RootDigest);
        await fixture.Authorities[0].PrepareAsync(second);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].ReadyAsync(second.AttemptId));
        var verifier = new ObservedQuiescenceFixture();
        verifier.Observe(fence, remoteExecution.Task);
        Should.Throw<RouteCatalogUnavailableException>(() => verifier.Issue(fence));
        var restarted = fixture.CreateAuthority(fixture.Hosts[0], verifier: verifier);
        var forged = new Hexalith.EventStore.Contracts.Routing.RouteCatalogQuiescenceRequest(fence.LeaseId, fence.RootDigest, fence.Epoch, new string('0', 64));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => restarted.ReconcileAsync(forged));
        remoteExecution.SetResult();
        await remoteExecution.Task;
        // Eventual remote completion alone never clears the persisted fence, including across restart.
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => restarted.ReadyAsync(second.AttemptId));
        var receipt = verifier.Issue(fence);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].ReconcileAsync(receipt)); // no default owner authority
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => restarted.ReconcileAsync(receipt with { Epoch = receipt.Epoch + 1 }));
        await restarted.ReconcileAsync(receipt);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        foreach (var host in fixture.Authorities) await host.ReadyAsync(second.AttemptId);
        await restarted.CommitAsync(second.AttemptId);
        (await restarted.ReadbackAsync()).RootDigest.ShouldBe(second.RootDigest);
    }

    private static SubmitQuery Query(string domain, string type) => new("tenant-a", domain, "aggregate-1", type, [], "correlation", "user");
}
