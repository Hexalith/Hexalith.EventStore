using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogLifecycleAmendmentTests
{
    [Fact]
    public async Task DelayedPendingAbortCannotBecomeCommittedRollbackOrAbortAReplacement()
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        var second = fixture.Candidate(2, first.RootDigest);
        await fixture.Authorities[0].PrepareAsync(second);
        var delayed = new RouteCatalogRollbackRequest(second.AttemptId, RouteCatalogActivationBinding.ComputeDigest(second), 1, "pending");
        foreach (var host in fixture.Authorities) await host.ReadyAsync(second.AttemptId);
        await fixture.Authorities[0].CommitAsync(second.AttemptId);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].RollbackAsync(delayed));
        (await fixture.Store.ReadAsync(default)).State.Committed!.AttemptId.ShouldBe(second.AttemptId);
        await fixture.Authorities[0].RollbackAsync(new(second.AttemptId, delayed.BindingDigest, 2, "committed"));
        var replacement = fixture.Candidate(2, first.RootDigest, "replacement-same-bytes");
        replacement.RootDigest.ShouldBe(second.RootDigest);
        await fixture.Authorities[0].PrepareAsync(replacement);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].RollbackAsync(delayed));
        (await fixture.Store.ReadAsync(default)).State.Pending!.AttemptId.ShouldBe(replacement.AttemptId);
    }

    [Fact]
    public async Task AbortedAttemptIsDurablyImmutableAndExactAbortRetriesLeaveReplacementUntouched()
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        var old = fixture.Candidate(2, first.RootDigest, "aborted-attempt");
        await fixture.Authorities[0].PrepareAsync(old);
        var abort = new RouteCatalogRollbackRequest(old.AttemptId, RouteCatalogActivationBinding.ComputeDigest(old), 1, "pending");
        await fixture.Authorities[0].RollbackAsync(abort);
        var restarted = fixture.CreateAuthority(fixture.Hosts[0]);
        var reused = old with { Owner = "different-owner" };
        reused = reused with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(reused) };
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => restarted.PrepareAsync(reused));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => restarted.PrepareAsync(old));
        var replacement = fixture.Candidate(2, first.RootDigest, "replacement");
        await restarted.PrepareAsync(replacement);
        await restarted.PrepareAsync(replacement); // Response-loss retry of the same live prepare.
        await restarted.RollbackAsync(abort); // Exact terminal retry is a no-op.
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => restarted.ReadyAsync(old.AttemptId));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => restarted.CommitAsync(old.AttemptId));
        var state = (await fixture.Store.ReadAsync(default)).State;
        state.Pending!.AttemptId.ShouldBe(replacement.AttemptId);
        state.Attempts.Single(a => a.AttemptId == old.AttemptId).BindingDigest.ShouldBe(abort.BindingDigest);
        state.Attempts.Count.ShouldBe(3);
    }

    [Fact]
    public async Task CapacityRefusalIsAlsoReadinessRefusalAndRecoveryRestoresServing()
    {
        var fixture = new RouteCatalogTestFixture();
        var candidate = fixture.Candidate();
        await fixture.ActivateAsync(candidate);
        var (state, etag) = await fixture.Store.ReadAsync(default);
        var full = state with { Leases = Enumerable.Range(1, 4096).Select(i => new RouteCatalogDispatchFence(i, "lease-" + i, fixture.Hosts[0].HostId, candidate.RootDigest, 1)).ToArray() };
        (await fixture.Store.CompareExchangeAsync(full, etag, default)).ShouldBeTrue();
        (await fixture.Authorities[0].ReadbackAsync()).Serving.ShouldBeFalse();
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].AcquireAsync());
        var (_, currentEtag) = await fixture.Store.ReadAsync(default);
        (await fixture.Store.CompareExchangeAsync(full with { Leases = [] }, currentEtag, default)).ShouldBeTrue();
        (await fixture.Authorities[0].ReadbackAsync()).Serving.ShouldBeTrue();
        await using var positive = await fixture.Authorities[0].AcquireAsync();
    }

    [Fact]
    public void CommandOnlyCandidateCannotOmitTheAdmissionConsumerHost()
    {
        var fixture = new RouteCatalogTestFixture();
        var envelope = RouteCatalogCodec.Seal(fixture.Envelope with
        {
            Declarations = fixture.Envelope.Declarations.Where(d => d.Kind == "command").ToArray(),
            Routes = fixture.Envelope.Routes.Where(r => fixture.Envelope.Declarations.Any(d => d.RouteId == r.RouteId && d.Kind == "command")).ToArray(),
        });
        var original = fixture.Candidate();
        RouteCatalogActivation valid = original with { CanonicalCatalog = RouteCatalogCodec.Encode(envelope), RootDigest = RouteCatalogCodec.ComputeRootDigest(envelope) };
        RouteCatalogActivationBinding.ComputeDigest(valid).ShouldNotBeNullOrWhiteSpace();
        string consumer = envelope.Idempotency.First().ConsumerAppId;
        var invalid = valid with { RequiredHosts = valid.RequiredHosts.Where(h => h.AppId != consumer).ToArray() };
        Should.Throw<RouteCatalogUnavailableException>(() => RouteCatalogActivationBinding.ComputeDigest(invalid));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentlyObservedExecutableDriftRefusesPrepareOrReadyDespiteCorrectTrust(bool ready)
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        var observed = fixture.Observations.First(o => o.Targets.Count > 0);
        var changed = observed with { Targets = observed.Targets.Select((r, i) => i == 0 ? r with { Method = "independently-observed-different-method" } : r).ToArray() };
        var identity = ObservedRouteCatalogHost.Identity(changed);
        var options = Options.Create(new RouteCatalogHostOptions { Identity = identity, Profile = "Development", DevelopmentFixture = true });
        IHostEnvironment env = Substitute.For<IHostEnvironment>(); env.EnvironmentName.Returns("Development");
        var authority = new RouteCatalogAuthority(fixture.Store, new DevelopmentRouteCatalogTrustValidator(options, env), options,
            new ObservedRouteCatalogHost(_ => Task.FromResult(changed)));
        var second = fixture.Candidate(2, first.RootDigest) with { RequiredHosts = fixture.Hosts.Select(h => h.HostId == identity.HostId ? identity : h).ToArray() };
        second = second with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(second) };
        if (ready)
        {
            var independentPreparer = fixture.Authorities.First(a => a != fixture.Authorities[Array.FindIndex(fixture.Hosts, h => h.HostId == identity.HostId)]);
            await independentPreparer.PrepareAsync(second);
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.ReadyAsync(second.AttemptId));
        }
        else await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.PrepareAsync(second));
        (await fixture.Store.ReadAsync(default)).State.Committed!.RootDigest.ShouldBe(first.RootDigest);
    }

    [Fact]
    public async Task FailedDurableReleasePermitsExplicitRetry()
    {
        int attempts = 0;
        var fixture = new RouteCatalogTestFixture();
        var candidate = fixture.Candidate();
        await fixture.ActivateAsync(candidate);
        var durable = await fixture.Authorities[0].AcquireAsync();
        var lease = new RouteCatalogDispatchLease(durable.Snapshot, async () =>
        {
            if (Interlocked.Increment(ref attempts) == 1) throw new IOException("store temporarily unavailable");
            await durable.DisposeAsync();
        });
        await Should.ThrowAsync<IOException>(() => lease.DisposeAsync().AsTask());
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
        int effects = 0;
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => lease.InvokeRemoteAsync(() =>
        {
            effects++;
            return Task.FromResult(1);
        }));
        effects.ShouldBe(0);
        await lease.DisposeAsync();
        attempts.ShouldBe(2);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyActualAmbiguousRemoteCallsRetainCanceledScopes(bool remote)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        using var canceled = new CancellationTokenSource();
        var lease = await fixture.Authorities[0].AcquireAsync();
        await using (new RouteCatalogLeaseReleaser(lease, canceled.Token))
        {
            canceled.Cancel();
            if (remote)
                await Should.ThrowAsync<IOException>(() => RouteCatalogRemoteCall.InvokeAsync(() => Task.FromException(new IOException("remote response was lost"))));
        }
        var state = (await fixture.Store.ReadAsync(default)).State;
        state.Leases.Count.ShouldBe(remote ? 1 : 0);
    }
    [Fact]
    public async Task AbandonedActualRemoteTaskRetainsItsFenceEvenAfterEventualCompletion()
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate(); await fixture.ActivateAsync(first);
        var lease = await fixture.Authorities[0].AcquireAsync();
        var remote = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int> pending;
        await using (new RouteCatalogLeaseReleaser(lease))
        {
            pending = RouteCatalogRemoteCall.InvokeAsync(() => remote.Task);
            pending.IsCompleted.ShouldBeFalse();
        }
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
        remote.SetResult(1);
        (await pending).ShouldBe(1);
        // Eventual completion alone supplies no owner-verified request-to-fence proof.
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
        var second = fixture.Candidate(2, first.RootDigest); await fixture.Authorities[0].PrepareAsync(second);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].ReadyAsync(second.AttemptId));
    }
    [Fact]
    public async Task DisposalWhileRemoteFactoryIsExecutingRetainsTheDurableFence()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var lease = await fixture.Authorities[0].AcquireAsync();
        var scope = new RouteCatalogLeaseReleaser(lease);
        using var entered = new ManualResetEventSlim();
        using var releaseFactory = new ManualResetEventSlim();
        Task<int> pending = Task.Run(() => RouteCatalogRemoteCall.InvokeAsync(() =>
        {
            entered.Set();
            releaseFactory.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            return Task.FromResult(1);
        }));
        try
        {
            entered.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            await scope.DisposeAsync();
            (await fixture.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
        }
        finally
        {
            releaseFactory.Set();
            (await pending).ShouldBe(1);
            await scope.DisposeAsync();
        }
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task CapturedAsyncContextCannotStartRemoteEffectsAfterItsLeaseIsDisposed()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var lease = await fixture.Authorities[0].AcquireAsync();
        var scope = new RouteCatalogLeaseReleaser(lease);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int effects = 0;
        Task<int> delayed = Task.Run(async () =>
        {
            await start.Task;
            return await RouteCatalogRemoteCall.InvokeAsync(() =>
            {
                Interlocked.Increment(ref effects);
                return Task.FromResult(1);
            });
        });
        await scope.DisposeAsync();
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        start.SetResult();
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => delayed);
        effects.ShouldBe(0);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }
}
