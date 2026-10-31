using Dapr.Actors;
using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.DomainServices;
using Hexalith.EventStore.Server.Tests.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Exercises each actual Direct notifier RPC inside a real durable ambient catalog lease.</summary>
public sealed class DaprProjectionChangeNotifierLeaseTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AmbiguousOrCanceledDirectRpcRetainsFenceEvenWhenCallerCatches(bool detail, bool canceled)
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        var (notifier, actor, broadcaster) = Create();
        actor.RegenerateAsync().Returns(_ => Task.FromException<string>(canceled
            ? new OperationCanceledException("remote response canceled") : new TimeoutException("remote response lost")));
        await using (new RouteCatalogLeaseReleaser(await fixture.Authorities[0].AcquireAsync()))
        {
            // The real projection base catches notifier failures; that cannot prove remote quiescence.
            await Should.ThrowAsync<Exception>(() => NotifyAsync(notifier, detail));
        }
        _ = actor.Received(1).RegenerateAsync();
        broadcaster.ReceivedCalls().ShouldBeEmpty();
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
        var second = fixture.Candidate(2, first.RootDigest);
        await fixture.Authorities[0].PrepareAsync(second);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].ReadyAsync(second.AttemptId));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].CommitAsync(second.AttemptId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KnownSuccessBeforeScopeDisposalReleasesItsFence(bool detail)
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        var (notifier, actor, _) = Create();
        actor.RegenerateAsync().Returns(Task.FromResult("known-etag"));
        await using (new RouteCatalogLeaseReleaser(await fixture.Authorities[0].AcquireAsync()))
        {
            await NotifyAsync(notifier, detail);
        }
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        var second = fixture.Candidate(2, first.RootDigest);
        await fixture.ActivateAsync(second);
        (await fixture.Store.ReadAsync(default)).State.Committed!.RootDigest.ShouldBe(second.RootDigest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposedScopeWhileDirectRpcPendingRetainsFenceAfterEventualSuccess(bool detail)
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        var (notifier, actor, _) = Create();
        var remote = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        actor.RegenerateAsync().Returns(remote.Task);
        Task pending;
        await using (new RouteCatalogLeaseReleaser(await fixture.Authorities[0].AcquireAsync()))
        {
            pending = NotifyAsync(notifier, detail);
            pending.IsCompleted.ShouldBeFalse();
        }
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
        remote.SetResult("eventual-etag");
        await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        // The established contract requires owner-verified quiescence after an abandoned await.
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
        var second = fixture.Candidate(2, first.RootDigest);
        await fixture.Authorities[0].PrepareAsync(second);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].ReadyAsync(second.AttemptId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBeforeDispatchStartsNoRpcAndRetainsNoFence(bool detail)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var (notifier, actor, broadcaster) = Create();
        actor.RegenerateAsync().Returns(Task.FromResult("must-not-dispatch"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using (new RouteCatalogLeaseReleaser(await fixture.Authorities[0].AcquireAsync()))
        {
            await Should.ThrowAsync<OperationCanceledException>(() => NotifyAsync(notifier, detail, cancellation.Token));
        }
        actor.ReceivedCalls().ShouldBeEmpty();
        broadcaster.ReceivedCalls().ShouldBeEmpty();
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealProjectionFireAndForgetPreservesPendingAndKnownSuccessLifecycles(bool pendingAtDisposal)
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        var (notifier, actor, broadcaster) = Create();
        var remote = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pendingAtDisposal) { remote.SetResult("known-before-disposal"); }
        actor.RegenerateAsync().Returns(remote.Task);
        var broadcast = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        broadcaster.BroadcastChangedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => { broadcast.TrySetResult(); return Task.CompletedTask; });
        var projection = new ExtraSdkCatalogProjection { Notifier = notifier, TenantId = "acme" };
        await using (new RouteCatalogLeaseReleaser(await fixture.Authorities[0].AcquireAsync()))
        {
            // Invoke the genuine base implementation, which intentionally discards NotifyAsync's Task.
            _ = projection.Project(Array.Empty<object>());
            _ = actor.Received(1).RegenerateAsync();
            broadcast.Task.IsCompleted.ShouldBe(!pendingAtDisposal);
        }
        (await fixture.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(pendingAtDisposal ? 1 : 0);
        if (pendingAtDisposal) { remote.SetResult("known-only-after-disposal"); }
        await broadcast.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        (await fixture.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(pendingAtDisposal ? 1 : 0);
        var second = fixture.Candidate(2, first.RootDigest);
        if (pendingAtDisposal)
        {
            await fixture.Authorities[0].PrepareAsync(second);
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].ReadyAsync(second.AttemptId));
        }
        else { await fixture.ActivateAsync(second); }
    }

    private static Task NotifyAsync(DaprProjectionChangeNotifier notifier, bool detail, CancellationToken cancellationToken = default) => detail
        ? notifier.NotifyProjectionChangedAsync(new ProjectionChangedDetail("counter", "acme", null, new Dictionary<string, string>()), cancellationToken)
        : notifier.NotifyProjectionChangedAsync("counter", "acme", cancellationToken: cancellationToken);

    private static (DaprProjectionChangeNotifier Notifier, IETagActor Actor, IProjectionChangedBroadcaster Broadcaster) Create()
    {
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        IETagActor actor = Substitute.For<IETagActor>();
        factory.CreateActorProxy<IETagActor>(Arg.Any<ActorId>(), Arg.Any<string>()).Returns(actor);
        IProjectionChangedBroadcaster broadcaster = Substitute.For<IProjectionChangedBroadcaster>();
        var notifier = new DaprProjectionChangeNotifier(Substitute.For<DaprClient>(), factory, broadcaster,
            Options.Create(new ProjectionChangeNotifierOptions { Transport = ProjectionChangeTransport.Direct }),
            Substitute.For<ILogger<DaprProjectionChangeNotifier>>());
        return (notifier, actor, broadcaster);
    }
}
