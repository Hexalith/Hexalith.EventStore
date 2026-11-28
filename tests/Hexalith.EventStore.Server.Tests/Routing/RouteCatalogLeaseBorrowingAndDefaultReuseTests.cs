using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Validates default-mode computational reuse and issuer-bound nested lease borrowing under drift, concurrency, and failure.</summary>
public sealed class RouteCatalogLeaseBorrowingAndDefaultReuseTests
{
    /// <summary>Default host options enable short admission and bounded exact-input memos across environments while honoring opt-out.</summary>
    [Fact]
    public async Task DefaultOptionsEnableShortAdmissionAndBoundedMemosWithoutDebugFlags()
    {
        var defaults = new RouteCatalogHostOptions();
        defaults.ScopedDecodeReuse.ShouldBeTrue();
        defaults.RetainValidatedCatalogBytes.ShouldBeTrue();

        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var options = new RouteCatalogHostOptions
        {
            Identity = fixture.Hosts[0],
            Profile = "Development",
        };
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        var trust = Substitute.For<IRouteCatalogTrustValidator>();
        trust.ValidateAsync(Arg.Any<RouteCatalogActivation>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var observedMemo = new List<(RouteCatalogDecodeScope? Scope, bool HasHostMemo)>();
        using var authority = new RouteCatalogAuthority(
            fixture.Store,
            trust,
            Options.Create(options),
            new ObservedRouteCatalogHost(_ =>
            {
                RouteCatalogDecodeScope? current = RouteCatalogDecodeScope.Current;
                observedMemo.Add((current, current?.HasHostMemo == true));
                return Task.FromResult(fixture.Observations[0]);
            }),
            environment: environment);

        await using (await authority.AcquireAsync(TestContext.Current.CancellationToken)) { }
        observedMemo[0].Scope.ShouldNotBeNull();
        observedMemo[0].HasHostMemo.ShouldBeTrue();

        options.ScopedDecodeReuse = false;
        await using (await authority.AcquireAsync(TestContext.Current.CancellationToken)) { }
        observedMemo[1].Scope.ShouldBeNull();
        observedMemo[1].HasHostMemo.ShouldBeFalse();
    }

    /// <summary>Same-issuer nested scopes borrow the open parent durable lease without adding extra Redis fences or releasing early.</summary>
    [Fact]
    public async Task NestedGatewayAcquireOrBorrowReusesOpenSameIssuerLeaseAndNeverReleasesParentEarly()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        int reads = 0;
        int exchanges = 0;
        var countingStore = Substitute.For<IRouteCatalogStateStore>();
        countingStore.Coordinates.Returns(_ => fixture.Store.Coordinates);
        countingStore.ReadAsync(Arg.Any<CancellationToken>()).Returns(call =>
        {
            Interlocked.Increment(ref reads);
            return fixture.Store.ReadAsync(call.Arg<CancellationToken>());
        });
        countingStore.CompareExchangeAsync(Arg.Any<RouteCatalogActivationState>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            Interlocked.Increment(ref exchanges);
            return fixture.Store.CompareExchangeAsync(
                call.Arg<RouteCatalogActivationState>(),
                call.Arg<string>(),
                call.Arg<CancellationToken>());
        });
        using RouteCatalogAuthority authority = CreateAuthority(fixture, countingStore);

        RouteCatalogDispatchLease parent = await authority.AcquireAsync(TestContext.Current.CancellationToken);
        await using (new RouteCatalogLeaseReleaser(parent))
        {
            int readsAfterParent = reads;
            int exchangesAfterParent = exchanges;
            string parentFenceId = (await fixture.Store.ReadAsync(default)).State.Leases.Single().LeaseId;

            for (int depth = 0; depth < 5; depth++)
            {
                RouteCatalogDispatchLease borrowed = await RouteCatalogAuthority.AcquireOrBorrowScopeAsync(
                    authority,
                    TestContext.Current.CancellationToken);
                borrowed.IsBorrowed.ShouldBeTrue();
                borrowed.Root.ShouldBeSameAs(parent);
                borrowed.Snapshot.ShouldBeSameAs(parent.Snapshot);
                await using (new RouteCatalogLeaseReleaser(borrowed))
                {
                    RouteCatalogDispatchLease deeplyBorrowed = await RouteCatalogAuthority.AcquireOrBorrowScopeAsync(
                        authority,
                        TestContext.Current.CancellationToken);
                    deeplyBorrowed.IsBorrowed.ShouldBeTrue();
                    deeplyBorrowed.Root.ShouldBeSameAs(parent);
                    await deeplyBorrowed.DisposeAsync();
                }

                (await fixture.Store.ReadAsync(default)).State.Leases.Single().LeaseId.ShouldBe(parentFenceId);
            }

            reads.ShouldBe(readsAfterParent);
            exchanges.ShouldBe(exchangesAfterParent);
        }

        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>A separate authority instance never borrows a lease issued by a different authority.</summary>
    [Fact]
    public async Task ForeignIssuerCannotBorrowAnotherAuthorityLease()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        using RouteCatalogAuthority authorityA = CreateAuthority(fixture, fixture.Store);
        using RouteCatalogAuthority authorityB = CreateAuthority(fixture, fixture.Store);

