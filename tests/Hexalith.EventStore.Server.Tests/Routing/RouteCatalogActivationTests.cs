using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class RouteCatalogActivationTests
{
    [Fact]
    public async Task DeclarationsAloneCannotActivateOrServe()
    {
        var fixture = new RouteCatalogTestFixture();
        foreach (RouteCatalogAuthority host in fixture.Authorities)
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => host.AcquireAsync());
        (var state, _) = await fixture.Store.ReadAsync(default);
        state.Committed.ShouldBeNull();
        state.Leases.ShouldBeEmpty();
    }

    [Fact]
    public async Task EveryExactHostMustAttestTheSameCompleteGeneration()
    {
        var fixture = new RouteCatalogTestFixture();
        RouteCatalogActivation candidate = fixture.Candidate();
        await fixture.Authorities[0].PrepareAsync(candidate);
        await fixture.Authorities[0].ReadyAsync(candidate.AttemptId);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].CommitAsync(candidate.AttemptId));
        foreach (RouteCatalogAuthority host in fixture.Authorities.Skip(1)) await host.ReadyAsync(candidate.AttemptId);
        await fixture.Authorities[0].CommitAsync(candidate.AttemptId);
        foreach (RouteCatalogAuthority host in fixture.Authorities)
        {
            var readback = await host.ReadbackAsync();
            readback.RootDigest.ShouldBe(candidate.RootDigest);
            readback.RequiredHosts.ShouldBe(fixture.Hosts, ignoreOrder: true);
            readback.Serving.ShouldBeTrue();
        }
        (var state, _) = await fixture.Store.ReadAsync(default);
        state.Committed!.CanonicalCatalog.ShouldBe(candidate.CanonicalCatalog);
        state.Pending.ShouldBeNull();
        state.Epoch.ShouldBe(1);
    }

    [Fact]
    public async Task PrepareFencesNewDispatchAndCannotCommitUntilDurableLeasesDrain()
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        RouteCatalogDispatchLease inFlight = await fixture.Authorities[1].AcquireAsync();
        var second = fixture.Candidate(2, first.RootDigest);
        await fixture.Authorities[0].PrepareAsync(second);
        foreach (RouteCatalogAuthority host in fixture.Authorities)
        {
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => host.AcquireAsync());
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => host.ReadyAsync(second.AttemptId));
        }
        (var draining, _) = await fixture.Store.ReadAsync(default);
        draining.Leases.Count.ShouldBe(1);
        draining.Committed!.RootDigest.ShouldBe(first.RootDigest);
        inFlight.Snapshot.RootDigest.ShouldBe(first.RootDigest);
        await inFlight.DisposeAsync();
        foreach (RouteCatalogAuthority host in fixture.Authorities) await host.ReadyAsync(second.AttemptId);
        await fixture.Authorities[0].CommitAsync(second.AttemptId);
        (var committed, _) = await fixture.Store.ReadAsync(default);
        committed.Leases.ShouldBeEmpty();
        committed.Committed!.RootDigest.ShouldBe(second.RootDigest);
        committed.Previous!.RootDigest.ShouldBe(first.RootDigest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostFailureBeforeCommitRecoversOnlyThePriorCompleteAuthority(bool ready)
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        var second = fixture.Candidate(2, first.RootDigest);
        await fixture.Authorities[0].PrepareAsync(second);
        if (ready) await fixture.Authorities[0].ReadyAsync(second.AttemptId);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Authorities[0].CommitAsync(second.AttemptId));
        await fixture.Authorities[0].RollbackAsync(new(second.AttemptId, RouteCatalogActivationBinding.ComputeDigest(second), (await fixture.Authorities[0].ReadbackAsync()).Epoch, "pending"));
        foreach (RouteCatalogAuthority host in fixture.Authorities) (await host.ReadbackAsync()).RootDigest.ShouldBe(first.RootDigest);
        (var state, _) = await fixture.Store.ReadAsync(default);
        state.Pending.ShouldBeNull();
        state.Ready.ShouldBeEmpty();
        state.Committed!.RootDigest.ShouldBe(first.RootDigest);
    }

    [Fact]
    public async Task RestartAndRollbackReadPersistedCompleteGenerationsAndAdvanceTheFence()
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        var second = fixture.Candidate(2, first.RootDigest);
        await fixture.ActivateAsync(second);
        var restarted = fixture.CreateAuthority(fixture.Hosts[0]);
        (await restarted.ReadbackAsync()).RootDigest.ShouldBe(second.RootDigest);
        await restarted.RollbackAsync(new(second.AttemptId, RouteCatalogActivationBinding.ComputeDigest(second), (await restarted.ReadbackAsync()).Epoch, "committed"));
        (await restarted.ReadbackAsync()).Epoch.ShouldBe(3);
        (await restarted.ReadbackAsync()).RootDigest.ShouldBe(first.RootDigest);
        var drifted = fixture.CreateAuthority(fixture.Hosts[0] with { TopologyDigest = RouteCatalogTestFixture.Hash("drift") });
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => drifted.AcquireAsync());
    }

    [Fact]
    public async Task ConcurrentAttemptsCannotOverwritePreparedAuthority()
    {
        var fixture = new RouteCatalogTestFixture();
        var proposals = new[] { fixture.Candidate(attempt: "first"), fixture.Candidate(attempt: "second") };
        var accepted = await Task.WhenAll(proposals.Select(async proposal =>
        {
            try { await fixture.Authorities[0].PrepareAsync(proposal); return proposal.AttemptId; }
            catch (RouteCatalogUnavailableException) { return null; }
        }));
        accepted.Count(a => a is not null).ShouldBe(1);
        (var state, _) = await fixture.Store.ReadAsync(default);
        state.Pending!.AttemptId.ShouldBe(accepted.Single(a => a is not null));
        state.Committed.ShouldBeNull();
    }

    [Theory]
    [InlineData("root")]
    [InlineData("bytes")]
    [InlineData("hosts")]
    [InlineData("trust")]
    [InlineData("predecessor")]
    public async Task InvalidCandidateCannotReplacePriorAuthority(string fault)
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        var valid = fixture.Candidate(2, first.RootDigest);
        var invalid = fault switch
        {
            "root" => valid with { RootDigest = new string('0', 64) },
            "bytes" => valid with { CanonicalCatalog = [.. valid.CanonicalCatalog, (byte)'\n'] },
            "hosts" => valid with { RequiredHosts = valid.RequiredHosts.Take(1).ToArray() },
            "trust" => valid with { TrustProof = "bad-proof" },
            _ => valid with { PredecessorRootDigest = new string('f', 64) },
        };
        Exception exception = await Should.ThrowAsync<Exception>(() => fixture.Authorities[0].PrepareAsync(invalid));
        (exception is RouteCatalogValidationException or RouteCatalogUnavailableException).ShouldBeTrue();
        (var state, _) = await fixture.Store.ReadAsync(default);
        state.Pending.ShouldBeNull();
        state.Committed!.RootDigest.ShouldBe(first.RootDigest);
    }

    [Fact]
    public async Task DevelopmentFixtureCannotGrantProductionReadiness()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var production = fixture.CreateAuthority(fixture.Hosts[0], "Production");
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => production.ReadbackAsync());
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => production.AcquireAsync());
    }

    [Fact]
    public async Task CapturedSnapshotCannotBeChangedThroughMutableCallerCollectionsOrBytes()
    {
        var fixture = new RouteCatalogTestFixture();
        var candidate = fixture.Candidate();
        await fixture.ActivateAsync(candidate);
        candidate.CanonicalCatalog[0] = (byte)'x';
        RouteCatalogDispatchLease lease = await fixture.Authorities[0].AcquireAsync();
        byte[] exposed = lease.Snapshot.GetCanonicalBytes();
        exposed[0] = (byte)'x';
        RouteCatalogCodec.Decode(lease.Snapshot.GetCanonicalBytes(), lease.Snapshot.RootDigest).ShouldNotBeNull();
        Should.Throw<NotSupportedException>(() => ((IList<RouteCatalogEntry>)lease.Snapshot.Envelope.Routes)[0] = lease.Snapshot.Envelope.Routes[0] with { AppId = "attacker" });
        await lease.DisposeAsync();
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }
}
