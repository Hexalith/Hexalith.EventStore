using System.Diagnostics;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Proves private parallel observations retain serialized local state phases and independent fresh authority.</summary>
public sealed class ParallelCatalogObservationTests(ITestOutputHelper output)
{
#if DEBUG
    private static bool PrivateExpected => true;
#else
    private static bool PrivateExpected => false;
#endif

    /// <summary>Two selected observations reach one barrier together; final state reads/CAS remain mutually exclusive.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ParallelObservationsKeepFinalWritesSerializedAndFallbackUnchanged(bool selected)
    {
        var fixture = await FixtureAsync();
        var store = new CatalogAdmissionRebaseStore(fixture.Store);
        var entered = Signal(); var observed = Signal(); var resume = Signal(); var firstCas = Signal(); var finishCas = Signal();
        int captures = 0, writes = 0;
        long firstAt = 0, secondAt = 0;
        RouteCatalogAuthority? authority = null;
        using var local = authority = Create(fixture, store, async token =>
        {
            int capture = Interlocked.Increment(ref captures);
            RouteCatalogLocalAdmissionTurn.IsHeldBy(authority!).ShouldBe(!(selected && PrivateExpected));
            if (capture == 1) { firstAt = Stopwatch.GetTimestamp(); entered.SetResult(); }
            if (capture == 2) { secondAt = Stopwatch.GetTimestamp(); observed.SetResult(); }
            await resume.Task.WaitAsync(token);
            return fixture.Observations[0];
        }, selected);
        store.Exchange = async (_, _, _, token) =>
        {
            RouteCatalogLocalAdmissionTurn.IsHeldBy(authority).ShouldBeTrue();
            if (Interlocked.Increment(ref writes) == 1) { firstCas.SetResult(); await finishCas.Task.WaitAsync(token); }
            return null;
        };
        Task<RouteCatalogDispatchLease> first = authority.AcquireAsync(TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<RouteCatalogDispatchLease> second = authority.AcquireAsync(TestContext.Current.CancellationToken);
        if (selected && PrivateExpected) await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        else captures.ShouldBe(1);
        long releaseAt = Stopwatch.GetTimestamp();
        resume.SetResult();
        await firstCas.Task.WaitAsync(TimeSpan.FromSeconds(5));
        writes.ShouldBe(1);
        finishCas.SetResult();
        await using var firstLease = await first.WaitAsync(TimeSpan.FromSeconds(5));
        await using var secondLease = await second.WaitAsync(TimeSpan.FromSeconds(5));
        captures.ShouldBe(2);
        var state = (await fixture.Store.ReadAsync(default)).State;
        state.Leases.Count.ShouldBe(2);
        state.Leases.Select(fence => fence.Sequence).Distinct().Count().ShouldBe(2);
        store.ExchangeETags.Distinct().Count().ShouldBe(2);
        store.Exchanges.ShouldBe(2);
        RouteCatalogLocalAdmissionTurn.IsHeldBy(authority).ShouldBeFalse();
        output.WriteLine(JsonSerializer.Serialize(new { selected, privateLane = selected && PrivateExpected,
            overlappingCaptures = selected && PrivateExpected ? 2 : 1,
            firstCaptureToBarrierMs = Stopwatch.GetElapsedTime(firstAt, releaseAt).TotalMilliseconds,
            secondCaptureToBarrierMs = secondAt < releaseAt ? Stopwatch.GetElapsedTime(secondAt, releaseAt).TotalMilliseconds : 0 }));
        store.Exchange = null; // Completed-lease release has its own scheduling and is verified separately.
    }

    /// <summary>An unlocked observer's real completion uses the ordinary gate, then admission reads its fresh inventory.</summary>
    [Fact]
    public async Task ObserverCanCompletePriorLeaseWithoutRetainingALiveTurn()
    {
        var fixture = await FixtureAsync();
        RouteCatalogDispatchLease? prior = null;
        int captures = 0;
        RouteCatalogAuthority? authority = null;
        using var local = authority = Create(fixture, fixture.Store, async _ =>
        {
            captures++;
            RouteCatalogLocalAdmissionTurn.IsHeldBy(authority!).ShouldBe(!PrivateExpected);
            if (prior is not null) { await prior.DisposeAsync(); prior = null; }
            return fixture.Observations[0];
        });
        prior = await authority.AcquireAsync(TestContext.Current.CancellationToken);
        string priorId = (await fixture.Store.ReadAsync(default)).State.Leases.Single().LeaseId;
        await using var next = await authority.AcquireAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5));
        captures.ShouldBe(2);
        (await fixture.Store.ReadAsync(default)).State.Leases.Single().LeaseId.ShouldNotBe(priorId);
    }

    /// <summary>A context captured in the initial short phase is closed while another final phase is actually held.</summary>
    [Fact]
    public async Task EscapedInitialTurnCannotBypassLaterFinalPhase()
    {
        if (!PrivateExpected) return;
        var fixture = await FixtureAsync();
        var store = new CatalogAdmissionRebaseStore(fixture.Store);
        ExecutionContext? escaped = null;
        RouteCatalogAuthority? authority = null;
        using var local = authority = Create(fixture, store, _ => Task.FromResult(fixture.Observations[0]));
        await using var prior = await authority.AcquireAsync(TestContext.Current.CancellationToken);
        store.BeforeRead = (_, _) =>
        {
            if (escaped is null)
            {
                RouteCatalogLocalAdmissionTurn.IsHeldBy(authority).ShouldBeTrue();
                escaped = ExecutionContext.Capture();
            }
            return Task.CompletedTask;
        };
        var entered = Signal(); var resume = Signal();
        store.Exchange = async (_, _, _, token) => { entered.TrySetResult(); await resume.Task.WaitAsync(token); return null; };
        Task<RouteCatalogDispatchLease> acquiring = authority.AcquireAsync(TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task? releasing = null;
        ExecutionContext.Run(escaped!, _ =>
        {
            RouteCatalogLocalAdmissionTurn.IsHeldBy(authority).ShouldBeFalse();
            releasing = prior.DisposeAsync().AsTask();
        }, null);
        releasing.ShouldNotBeNull().IsCompleted.ShouldBeFalse();
        resume.SetResult();
        await using var next = await acquiring.WaitAsync(TimeSpan.FromSeconds(5));
        await releasing.WaitAsync(TimeSpan.FromSeconds(5));
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
    }

    /// <summary>Cancellation at either actual local wait creates no new fence and never over-releases the semaphore.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAtInitialOrFinalWaitPreservesGateAndOtherFence(bool finalWait)
    {
        if (!PrivateExpected && finalWait) return;
        var fixture = await FixtureAsync();
        var store = new CatalogAdmissionRebaseStore(fixture.Store);
        var firstObserved = Signal(); var secondObserved = Signal(); var observeFirst = Signal(); var observeSecond = Signal();
        var casEntered = Signal(); var finishCas = Signal();
        int captures = 0;
        using var authority = Create(fixture, store, async token =>
        {
            if (Interlocked.Increment(ref captures) == 1) { firstObserved.SetResult(); await observeFirst.Task.WaitAsync(token); }
            else { secondObserved.TrySetResult(); await observeSecond.Task.WaitAsync(token); }
            return fixture.Observations[0];
        });
        store.Exchange = async (attempt, _, _, token) =>
        {
            if (attempt == 1) { casEntered.SetResult(); await finishCas.Task.WaitAsync(token); }
            return null;
        };
        Task<RouteCatalogDispatchLease> first = authority.AcquireAsync(TestContext.Current.CancellationToken);
        await firstObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task<RouteCatalogDispatchLease>? canceled = null;
        if (finalWait)
        {
            canceled = authority.AcquireAsync(cancellation.Token);
            await secondObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        observeFirst.SetResult();
        await casEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        canceled ??= authority.AcquireAsync(cancellation.Token);
        observeSecond.SetResult();
        cancellation.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => canceled.WaitAsync(TimeSpan.FromSeconds(5)));
        store.Exchanges.ShouldBe(1);
        finishCas.SetResult();
        await using var lease = await first.WaitAsync(TimeSpan.FromSeconds(5));
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
        await using var later = await authority.AcquireAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5));
        (await fixture.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(2);
    }

    /// <summary>State changed during the unlocked owner observation must be freshly revalidated or refused.</summary>
    [Theory]
    [InlineData("epoch")]
    [InlineData("catalog")]
    [InlineData("proof")]
    [InlineData("pending")]
    public async Task ChangedAuthorityDuringObservationCannotInheritOldValidation(string changed)
    {
        var fixture = await FixtureAsync();
        var store = new CatalogAdmissionRebaseStore(fixture.Store);
        var entered = Signal(); var resume = Signal();
        int captures = 0;
        using var authority = Create(fixture, store, async token =>
        {
            if (Interlocked.Increment(ref captures) == 1) { entered.SetResult(); await resume.Task.WaitAsync(token); }
            return fixture.Observations[0];
        });
        Task<RouteCatalogDispatchLease> acquiring = authority.AcquireAsync(TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var before = await fixture.Store.ReadAsync(default);
        RouteCatalogActivation next = fixture.Candidate(2, before.State.Committed!.RootDigest);
        var changedState = changed switch
        {
            "epoch" => before.State with { Epoch = before.State.Epoch + 1 },
            "catalog" => before.State with { Committed = next, Epoch = before.State.Epoch + 1 },
            "proof" => before.State with { Committed = before.State.Committed with { TrustProof = "changed-owner-proof" } },
            _ => before.State with { Pending = next },
        };
        (await fixture.Store.CompareExchangeAsync(changedState, before.ETag, default)).ShouldBeTrue();
        resume.SetResult();
        if (changed is "proof" or "pending")
        {
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => acquiring.WaitAsync(TimeSpan.FromSeconds(5)));
            store.Exchanges.ShouldBe(0);
        }
        else
        {
            await using var lease = await acquiring.WaitAsync(TimeSpan.FromSeconds(5));
            lease.Snapshot.Epoch.ShouldBe(2);
            lease.Snapshot.RootDigest.ShouldBe(changed == "catalog" ? next.RootDigest : before.State.Committed.RootDigest);
        }
        captures.ShouldBe(changed == "pending" ? 1 : 2);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    /// <summary>Lost successful responses recover one exact fence; malformed own fences remain refused and retained.</summary>
    [Theory]
    [InlineData("valid")]
    [InlineData("pending")]
    [InlineData("host")]
    [InlineData("root")]
    [InlineData("epoch")]
    [InlineData("sequence")]
    [InlineData("duplicate")]
    public async Task ResponseLossKeepsExactRecoveryAndMalformedFenceRefusal(string changed)
    {
        var fixture = await FixtureAsync();
        var store = new CatalogAdmissionRebaseStore(fixture.Store);
        store.Exchange = async (attempt, state, etag, token) =>
        {
            if (attempt != 1) return null;
            var original = state.Leases.Single();
            var fence = changed switch
            {
                "host" => original with { HostId = fixture.Hosts[1].HostId },
                "root" => original with { RootDigest = new string('0', 64) },
                "epoch" => original with { Epoch = state.Epoch + 1 },
                "sequence" => original with { Sequence = state.LastLease + 1 },
                _ => original,
            };
            (await fixture.Store.CompareExchangeAsync(state with
            {
                Leases = changed == "duplicate" ? [fence, fence] : [fence],
                Pending = changed == "pending" ? fixture.Candidate(2, state.Committed!.RootDigest) : null,
            }, etag, token)).ShouldBeTrue();
            return false;
        };
        using var authority = Create(fixture, store, _ => Task.FromResult(fixture.Observations[0]));
        if (changed is "valid" or "pending")
        {
            await using (await authority.AcquireAsync(TestContext.Current.CancellationToken))
            {
                (await fixture.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
                store.Exchanges.ShouldBe(1);
            }
            (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        }
        else
        {
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.AcquireAsync(TestContext.Current.CancellationToken));
            (await fixture.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(changed == "duplicate" ? 2 : 1);
            store.Exchanges.ShouldBe(1);
        }
    }

    /// <summary>A live final CAS callback can complete its exact prior lease; the resulting ETag conflict forces fresh validation.</summary>
    [Fact]
    public async Task ReentrantFinalPhaseReleaseKeepsItsActualOwnerExceptionAndRetriesFreshly()
    {
        var fixture = await FixtureAsync();
        var store = new CatalogAdmissionRebaseStore(fixture.Store);
        int captures = 0;
        RouteCatalogAuthority? authority = null;
        using var local = authority = Create(fixture, store, _ => { captures++; return Task.FromResult(fixture.Observations[0]); });
        RouteCatalogDispatchLease? prior = await authority.AcquireAsync(TestContext.Current.CancellationToken);
        string priorId = (await fixture.Store.ReadAsync(default)).State.Leases.Single().LeaseId;
        store.Exchange = async (_, _, _, _) =>
        {
            if (prior is not null)
            {
                RouteCatalogLocalAdmissionTurn.IsHeldBy(authority).ShouldBeTrue();
                var completed = prior;
                prior = null;
                await completed.DisposeAsync();
            }
            return null;
        };
        await using var next = await authority.AcquireAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5));
        captures.ShouldBe(3);
        (await fixture.Store.ReadAsync(default)).State.Leases.Single().LeaseId.ShouldNotBe(priorId);
    }

    /// <summary>Changing the private selector during an unlocked callback cannot alter release ownership in that acquisition.</summary>
    [Fact]
    public async Task PrivateModeIsSelectedOnceAndNextCallUsesChangedFallback()
    {
        var fixture = await FixtureAsync();
        var options = new RouteCatalogHostOptions { Identity = fixture.Hosts[0], Profile = "Development", DevelopmentFixture = true,
            RequireTechnicalHttpBinding = true, ScopedDecodeReuse = true, RetainValidatedCatalogBytes = true };
        int captures = 0;
        RouteCatalogAuthority? authority = null;
        using var local = authority = Create(fixture, fixture.Store, _ =>
        {
            RouteCatalogLocalAdmissionTurn.IsHeldBy(authority!).ShouldBe(captures != 0 || !PrivateExpected);
            captures++;
            options.ScopedDecodeReuse = false;
            return Task.FromResult(fixture.Observations[0]);
        }, localOptions: options);
        await using var first = await authority.AcquireAsync(TestContext.Current.CancellationToken);
        await using var second = await authority.AcquireAsync(TestContext.Current.CancellationToken);
        (await fixture.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(2);
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task<RouteCatalogTestFixture> FixtureAsync()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        return fixture;
    }

    private static RouteCatalogAuthority Create(RouteCatalogTestFixture fixture, IRouteCatalogStateStore store,
        Func<CancellationToken, Task<RouteCatalogHostObservation>> capture, bool selected = true, RouteCatalogHostOptions? localOptions = null)
    {
        var options = Options.Create(localOptions ?? new RouteCatalogHostOptions { Identity = fixture.Hosts[0], Profile = "Development", DevelopmentFixture = true,
            RequireTechnicalHttpBinding = true, ScopedDecodeReuse = selected, RetainValidatedCatalogBytes = selected });
        var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns(Environments.Development);
        return new(store, new DevelopmentRouteCatalogTrustValidator(options, environment), options,
            new ObservedRouteCatalogHost(capture), environment: environment);
    }
}
