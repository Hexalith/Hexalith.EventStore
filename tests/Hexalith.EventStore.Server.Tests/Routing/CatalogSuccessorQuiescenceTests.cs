using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogSuccessorQuiescenceTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("unprepared")]
    [InlineData("no-verifier")]
    [InlineData("forged-proof")]
    [InlineData("old-local-identity")]
    [InlineData("old-trust")]
    [InlineData("wrong-predecessor")]
    [InlineData("wrong-epoch")]
    [InlineData("wrong-root")]
    [InlineData("unknown-lease")]
    [InlineData("blank-proof")]
    [InlineData("foreign-old-host")]
    [InlineData("missing-attempt")]
    [InlineData("duplicate-attempt")]
    [InlineData("changed-pending")]
    [InlineData("nonadvancing")]
    [InlineData("stale-observer")]
    [InlineData("missing-observer")]
    [InlineData("missing-coordinates")]
    public async Task ChangedCoordinatesRequirePreparedSuccessorAndExactOwnerVerifiedOldFence(string scenario)
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate();
        await fixture.ActivateAsync(first);
        var lease = await fixture.Authorities[0].AcquireAsync();
        lease.Retain();
        await lease.DisposeAsync();
        var before = (await fixture.Store.ReadAsync(default)).State;
        var fence = before.Leases.ShouldHaveSingleItem();
        var oldObservation = fixture.Observations[0];
        fixture.Store.Coordinates = fixture.Store.Coordinates with { HttpEndpoint = "http://127.0.0.1:3600/" };
        for (int index = 0; index < fixture.Observations.Length; index++) fixture.Observations[index] = fixture.Observations[index] with
        {
            AuthorityCoordinates = fixture.Store.Coordinates,
            TopologyDocuments = [.. fixture.Observations[index].TopologyDocuments, Encoding.UTF8.GetBytes("independently-selected-successor-sidecar")],
        };
        var hosts = fixture.Observations.Select(ObservedRouteCatalogHost.Identity).ToArray();
        var verifier = new ObservedQuiescenceFixture();
        verifier.Observe(fence, Task.CompletedTask);
        var verification = Substitute.For<IRouteCatalogQuiescenceVerifier>();
        verification.VerifyAsync(Arg.Any<RouteCatalogQuiescenceRequest>(), Arg.Any<RouteCatalogDispatchFence>(), Arg.Any<CancellationToken>())
            .Returns(call => verifier.VerifyAsync(call.Arg<RouteCatalogQuiescenceRequest>(), call.Arg<RouteCatalogDispatchFence>(), call.Arg<CancellationToken>()));
        var authorities = hosts.Select(host => fixture.CreateAuthority(host, verifier: scenario == "no-verifier" ? null : verification)).ToArray();
        var second = fixture.Candidate(2, first.RootDigest) with { RequiredHosts = hosts };
        second = second with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(second) };
        if (scenario != "unprepared") await authorities[0].PrepareAsync(second);
        if (scenario == "valid")
        {
            var draining = await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authorities[0].ReadyAsync(second.AttemptId));
            draining.Category.ShouldBe(RouteCatalogRefusalReason.Draining);
        }
        if (scenario is "old-trust" or "wrong-predecessor" or "foreign-old-host" or "missing-attempt" or "duplicate-attempt" or "changed-pending" or "nonadvancing")
        {
            var (state, etag) = await fixture.Store.ReadAsync(default);
            if (scenario == "old-trust") state = state with { Committed = state.Committed! with { TrustProof = new string('0', 64) } };
            else if (scenario == "foreign-old-host") state = state with { Leases = [fence with { HostId = "foreign-old-host" }] };
            else if (scenario == "missing-attempt") state = state with { Attempts = state.Attempts.Where(attempt => attempt.AttemptId != second.AttemptId).ToArray() };
            else if (scenario == "duplicate-attempt") state = state with { Attempts = [.. state.Attempts, state.Attempts.Single(attempt => attempt.AttemptId == second.AttemptId)] };
            else if (scenario == "changed-pending")
            {
                var changed = fixture.Candidate(3, first.RootDigest, second.AttemptId) with { RequiredHosts = hosts };
                state = state with { Pending = changed with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(changed) } };
            }
            else
            {
                var changed = scenario == "wrong-predecessor" ? state.Pending! with { PredecessorRootDigest = new string('0', 64) }
                    : fixture.Candidate(1, first.RootDigest, second.AttemptId) with { RequiredHosts = hosts };
                string binding = RouteCatalogActivationBinding.ComputeDigest(changed);
                state = state with { Pending = changed with { TrustProof = binding }, Attempts = state.Attempts.Select(attempt =>
                    attempt.AttemptId == changed.AttemptId ? attempt with { BindingDigest = binding } : attempt).ToArray() };
            }
            (await fixture.Store.CompareExchangeAsync(state, etag, default)).ShouldBeTrue();
        }
        var request = verifier.Issue(fence);
        if (scenario == "forged-proof") request = request with { Proof = new string('0', 64) };
        if (scenario == "wrong-epoch") request = request with { Epoch = request.Epoch + 1 };
        if (scenario == "wrong-root") request = request with { RootDigest = new string('0', 64) };
        if (scenario == "unknown-lease") request = request with { LeaseId = "unknown" };
        if (scenario == "blank-proof") request = request with { Proof = "" };
        var selected = scenario == "old-local-identity" ? fixture.Authorities[0] : authorities[0];
        if (scenario is "stale-observer" or "missing-observer")
        {
            var options = Options.Create(new RouteCatalogHostOptions { Identity = hosts[0], Profile = "Development", DevelopmentFixture = true });
            var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns("Development");
            selected = new(fixture.Store, new DevelopmentRouteCatalogTrustValidator(options, environment), options,
                scenario == "missing-observer" ? null : new ObservedRouteCatalogHost(_ => Task.FromResult(oldObservation)), verification);
        }
        if (scenario == "missing-coordinates") fixture.Store.Coordinates = null!;
        if (scenario != "valid")
        {
            string stateBefore = JsonSerializer.Serialize((await fixture.Store.ReadAsync(default)).State);
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => selected.ReconcileAsync(request));
            JsonSerializer.Serialize((await fixture.Store.ReadAsync(default)).State).ShouldBe(stateBefore);
            if (scenario != "forged-proof") verification.ReceivedCalls().ShouldBeEmpty();
            return;
        }
        await selected.ReconcileAsync(request);
        var reconciled = (await fixture.Store.ReadAsync(default)).State;
        reconciled.Leases.ShouldBeEmpty();
        reconciled.Committed!.RequiredHosts.ShouldBe(first.RequiredHosts);
        reconciled.Committed.CanonicalCatalog.ShouldBe(first.CanonicalCatalog);
        reconciled.Epoch.ShouldBe(before.Epoch);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => selected.CommitAsync(second.AttemptId));
        foreach (var authority in authorities.SkipLast(1)) await authority.ReadyAsync(second.AttemptId);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => selected.CommitAsync(second.AttemptId));
        await authorities[^1].ReadyAsync(second.AttemptId);
        await selected.CommitAsync(second.AttemptId);
        var committed = (await fixture.Store.ReadAsync(default)).State;
        committed.Epoch.ShouldBe(before.Epoch + 1);
        committed.Previous!.RequiredHosts.ShouldBe(first.RequiredHosts);
        committed.Committed!.RequiredHosts.ShouldBe(hosts);
    }
}
