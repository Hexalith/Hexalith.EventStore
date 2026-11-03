using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Proves local release scheduling preserves durable fences, fresh retries and reentrant callback progress.</summary>
/// <remarks>Completed-lease release always shares the local admission gate; the retained selector no longer changes it.</remarks>
public sealed class LocalLeaseReleaseSchedulingTests
{
    /// <summary>Every local completion queues behind an actual in-progress acquisition, whatever the retained selector.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReleaseWaitsForLocalAdmissionWithoutInvalidatingItsFreshEtag(bool selected)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var blocked = new TaskCompletionSource();
        var entered = new TaskCompletionSource();
        bool hold = false;
        int observations = 0;
        RouteCatalogAuthority authority = Create(fixture, async token =>
        {
            observations++;
            if (hold) { entered.TrySetResult(); await blocked.Task.WaitAsync(token); }
            return fixture.Observations[0];
        }, selected);
        RouteCatalogDispatchLease prior = await authority.AcquireAsync();
        string priorId = (await fixture.Store.ReadAsync(default)).State.Leases.Single().LeaseId;
        hold = true;
        Task<RouteCatalogDispatchLease> acquiring = authority.AcquireAsync(TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Task releasing = prior.DisposeAsync().AsTask();
        releasing.IsCompleted.ShouldBeFalse();
        (await fixture.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(1);
        blocked.SetResult();
        await using RouteCatalogDispatchLease next = await acquiring.WaitAsync(TimeSpan.FromSeconds(5));
        await releasing.WaitAsync(TimeSpan.FromSeconds(5));

        observations.ShouldBe(2);
        (await fixture.Store.ReadAsync(default)).State.Leases.Single().LeaseId.ShouldNotBe(priorId);
    }

    /// <summary>Canceling or faulting validation releases the gate and preserves uncancelable completed-lease cleanup.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedAdmissionCannotStrandQueuedCompletion(bool canceled)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var blocked = new TaskCompletionSource();
        var entered = new TaskCompletionSource();
        using var cancellation = new CancellationTokenSource();
        bool hold = false;
        ExecutionContext? captured = null;
        RouteCatalogAuthority? authority = null;
        authority = Create(fixture, async token =>
        {
            if (hold) { captured = ExecutionContext.Capture(); entered.TrySetResult(); await blocked.Task.WaitAsync(token); }
            return fixture.Observations[0];
        });
        RouteCatalogDispatchLease prior = await authority.AcquireAsync();
        hold = true;
        Task<RouteCatalogDispatchLease> acquiring = authority.AcquireAsync(cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task releasing = prior.DisposeAsync().AsTask();
        releasing.IsCompleted.ShouldBeFalse();
        if (canceled) cancellation.Cancel();
        else blocked.SetException(new IOException("actual observer failure"));

        if (canceled) await Should.ThrowAsync<OperationCanceledException>(() => acquiring.WaitAsync(TimeSpan.FromSeconds(5)));
        else await Should.ThrowAsync<IOException>(() => acquiring.WaitAsync(TimeSpan.FromSeconds(5)));
        await releasing.WaitAsync(TimeSpan.FromSeconds(5));
        ExecutionContext.Run(captured!, _ => RouteCatalogLocalAdmissionTurn.IsHeldBy(authority).ShouldBeFalse(), null);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        hold = false;
        await using var next = await authority.AcquireAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A real validation callback can complete its prior lease without waiting on its own gate.</summary>
    [Fact]
    public async Task ReentrantObserverCompletionUsesOriginalCasAndForcesFreshValidationRetry()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        RouteCatalogDispatchLease? prior = null;
        int observations = 0;
        RouteCatalogAuthority? authority = null;
        authority = Create(fixture, async _ =>
        {
            observations++;
            RouteCatalogLocalAdmissionTurn.IsHeldBy(authority!).ShouldBeTrue();
            if (prior is not null) { await prior.DisposeAsync(); prior = null; }
            return fixture.Observations[0];
        });
        prior = await authority.AcquireAsync();
        string priorId = (await fixture.Store.ReadAsync(default)).State.Leases.Single().LeaseId;

        await using RouteCatalogDispatchLease next = await authority.AcquireAsync().WaitAsync(TimeSpan.FromSeconds(5));

        observations.ShouldBe(3);
        (await fixture.Store.ReadAsync(default)).State.Leases.Single().LeaseId.ShouldNotBe(priorId);
        RouteCatalogLocalAdmissionTurn.IsHeldBy(authority).ShouldBeFalse();
    }

    /// <summary>An escaped closed callback cannot bypass a later acquisition's scheduling gate.</summary>
    [Fact]
    public async Task EscapedClosedContextDoesNotRetainSchedulingOwnership()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var escapedGo = new TaskCompletionSource();
        var blocked = new TaskCompletionSource();
        var entered = new TaskCompletionSource();
        var releaseObserved = new TaskCompletionSource<bool>();
        Task? escaped = null;
        RouteCatalogDispatchLease? prior = null;
        bool hold = false;
        RouteCatalogAuthority? authority = null;
        authority = Create(fixture, async token =>
        {
            escaped ??= Task.Run(async () =>
            {
                await escapedGo.Task;
                RouteCatalogLocalAdmissionTurn.IsHeldBy(authority!).ShouldBeFalse();
                ValueTask release = prior!.DisposeAsync();
                releaseObserved.SetResult(release.IsCompleted);
                await release;
            });
            if (hold) { entered.TrySetResult(); await blocked.Task.WaitAsync(token); }
            return fixture.Observations[0];
        });
        prior = await authority.AcquireAsync();
        string priorId = (await fixture.Store.ReadAsync(default)).State.Leases.Single().LeaseId;
        hold = true;
        Task<RouteCatalogDispatchLease> acquiring = authority.AcquireAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        escapedGo.SetResult();
        (await releaseObserved.Task.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBeFalse();
        blocked.SetResult();
        await using RouteCatalogDispatchLease next = await acquiring.WaitAsync(TimeSpan.FromSeconds(5));
        await escaped!.WaitAsync(TimeSpan.FromSeconds(5));
        (await fixture.Store.ReadAsync(default)).State.Leases.Single().LeaseId.ShouldNotBe(priorId);
    }

    /// <summary>A genuinely live ancestor remains recognized across a nested different-authority acquisition.</summary>
    [Fact]
    public async Task NestedAuthorityCallbackCanReleaseTheExactLiveAncestorWithoutDeadlock()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        RouteCatalogAuthority? first = null, second = null;
        RouteCatalogDispatchLease? prior = null;
        bool nested = false;
        second = Create(fixture, async _ =>
        {
            RouteCatalogLocalAdmissionTurn.IsHeldBy(first!).ShouldBeTrue();
            RouteCatalogLocalAdmissionTurn.IsHeldBy(second!).ShouldBeTrue();
            if (prior is not null) { await prior.DisposeAsync(); prior = null; }
            return fixture.Observations[0];
        });
        first = Create(fixture, async _ =>
        {
            if (nested)
            {
                nested = false;
                await using var inner = await second.AcquireAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            return fixture.Observations[0];
        });
        prior = await first.AcquireAsync();
        string priorId = (await fixture.Store.ReadAsync(default)).State.Leases.Single().LeaseId;
        nested = true;

        await using var next = await first.AcquireAsync().WaitAsync(TimeSpan.FromSeconds(5));

        (await fixture.Store.ReadAsync(default)).State.Leases.Single().LeaseId.ShouldNotBe(priorId);
    }

    /// <summary>A foreign-only callback must queue behind the other authority's real acquisition.</summary>
    [Fact]
    public async Task ForeignCallbackCannotBypassAnotherAuthorityAdmission()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var blocked = new TaskCompletionSource();
        var entered = new TaskCompletionSource();
        var releaseObserved = new TaskCompletionSource<bool>();
        bool hold = false;
        RouteCatalogAuthority first = Create(fixture, async token =>
        {
            if (hold) { entered.TrySetResult(); await blocked.Task.WaitAsync(token); }
            return fixture.Observations[0];
        });
        RouteCatalogDispatchLease prior = await first.AcquireAsync();
        string priorId = (await fixture.Store.ReadAsync(default)).State.Leases.Single().LeaseId;
        hold = true;
        Task<RouteCatalogDispatchLease> acquiring = first.AcquireAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        RouteCatalogAuthority second = Create(fixture, async _ =>
        {
            RouteCatalogLocalAdmissionTurn.IsHeldBy(first).ShouldBeFalse();
            ValueTask release = prior.DisposeAsync();
            releaseObserved.TrySetResult(release.IsCompleted);
            await release;
            return fixture.Observations[0];
        });
        Task<RouteCatalogDispatchLease> foreign = second.AcquireAsync();
        (await releaseObserved.Task.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBeFalse();
        blocked.SetResult();

        await using var firstNext = await acquiring.WaitAsync(TimeSpan.FromSeconds(5));
        await using var secondNext = await foreign.WaitAsync(TimeSpan.FromSeconds(5));
        var remaining = (await fixture.Store.ReadAsync(default)).State.Leases;
        remaining.Count.ShouldBe(2);
        remaining.Any(item => item.LeaseId == priorId).ShouldBeFalse();
    }

    /// <summary>Pending generations refuse admission promptly while completed leases remain able to drain.</summary>
    [Fact]
    public async Task PendingGenerationAndOwnerQuiescenceDoNotHoldTheAdmissionGate()
    {
        var fixture = new RouteCatalogTestFixture();
        RouteCatalogActivation first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        RouteCatalogAuthority authority = Create(fixture, _ => Task.FromResult(fixture.Observations[0]));
        RouteCatalogDispatchLease prior = await authority.AcquireAsync();
        RouteCatalogActivation successor = fixture.Candidate(2, first.RootDigest);
        await fixture.Authorities[0].PrepareAsync(successor);

        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.AcquireAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        await prior.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        await authority.ReadyAsync(successor.AttemptId).WaitAsync(TimeSpan.FromSeconds(5));
        foreach (RouteCatalogAuthority peer in fixture.Authorities.Skip(1)) await peer.ReadyAsync(successor.AttemptId);
        await authority.CommitAsync(successor.AttemptId).WaitAsync(TimeSpan.FromSeconds(5));
        await using var next = await authority.AcquireAsync();
        next.Snapshot.RootDigest.ShouldBe(successor.RootDigest);
    }

    /// <summary>Remote ambiguity stays retained, and an exact owner verifier can complete another local lease without deadlock.</summary>
    [Fact]
    public async Task RemoteDrainAndOwnerVerifiedReconciliationKeepTheirOriginalDurableSemantics()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var verifier = Substitute.For<IRouteCatalogQuiescenceVerifier>();
        RouteCatalogDispatchLease? completing = null;
        int verifications = 0;
        verifier.VerifyAsync(Arg.Any<RouteCatalogQuiescenceRequest>(), Arg.Any<RouteCatalogDispatchFence>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                verifications++;
                call.Arg<RouteCatalogQuiescenceRequest>().Proof.ShouldBe("exact-owner-proof");
                await completing!.DisposeAsync();
            });
        RouteCatalogAuthority authority = Create(fixture, _ => Task.FromResult(fixture.Observations[0]), verifier: verifier);
        RouteCatalogDispatchLease retained = await authority.AcquireAsync();
        RouteCatalogDispatchFence fence = (await fixture.Store.ReadAsync(default)).State.Leases.Single();
        var remote = new TaskCompletionSource<int>();
        Task<int> invoking = retained.InvokeRemoteAsync(() => remote.Task);
        Task draining = retained.DrainRemoteCallsAsync();
        await retained.DisposeAsync();
        draining.IsCompleted.ShouldBeFalse();
        (await fixture.Store.ReadAsync(default)).State.Leases.Single().ShouldBe(fence);
        remote.SetResult(1);
        (await invoking).ShouldBe(1);
        await draining.WaitAsync(TimeSpan.FromSeconds(5));
        await retained.DisposeAsync();
        (await fixture.Store.ReadAsync(default)).State.Leases.Single().ShouldBe(fence);
        completing = await authority.AcquireAsync();

        await authority.ReconcileAsync(new(fence.LeaseId, fence.RootDigest, fence.Epoch, "exact-owner-proof"))
            .WaitAsync(TimeSpan.FromSeconds(5));

        verifications.ShouldBe(2); // The verifier's first completed-lease write requires a fresh reconciliation retry.
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    private static RouteCatalogAuthority Create(RouteCatalogTestFixture fixture,
        Func<CancellationToken, Task<RouteCatalogHostObservation>> capture, bool selected = true,
        IRouteCatalogQuiescenceVerifier? verifier = null)
    {
        var options = Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts[0], Profile = "Development",
            DevelopmentFixture = true, RequireTechnicalHttpBinding = true, SerializeLocalLeaseRelease = selected });
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        return new(fixture.Store, new DevelopmentRouteCatalogTrustValidator(options, environment), options,
            new ObservedRouteCatalogHost(capture), verifier, environment);
    }
}
