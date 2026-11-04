using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Fresh admission may rebase inventory only across the exact independently validated authority.</summary>
public sealed class CatalogAdmissionRebaseTests
{
    [Fact]
    public async Task EveryCaptureCanAdvanceForeignInventoryWithoutLosingPeersOrUsingItsOldETag()
    {
        var fixture = await FixtureAsync();
        var store = new CatalogAdmissionRebaseStore(fixture.Store);
        int captures = 0;
        var authority = Authority(fixture, store, async (_, token) =>
        {
            int capture = ++captures;
            await ChangeAsync(fixture, state => state with
            {
                LastLease = state.LastLease + 1,
                Leases = [.. state.Leases, new(state.LastLease + 1, "peer-" + capture, fixture.Hosts[1].HostId, state.Committed!.RootDigest, state.Epoch)],
                Previous = state.Committed,
            }, token);
        });
        store.Exchange = (attempt, _, _, _) => Task.FromResult<bool?>(attempt == 1 ? false : null);
        await using (var lease = await authority.AcquireAsync(TestContext.Current.CancellationToken))
        {
            captures.ShouldBe(2); store.Reads.ShouldBe(4); store.Exchanges.ShouldBe(2);
            store.ExchangeETags.ShouldBe([store.ReadETags[1], store.ReadETags[3]]);
            store.ReadETags[0].ShouldNotBe(store.ReadETags[1]);
            var state = (await fixture.Store.ReadAsync(default)).State;
            state.LastLease.ShouldBe(3); state.Leases.Count.ShouldBe(3);
            state.Leases.Where(fence => fence.LeaseId.StartsWith("peer-", StringComparison.Ordinal)).Count().ShouldBe(2);
            state.Previous.ShouldNotBeNull();
            state.Attempts.ShouldHaveSingleItem();
            lease.Snapshot.Epoch.ShouldBe(state.Epoch);
        }
        (await fixture.Store.ReadAsync(default)).State.Leases.Select(fence => fence.LeaseId).ShouldBe(["peer-1", "peer-2"]);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("missing")]
    [InlineData("epoch")]
    [InlineData("committed")]
    [InlineData("binding")]
    [InlineData("proof")]
    [InlineData("bytes")]
    public async Task ChangedAuthorityCannotInheritThePriorObservationOrTrust(string change)
    {
        var fixture = await FixtureAsync();
        var store = new CatalogAdmissionRebaseStore(fixture.Store);
        int captures = 0;
        var authority = Authority(fixture, store, async (_, token) =>
        {
            if (++captures > 1) throw new InvalidOperationException("fresh observation required");
            await ChangeAsync(fixture, state => change switch
            {
                "pending" => state with { Pending = fixture.Candidate(2, state.Committed!.RootDigest) },
                "missing" => state with { Committed = null },
                "epoch" => state with { Epoch = state.Epoch + 1 },
                "committed" => state with { Committed = fixture.Candidate(2, state.Committed!.RootDigest) },
                "binding" => state with { Committed = state.Committed! with { Owner = "different-owner" } },
                "proof" => state with { Committed = state.Committed! with { TrustProof = state.Committed.TrustProof + "-changed" } },
                _ => state with { Committed = state.Committed! with { CanonicalCatalog = [.. state.Committed.CanonicalCatalog, 32] } },
            }, token);
        });
        await Should.ThrowAsync<Exception>(() => authority.AcquireAsync(TestContext.Current.CancellationToken));
        store.Exchanges.ShouldBe(0);
        captures.ShouldBe(change is "epoch" or "committed" or "binding" or "proof" ? 2 : 1);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("bytes")]
    [InlineData("hosts")]
    public async Task MutableReadAliasesCannotRewriteThePrivatelyValidatedActivation(string field)
    {
        var fixture = await FixtureAsync();
        var initial = (await fixture.Store.ReadAsync(default)).State;
        byte[] bytes = initial.Committed!.CanonicalCatalog.ToArray();
        RouteCatalogHostIdentity[] hosts = [.. initial.Committed.RequiredHosts];
        var alias = initial.Committed with { CanonicalCatalog = bytes, RequiredHosts = hosts };
        var store = new CatalogAdmissionRebaseStore(fixture.Store)
        {
            TransformRead = (read, state) => read == 1 ? state with { Committed = alias } : state,
        };
        int captures = 0;
        var authority = Authority(fixture, store, async (observed, token) =>
        {
            if (++captures > 1) throw new InvalidOperationException("fresh observation required");
            observed.CanonicalCatalog.ShouldNotBeSameAs(bytes);
            observed.RequiredHosts.ShouldNotBeSameAs(hosts);
            if (field == "bytes") bytes[0] ^= 1;
            else hosts[0] = hosts[1];
            // A concurrent backend change shares the changed original values, never the owned snapshot.
            await ChangeAsync(fixture, state => state with { Committed = alias }, token);
        });
        await Should.ThrowAsync<Exception>(() => authority.AcquireAsync(TestContext.Current.CancellationToken));
        store.Exchanges.ShouldBe(0);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("cap")]
    [InlineData("overflow")]
    public async Task RefreshedInventoryLimitsRemainClosed(string limit)
    {
        var fixture = await FixtureAsync();
        var store = new CatalogAdmissionRebaseStore(fixture.Store);
        var authority = Authority(fixture, store, (_, token) => ChangeAsync(fixture, state => state with
        {
            LastLease = limit == "overflow" ? long.MaxValue : 4096,
            Leases = limit == "overflow" ? [] : [.. Enumerable.Range(1, 4096).Select(sequence =>
                new RouteCatalogDispatchFence(sequence, "peer-" + sequence, fixture.Hosts[1].HostId, state.Committed!.RootDigest, state.Epoch))],
        }, token));
        if (limit == "overflow") await Should.ThrowAsync<OverflowException>(() => authority.AcquireAsync(TestContext.Current.CancellationToken));
        else await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.AcquireAsync(TestContext.Current.CancellationToken));
        store.Exchanges.ShouldBe(0);
        (await fixture.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(limit == "cap" ? 4096 : 0);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("failure")]
    [InlineData("identity")]
    [InlineData("profile")]
    [InlineData("coordinates")]
    public async Task RefreshFailureCancellationOrChangedLocalIdentityCannotCreateAFence(string scenario)
    {
        var fixture = await FixtureAsync();
        var store = new CatalogAdmissionRebaseStore(fixture.Store);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var local = new RouteCatalogHostOptions { Identity = fixture.Hosts[0], Profile = "Development", DevelopmentFixture = true };
        store.BeforeRead = (read, _) =>
        {
            if (read == 2)
            {
                if (scenario == "cancel") cancelled.Cancel();
                else if (scenario == "failure") throw new IOException("refresh unavailable");
                else if (scenario == "identity") local.Identity = fixture.Hosts[1];
                else if (scenario == "profile") local.Profile = "Changed";
                else fixture.Store.Coordinates = new("changed", "changed", "http://127.0.0.1:3501/");
            }
            return Task.CompletedTask;
        };
        await Should.ThrowAsync<Exception>(() => Authority(fixture, store, local: local).AcquireAsync(cancelled.Token));
        store.Exchanges.ShouldBe(0);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingSuccessorDoesNotStrandAnAlreadyRecordedOwnFence(bool pendingOnRefresh)
    {
        var fixture = await FixtureAsync();
        var store = new CatalogAdmissionRebaseStore(fixture.Store);
        RouteCatalogActivationState? written = null;
        store.Exchange = async (attempt, state, etag, token) =>
        {
            if (attempt != 1) return null;
            written = state;
            if (!pendingOnRefresh)
            {
                (await fixture.Store.CompareExchangeAsync(state, etag, token)).ShouldBeTrue();
                await ChangeAsync(fixture, current => current with { Pending = fixture.Candidate(2, current.Committed!.RootDigest) }, token);
            }
            return false;
        };
        int captures = 0;
        Task Capture(RouteCatalogActivation _, CancellationToken token)
        {
            // An independent writer installs the exact previously proposed fence while capture
            // awaits. Both reads remain actual strong snapshots with their original exact ETags.
            return pendingOnRefresh && ++captures == 2
                ? ChangeAsync(fixture, current => current with
                {
                    LastLease = written!.LastLease,
                    Leases = written.Leases,
                    Pending = fixture.Candidate(2, current.Committed!.RootDigest),
                }, token)
                : Task.CompletedTask;
        }
        await using (await Authority(fixture, store, Capture).AcquireAsync(TestContext.Current.CancellationToken))
        {
            written.ShouldNotBeNull(); store.Exchanges.ShouldBe(1);
            var state = (await fixture.Store.ReadAsync(default)).State;
            state.Leases.ShouldHaveSingleItem(); state.Pending.ShouldNotBeNull();
        }
        var final = (await fixture.Store.ReadAsync(default)).State;
        final.Leases.ShouldBeEmpty(); final.Pending.ShouldNotBeNull();
    }

    [Fact]
    public async Task LostSuccessfulCasResponseRetainsOneExactOwnFenceWithoutDuplicatingAdmission()
    {
        var fixture = await FixtureAsync();
        var store = new CatalogAdmissionRebaseStore(fixture.Store);
        store.Exchange = async (attempt, state, etag, token) =>
        {
            if (attempt != 1) return null;
            (await fixture.Store.CompareExchangeAsync(state, etag, token)).ShouldBeTrue();
            return false;
        };
        int captures = 0;
        await using (await Authority(fixture, store, (_, _) => { captures++; return Task.CompletedTask; }).AcquireAsync(TestContext.Current.CancellationToken))
        {
            captures.ShouldBe(2); store.Reads.ShouldBe(3); store.Exchanges.ShouldBe(1);
            var state = (await fixture.Store.ReadAsync(default)).State;
            state.Leases.ShouldHaveSingleItem(); state.LastLease.ShouldBe(1);
        }
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Fact]
    public async Task WinningFenceAndSnapshotKeepTheirValidatedLocalBindingUntilCleanup()
    {
        var fixture = await FixtureAsync();
        var coordinates = fixture.Store.Coordinates;
        var local = new RouteCatalogHostOptions { Identity = fixture.Hosts[0], Profile = "Development", DevelopmentFixture = true };
        var store = new CatalogAdmissionRebaseStore(fixture.Store);
        store.Exchange = async (_, state, etag, token) =>
        {
            bool applied = await fixture.Store.CompareExchangeAsync(state, etag, token);
            local.Identity = fixture.Hosts[1];
            fixture.Store.Coordinates = coordinates with { StateKey = "later-cell" };
            return applied;
        };
        await using (var lease = await Authority(fixture, store, local: local).AcquireAsync(TestContext.Current.CancellationToken))
        {
            lease.Snapshot.LocalAppId.ShouldBe(fixture.Hosts[0].AppId);
            lease.Snapshot.AuthorityCoordinates.ShouldBe(coordinates);
            (await fixture.Store.ReadAsync(default)).State.Leases.Single().HostId.ShouldBe(fixture.Hosts[0].HostId);
        }
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("host")]
    [InlineData("root")]
    [InlineData("epoch")]
    [InlineData("sequence")]
    [InlineData("duplicate")]
    public async Task ResponseLossCannotReturnAMisboundOrDuplicateOwnFence(string field)
    {
        var fixture = await FixtureAsync();
        var store = new CatalogAdmissionRebaseStore(fixture.Store);
        store.Exchange = async (_, state, etag, token) =>
        {
            var fence = state.Leases.Single();
            var changed = field switch
            {
                "host" => fence with { HostId = fixture.Hosts[1].HostId },
                "root" => fence with { RootDigest = "different-root" },
                "epoch" => fence with { Epoch = state.Epoch + 1 },
                "sequence" => fence with { Sequence = state.LastLease + 1 },
                _ => fence,
            };
            (await fixture.Store.CompareExchangeAsync(state with { Leases = field == "duplicate" ? [changed, changed] : [changed] }, etag, token)).ShouldBeTrue();
            return false;
        };
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => Authority(fixture, store).AcquireAsync(TestContext.Current.CancellationToken));
        store.Exchanges.ShouldBe(1);
        // A malformed durable record is retained for explicit owner recovery, never silently erased.
        (await fixture.Store.ReadAsync(default)).State.Leases.Count.ShouldBe(field == "duplicate" ? 2 : 1);
    }

