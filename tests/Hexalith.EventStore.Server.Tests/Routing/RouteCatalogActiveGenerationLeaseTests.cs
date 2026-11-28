using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class RouteCatalogActiveGenerationLeaseTests
{
    [Fact]
    public async Task OverlappingDispatchesShareOneFenceAndReadFreshAuthority()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var store = new CatalogAdmissionRebaseStore(fixture.Store);
        var trust = Substitute.For<IRouteCatalogTrustValidator>();
        using var authority = Create(fixture, store, trust);
        CancellationToken token = TestContext.Current.CancellationToken;

        RouteCatalogDispatchLease command = await authority.AcquireActiveGenerationAsync(token);
        RouteCatalogDispatchFence fence = (await fixture.Store.ReadAsync(token)).State.Leases.Single();
        for (int index = 0; index < 5; index++)
        {
            await using RouteCatalogDispatchLease rpc = await authority.AcquireActiveGenerationAsync(token);
            rpc.ActiveGeneration.ShouldBeSameAs(command.ActiveGeneration);
            rpc.Root.ShouldNotBeSameAs(command);
        }
        store.Reads.ShouldBe(7);
        store.Exchanges.ShouldBe(1);
        await trust.Received(6).ValidateAsync(Arg.Any<RouteCatalogActivation>(), Arg.Any<string>(), token);

        RouteCatalogDispatchLease lastRpc = await authority.AcquireActiveGenerationAsync(token);
        await command.DisposeAsync();
        (await fixture.Store.ReadAsync(token)).State.Leases.ShouldBe([fence]);
        store.Exchanges.ShouldBe(1);
        await lastRpc.DisposeAsync();
        await lastRpc.DisposeAsync();
        (await fixture.Store.ReadAsync(token)).State.Leases.ShouldBeEmpty();
        store.Exchanges.ShouldBe(2);
    }

    [Fact]
    public async Task PrepareDuringObservationRejectsNewDispatchAndLifecycleWaitsForAllReferences()
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        var successor = fixture.Candidate(2, first.RootDigest);
        CancellationToken token = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int captures = 0;
        using var authority = Create(fixture, fixture.Store, observe: async cancellation =>
        {
            if (Interlocked.Increment(ref captures) == 3)
            {
                entered.SetResult();
                await resume.Task.WaitAsync(cancellation);
            }
            return fixture.Observations[0];
        });
        RouteCatalogDispatchLease firstDispatch = await authority.AcquireActiveGenerationAsync(token);
        RouteCatalogDispatchLease secondDispatch = await authority.AcquireActiveGenerationAsync(token);
        Task<RouteCatalogDispatchLease> joining = authority.AcquireActiveGenerationAsync(token);
        try
        {
            await entered.Task.WaitAsync(token);
            await fixture.Authorities[0].PrepareAsync(successor, token);
        }
        finally { resume.SetResult(); }
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => joining);
        await firstDispatch.DisposeAsync();
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].ReadyAsync(successor.AttemptId, token));
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].CommitAsync(successor.AttemptId, token));
        await secondDispatch.DisposeAsync();
        foreach (RouteCatalogAuthority host in fixture.Authorities) await host.ReadyAsync(successor.AttemptId, token);
        await fixture.Authorities[0].CommitAsync(successor.AttemptId, token);

        RouteCatalogDispatchLease current = await authority.AcquireActiveGenerationAsync(token);
        current.Snapshot.RootDigest.ShouldBe(successor.RootDigest);
        current.Snapshot.Epoch.ShouldBe(2);
        var rollback = new RouteCatalogRollbackRequest(successor.AttemptId, RouteCatalogActivationBinding.ComputeDigest(successor), 2, "committed");
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].RollbackAsync(rollback, token));
        await current.DisposeAsync();
        await fixture.Authorities[0].RollbackAsync(rollback, token);
        await using var restored = await authority.AcquireActiveGenerationAsync(token);
        restored.Snapshot.RootDigest.ShouldBe(first.RootDigest);
        restored.Snapshot.Epoch.ShouldBe(3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposedEnrolledWorkRetainsTheGroupThroughActualCompletionAndOwnerProof(bool borrowed)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var verifier = Substitute.For<IRouteCatalogQuiescenceVerifier>();
        using var authority = Create(fixture, fixture.Store, verifier: verifier);
        CancellationToken token = TestContext.Current.CancellationToken;
        RouteCatalogDispatchLease owner = await authority.AcquireActiveGenerationAsync(token);
        RouteCatalogDispatchLease rpc = await authority.AcquireActiveGenerationAsync(token);
        RouteCatalogDispatchFence retained = (await fixture.Store.ReadAsync(token)).State.Leases.Single();
        var pendingCall = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        RouteCatalogDispatchLease callLease = borrowed ? RouteCatalogDispatchLease.CreateBorrowed(rpc) : rpc;
        Task<bool> enrolled = callLease.InvokeRemoteAsync(() => pendingCall.Task, token);
        var reconcile = new RouteCatalogQuiescenceRequest(retained.LeaseId, retained.RootDigest, retained.Epoch, "owner-proof");
        try
        {
            await rpc.DisposeAsync();
            await callLease.DisposeAsync();
            await owner.DisposeAsync();
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.ReconcileAsync(reconcile, token));
            await verifier.DidNotReceiveWithAnyArgs().VerifyAsync(default!, default!, default);
            await using RouteCatalogDispatchLease unrelated = await authority.AcquireActiveGenerationAsync(token);
            unrelated.ActiveGeneration.ShouldBeNull();
            (await fixture.Store.ReadAsync(token)).State.Leases.Count.ShouldBe(2);
        }
        finally { pendingCall.SetException(new TimeoutException("Remote response lost.")); }
        await Should.ThrowAsync<TimeoutException>(() => enrolled);
        (await fixture.Store.ReadAsync(token)).State.Leases.ShouldBe([retained]);
        await authority.ReconcileAsync(reconcile, token);
        await verifier.Received(1).VerifyAsync(reconcile, retained, token);
        await verifier.Received(1).VerifyAsync(reconcile, retained with { SharedAdmissionSealed = true }, token);
        (await fixture.Store.ReadAsync(token)).State.Leases.ShouldBeEmpty();
    }

    [Fact]
    public async Task PersistedOpeningStaysProtectedBeforeAndAfterLostAcknowledgement()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        CancellationToken token = TestContext.Current.CancellationToken;
        var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var respond = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new CatalogAdmissionRebaseStore(fixture.Store)
        {
            Exchange = async (attempt, state, etag, cancellation) =>
            {
                if (attempt != 1) return null;
                (await fixture.Store.CompareExchangeAsync(state, etag, cancellation)).ShouldBeTrue();
                written.SetResult();
                await respond.Task.WaitAsync(cancellation);
                return false;
            },
        };
        var verifier = Substitute.For<IRouteCatalogQuiescenceVerifier>();
        using var authority = Create(fixture, store, verifier: verifier);
        Task<RouteCatalogDispatchLease> acquisition = authority.AcquireActiveGenerationAsync(token);
        RouteCatalogDispatchFence fence;
        try
        {
            await written.Task.WaitAsync(token);
            fence = (await fixture.Store.ReadAsync(token)).State.Leases.Single();
            var reconcile = new RouteCatalogQuiescenceRequest(fence.LeaseId, fence.RootDigest, fence.Epoch, "owner-proof");
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.ReconcileAsync(reconcile, token));
            await verifier.DidNotReceiveWithAnyArgs().VerifyAsync(default!, default!, default);
        }
        finally { respond.SetResult(); }
        await using RouteCatalogDispatchLease owner = await acquisition;
        await using RouteCatalogDispatchLease joined = await authority.AcquireActiveGenerationAsync(token);
        joined.ActiveGeneration.ShouldBeSameAs(owner.ActiveGeneration);
        (await fixture.Store.ReadAsync(token)).State.Leases.ShouldBe([fence]);
        store.Exchanges.ShouldBe(1);
    }

    [Fact]
    public async Task PeerReconciliationSealsBeforeRepeatingProofAndKeepsTheFenceIfProofFails()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        CancellationToken token = TestContext.Current.CancellationToken;
        using var authority = Create(fixture, fixture.Store);
        await using RouteCatalogDispatchLease owner = await authority.AcquireActiveGenerationAsync(token);
        var fence = (await fixture.Store.ReadAsync(token)).State.Leases.Single();
        var reconcile = new RouteCatalogQuiescenceRequest(fence.LeaseId, fence.RootDigest, fence.Epoch, "owner-proof");
        var verifier = Substitute.For<IRouteCatalogQuiescenceVerifier>();
        verifier.VerifyAsync(reconcile, fence with { SharedAdmissionSealed = true }, token).Returns(async _ =>
        {
            (await fixture.Store.ReadAsync(token)).State.Leases.Single().SharedAdmissionSealed.ShouldBe(true);
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.AcquireActiveGenerationAsync(token));
            throw new RouteCatalogUnavailableException("Shared remote work is still executing.");
        });
        using var peer = fixture.CreateAuthority(fixture.Hosts[1], verifier: verifier);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => peer.ReconcileAsync(reconcile, token));
        await verifier.Received(1).VerifyAsync(reconcile, fence, token);
        (await fixture.Store.ReadAsync(token)).State.Leases.Single().SharedAdmissionSealed.ShouldBe(true);
        await owner.DisposeAsync();
        (await fixture.Store.ReadAsync(token)).State.Leases.ShouldBeEmpty();
    }

    [Fact]
    public async Task WarmAdmissionReservationKeepsTheFenceBeforeTheStrongReadReturns()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        CancellationToken token = TestContext.Current.CancellationToken;
        var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new CatalogAdmissionRebaseStore(fixture.Store)
        {
            BeforeRead = async (read, cancellation) =>
            {
                if (read != 3) return;
                reading.SetResult();
                await response.Task.WaitAsync(cancellation);
            },
        };
        var verifier = Substitute.For<IRouteCatalogQuiescenceVerifier>();
        using var authority = Create(fixture, store, verifier: verifier);
        await using var owner = await authority.AcquireActiveGenerationAsync(token);
        var fence = (await fixture.Store.ReadAsync(token)).State.Leases.Single();
        Task<RouteCatalogDispatchLease> admission = authority.AcquireActiveGenerationAsync(token);
        try
        {
            await reading.Task.WaitAsync(token);
            await owner.DisposeAsync();
            (await fixture.Store.ReadAsync(token)).State.Leases.ShouldBe([fence]);
            var reconcile = new RouteCatalogQuiescenceRequest(fence.LeaseId, fence.RootDigest, fence.Epoch, "owner-proof");
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.ReconcileAsync(reconcile, token));
            await verifier.DidNotReceiveWithAnyArgs().VerifyAsync(default!, default!, default);
        }
        finally { response.SetResult(); }
        await using var joined = await admission;
        joined.ActiveGeneration.ShouldBeSameAs(owner.ActiveGeneration);
        store.Exchanges.ShouldBe(1);
    }

    [Fact]
    public async Task ThrownOpeningResponseLossNeverAdmitsAndLeavesAnyPersistedFenceForOwnerProof()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var store = new CatalogAdmissionRebaseStore(fixture.Store)
        {
            Exchange = async (attempt, state, etag, cancellation) =>
            {
                if (attempt != 1) return null;
                (await fixture.Store.CompareExchangeAsync(state, etag, cancellation)).ShouldBeTrue();
                throw new RouteCatalogUnavailableException("CAS response lost.", RouteCatalogRefusalReason.Backend);
            },
        };
        var verifier = Substitute.For<IRouteCatalogQuiescenceVerifier>();
        using var authority = Create(fixture, store, verifier: verifier);
        CancellationToken token = TestContext.Current.CancellationToken;
        var refused = await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.AcquireActiveGenerationAsync(token));
        refused.Category.ShouldBe(RouteCatalogRefusalReason.Backend);
        var uncertain = (await fixture.Store.ReadAsync(token)).State.Leases.Single();
        await using (var next = await authority.AcquireActiveGenerationAsync(token))
        {
            next.ActiveGeneration!.Fence.LeaseId.ShouldNotBe(uncertain.LeaseId);
        }
        (await fixture.Store.ReadAsync(token)).State.Leases.ShouldBe([uncertain]);
        var reconcile = new RouteCatalogQuiescenceRequest(uncertain.LeaseId, uncertain.RootDigest, uncertain.Epoch, "owner-proof");
        await authority.ReconcileAsync(reconcile, token);
        (await fixture.Store.ReadAsync(token)).State.Leases.ShouldBeEmpty();
    }

    [Fact]
    public async Task LocalAuthorityDriftDuringConfirmedOpeningRefusesAndReleasesTheExactFence()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var local = new RouteCatalogHostOptions { Identity = fixture.Hosts[0], Profile = "Development" };
        var store = new CatalogAdmissionRebaseStore(fixture.Store)
        {
            Exchange = async (attempt, state, etag, cancellation) =>
            {
                if (attempt != 1) return null;
                (await fixture.Store.CompareExchangeAsync(state, etag, cancellation)).ShouldBeTrue();
                local.Identity = fixture.Hosts[1];
                return true;
            },
        };
        using var authority = new RouteCatalogAuthority(store, Substitute.For<IRouteCatalogTrustValidator>(),
            Options.Create(local), new ObservedRouteCatalogHost(_ => Task.FromResult(fixture.Observations[0])));
        CancellationToken token = TestContext.Current.CancellationToken;
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.AcquireActiveGenerationAsync(token));
        (await fixture.Store.ReadAsync(token)).State.Leases.ShouldBeEmpty();
        store.Exchanges.ShouldBe(2);
    }

    [Fact]
    public async Task FailedReleaseCanRetryWithoutJoiningOrRemovingAReplacementFence()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var store = new CatalogAdmissionRebaseStore(fixture.Store)
        {
            Exchange = (attempt, _, _, _) => attempt == 2
                ? Task.FromException<bool?>(new IOException("Release response unavailable."))
                : Task.FromResult<bool?>(null),
        };
        using var authority = Create(fixture, store);
        CancellationToken token = TestContext.Current.CancellationToken;
        RouteCatalogDispatchLease prior = await authority.AcquireActiveGenerationAsync(token);
        await Should.ThrowAsync<IOException>(() => prior.DisposeAsync().AsTask());
        await using (RouteCatalogDispatchLease replacement = await authority.AcquireActiveGenerationAsync(token))
        {
            replacement.ActiveGeneration.ShouldNotBeSameAs(prior.ActiveGeneration);
            (await fixture.Store.ReadAsync(token)).State.Leases.Count.ShouldBe(2);
            await prior.DisposeAsync();
            (await fixture.Store.ReadAsync(token)).State.Leases.Single().ShouldBe(replacement.ActiveGeneration!.Fence);
        }
        (await fixture.Store.ReadAsync(token)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("changed")]
    [InlineData("duplicate")]
    public async Task StrongReadMustContainTheExactSharedFence(string corruption)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var store = new CatalogAdmissionRebaseStore(fixture.Store);
        using var authority = Create(fixture, store);
        CancellationToken token = TestContext.Current.CancellationToken;
        await using RouteCatalogDispatchLease prior = await authority.AcquireActiveGenerationAsync(token);
        store.TransformRead = (_, state) => state with
        {
            Leases = corruption switch
            {
                "missing" => [],
                "changed" => [state.Leases[0] with { Sequence = state.LastLease + 1 }],
                _ => [state.Leases[0], state.Leases[0]],
            },
        };
        try { await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.AcquireActiveGenerationAsync(token)); }
        finally { store.TransformRead = null; }
        store.Exchanges.ShouldBe(1);
    }

    [Theory]
    [InlineData("epoch")]
    [InlineData("proof")]
    [InlineData("binding")]
    [InlineData("capacity")]
    public async Task WarmAdmissionRefusesDurableAuthorityDriftWithTheExactFencePresent(string change)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var store = new CatalogAdmissionRebaseStore(fixture.Store);
        using var authority = Create(fixture, store);
        CancellationToken token = TestContext.Current.CancellationToken;
        await using RouteCatalogDispatchLease prior = await authority.AcquireActiveGenerationAsync(token);
        store.TransformRead = (_, state) => change switch
        {
            "epoch" => state with { Epoch = state.Epoch + 1 },
            "proof" => state with { Committed = state.Committed! with { TrustProof = "changed-proof" } },
            "binding" => state with { Committed = state.Committed! with { AttemptId = "changed-attempt" } },
            _ => state with { Leases = [state.Leases[0], .. Enumerable.Range(1, 4095).Select(index => state.Leases[0] with { LeaseId = $"peer-{index}" })] },
        };
        try { await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.AcquireActiveGenerationAsync(token)); }
        finally { store.TransformRead = null; }
        store.Exchanges.ShouldBe(1);
    }

    [Fact]
    public async Task LastReleasePreservesAChangedSameIdFence()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        using var authority = Create(fixture, fixture.Store);
        CancellationToken token = TestContext.Current.CancellationToken;
        var owner = await authority.AcquireActiveGenerationAsync(token);
        var (state, etag) = await fixture.Store.ReadAsync(token);
        var expected = state.Leases.Single();
        var changed = expected with { HostId = fixture.Hosts[1].HostId };
        (await fixture.Store.CompareExchangeAsync(state with { Leases = [changed] }, etag, token)).ShouldBeTrue();
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => owner.DisposeAsync().AsTask());
        var (persisted, currentEtag) = await fixture.Store.ReadAsync(token);
        persisted.Leases.ShouldBe([changed]);
        (await fixture.Store.CompareExchangeAsync(persisted with { Leases = [expected] }, currentEtag, token)).ShouldBeTrue();
        await owner.DisposeAsync();
        (await fixture.Store.ReadAsync(token)).State.Leases.ShouldBeEmpty();
    }

    private static RouteCatalogAuthority Create(RouteCatalogTestFixture fixture, IRouteCatalogStateStore store,
        IRouteCatalogTrustValidator? trust = null, Func<CancellationToken, Task<RouteCatalogHostObservation>>? observe = null,
        IRouteCatalogQuiescenceVerifier? verifier = null)
        => new(store, trust ?? Substitute.For<IRouteCatalogTrustValidator>(),
            Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts[0], Profile = "Development" }),
            new ObservedRouteCatalogHost(observe ?? (_ => Task.FromResult(fixture.Observations[0]))), verifier);
}