        RouteCatalogDispatchLease leaseA = await authorityA.AcquireAsync(TestContext.Current.CancellationToken);
        await using (new RouteCatalogLeaseReleaser(leaseA))
        {
            RouteCatalogDispatchLease leaseB = await RouteCatalogAuthority.AcquireOrBorrowScopeAsync(
                authorityB,
                TestContext.Current.CancellationToken);
            leaseB.IsBorrowed.ShouldBeFalse();
            leaseB.Root.ShouldNotBeSameAs(leaseA);
            await using (new RouteCatalogLeaseReleaser(leaseB))
            {
                (await fixture.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(2);
            }

            (await fixture.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(1);
        }

        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>A closed parent lease cannot be borrowed or used for execution through an escaped borrowed handle.</summary>
    [Fact]
    public async Task StaleOrDisposedParentLeaseCannotBeBorrowedAndClosedScopeFailsClosed()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        using RouteCatalogAuthority authority = CreateAuthority(fixture, fixture.Store);

        RouteCatalogDispatchLease parent = await authority.AcquireAsync(TestContext.Current.CancellationToken);
        RouteCatalogDispatchLease borrowed;
        using (RouteCatalogRemoteCall.EnterReceiver(parent, TestContext.Current.CancellationToken))
        {
            borrowed = await RouteCatalogAuthority.AcquireOrBorrowScopeAsync(authority, TestContext.Current.CancellationToken);
            borrowed.IsBorrowed.ShouldBeTrue();
            await parent.DisposeAsync();

            Should.Throw<RouteCatalogUnavailableException>(() => borrowed.RequireOpenForExecution());
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() =>
                RouteCatalogAuthority.AcquireOrBorrowScopeAsync(authority, TestContext.Current.CancellationToken));
        }

        await borrowed.DisposeAsync();
        RouteCatalogDispatchLease fresh = await RouteCatalogAuthority.AcquireOrBorrowScopeAsync(
            authority,
            TestContext.Current.CancellationToken);
        fresh.IsBorrowed.ShouldBeFalse();
        fresh.Root.ShouldNotBeSameAs(parent);
        await fresh.DisposeAsync();
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>Local identity, profile, or authority-coordinate drift fails closed when borrowing an active scope.</summary>
    [Theory]
    [InlineData("profile")]
    [InlineData("coordinates")]
    [InlineData("identity")]
    public async Task LocalHostIdentityOrCoordinateDriftFailsClosedOnBorrow(string drift)
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var localOptions = new RouteCatalogHostOptions
        {
            Identity = fixture.Hosts[0],
            Profile = "Development",
            DevelopmentFixture = true,
            RequireTechnicalHttpBinding = true,
        };
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        using var authority = new RouteCatalogAuthority(
            fixture.Store,
            new DevelopmentRouteCatalogTrustValidator(Options.Create(localOptions), environment),
            Options.Create(localOptions),
            new ObservedRouteCatalogHost(_ => Task.FromResult(fixture.Observations[0])),
            environment: environment);

        RouteCatalogDispatchLease parent = await authority.AcquireAsync(TestContext.Current.CancellationToken);
        await using (new RouteCatalogLeaseReleaser(parent))
        {
            switch (drift)
            {
                case "profile":
                    localOptions.Profile = "Production";
                    break;
                case "coordinates":
                    fixture.Store.Coordinates = fixture.Store.Coordinates with { StateKey = "drifted-key" };
                    break;
                case "identity":
                    localOptions.Identity = fixture.Hosts[0] with { InventoryDigest = "drifted-digest" };
                    break;
            }

            await Should.ThrowAsync<RouteCatalogUnavailableException>(() =>
                RouteCatalogAuthority.AcquireOrBorrowScopeAsync(authority, TestContext.Current.CancellationToken));
        }

        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>Ambiguous remote failure on a borrowed handle retains the parent durable fence until explicit reconciliation.</summary>
    [Fact]
    public async Task AmbiguousRemoteCallOnBorrowedLeaseRetainsParentDurableFenceForReconciliation()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var verifier = Substitute.For<IRouteCatalogQuiescenceVerifier>();
        verifier.VerifyAsync(Arg.Any<RouteCatalogQuiescenceRequest>(), Arg.Any<RouteCatalogDispatchFence>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        using RouteCatalogAuthority authority = CreateAuthority(fixture, fixture.Store, verifier);

        RouteCatalogDispatchLease parent = await authority.AcquireAsync(TestContext.Current.CancellationToken);
        RouteCatalogDispatchFence fence = (await fixture.Store.ReadAsync(default)).State.Leases.Single();
        var pendingRemote = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int> remoteCall;
        await using (new RouteCatalogLeaseReleaser(parent))
        {
            RouteCatalogDispatchLease borrowed = await RouteCatalogAuthority.AcquireOrBorrowScopeAsync(
                authority,
                TestContext.Current.CancellationToken);
            borrowed.IsBorrowed.ShouldBeTrue();
            await using (new RouteCatalogLeaseReleaser(borrowed))
            {
                remoteCall = RouteCatalogRemoteCall.InvokeAsync(() => pendingRemote.Task);
            }
        }

        (await fixture.Store.ReadAsync(default)).State.Leases.Single().ShouldBe(fence);
        pendingRemote.SetResult(42);
        (await remoteCall).ShouldBe(42);
        (await fixture.Store.ReadAsync(default)).State.Leases.Single().ShouldBe(fence);

        await authority.ReconcileAsync(
            new RouteCatalogQuiescenceRequest(fence.LeaseId, fence.RootDigest, fence.Epoch, "verified-owner-proof"),
            TestContext.Current.CancellationToken);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>Concurrent Prepare/Ready/Commit refuses Ready until the parent durable lease and borrowed scopes drain.</summary>
    [Fact]
    public async Task ConcurrentPrepareReadyCommitDrainsParentLeaseBeforeSuccessorCommit()
    {
        var fixture = new RouteCatalogTestFixture();
        RouteCatalogActivation initial = fixture.Candidate();
        await fixture.ActivateAsync(initial);
        using RouteCatalogAuthority authority = CreateAuthority(fixture, fixture.Store);
        RouteCatalogActivation successor = fixture.Candidate(2, initial.RootDigest);

        RouteCatalogDispatchLease parent = await authority.AcquireAsync(TestContext.Current.CancellationToken);
        await using (new RouteCatalogLeaseReleaser(parent))
        {
            RouteCatalogDispatchLease borrowed = await RouteCatalogAuthority.AcquireOrBorrowScopeAsync(
                authority,
                TestContext.Current.CancellationToken);
            await using (new RouteCatalogLeaseReleaser(borrowed))
            {
                await fixture.Authorities[0].PrepareAsync(successor, TestContext.Current.CancellationToken);
                RouteCatalogUnavailableException draining = await Should.ThrowAsync<RouteCatalogUnavailableException>(
                    () => authority.ReadyAsync(successor.AttemptId, TestContext.Current.CancellationToken));
                draining.Message.ShouldBe("Prior dispatch is still draining.");
            }
        }

        await authority.ReadyAsync(successor.AttemptId, TestContext.Current.CancellationToken);
        foreach (RouteCatalogAuthority peer in fixture.Authorities.Skip(1))
        {
            await peer.ReadyAsync(successor.AttemptId, TestContext.Current.CancellationToken);
        }

        await authority.CommitAsync(successor.AttemptId, TestContext.Current.CancellationToken);
        await using RouteCatalogDispatchLease afterCommit = await RouteCatalogAuthority.AcquireOrBorrowScopeAsync(
            authority,
            TestContext.Current.CancellationToken);
        afterCommit.IsBorrowed.ShouldBeFalse();
        afterCommit.Snapshot.RootDigest.ShouldBe(successor.RootDigest);
    }

    private static RouteCatalogAuthority CreateAuthority(
        RouteCatalogTestFixture fixture,
        IRouteCatalogStateStore store,
        IRouteCatalogQuiescenceVerifier? verifier = null)
    {
        var options = Options.Create(new RouteCatalogHostOptions
        {
            Identity = fixture.Hosts[0],
            Profile = "Development",
            DevelopmentFixture = true,
            RequireTechnicalHttpBinding = true,
        });
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        return new RouteCatalogAuthority(
            store,
            new DevelopmentRouteCatalogTrustValidator(options, environment),
            options,
            new ObservedRouteCatalogHost(_ => Task.FromResult(fixture.Observations[0])),
            verifier,
            environment);
    }
}