    [Fact]
    public async Task CompletedValidatorCannotRewriteThePrivateComparisonSnapshotThroughItsActivationArgument()
    {
        var fixture = await FixtureAsync();
        var store = new CatalogAdmissionRebaseStore(fixture.Store);
        var authority = Authority(fixture, store, afterTrust: observed => observed.CanonicalCatalog[0] ^= 1);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.AcquireAsync(TestContext.Current.CancellationToken));
        store.Exchanges.ShouldBe(0);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    private static async Task<RouteCatalogTestFixture> FixtureAsync()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        return fixture;
    }

    private static async Task ChangeAsync(RouteCatalogTestFixture fixture, Func<RouteCatalogActivationState, RouteCatalogActivationState> change, CancellationToken token)
    {
        var (state, etag) = await fixture.Store.ReadAsync(token);
        (await fixture.Store.CompareExchangeAsync(change(state), etag, token)).ShouldBeTrue();
    }

    private static RouteCatalogAuthority Authority(RouteCatalogTestFixture fixture, IRouteCatalogStateStore store,
        Func<RouteCatalogActivation, CancellationToken, Task>? capture = null, RouteCatalogHostOptions? local = null, Action<RouteCatalogActivation>? afterTrust = null)
    {
        var options = Options.Create(local ?? new RouteCatalogHostOptions { Identity = fixture.Hosts[0], Profile = "Development", DevelopmentFixture = true });
        var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns("Development");
        var observer = Substitute.For<IRouteCatalogHostObserver>();
        observer.ObserveAsync(Arg.Any<RouteCatalogActivation>(), Arg.Any<RouteCatalogStateCoordinates>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                if (capture is not null) await capture(call.Arg<RouteCatalogActivation>(), call.Arg<CancellationToken>());
                return fixture.Hosts[0];
            });
        IRouteCatalogTrustValidator actualTrust = new DevelopmentRouteCatalogTrustValidator(options, environment);
        if (afterTrust is null) return new(store, actualTrust, options, observer);
        var trust = Substitute.For<IRouteCatalogTrustValidator>();
        trust.ValidateAsync(Arg.Any<RouteCatalogActivation>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var activation = call.Arg<RouteCatalogActivation>();
            await actualTrust.ValidateAsync(activation, call.Arg<string>(), call.Arg<CancellationToken>());
            afterTrust(activation);
        });
        return new(store, trust, options, observer);
    }
}
