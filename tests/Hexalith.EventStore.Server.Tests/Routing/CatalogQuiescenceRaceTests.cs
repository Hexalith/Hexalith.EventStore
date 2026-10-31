using System.Text;
using System.Text.Json;

using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

public sealed class CatalogQuiescenceRaceTests
{
    [Theory]
    [InlineData("unrelated-completion")]
    [InlineData("pending-aborted")]
    [InlineData("pending-replaced")]
    [InlineData("old-trust-revoked")]
    [InlineData("epoch-changed")]
    [InlineData("target-replaced")]
    [InlineData("verifier-failure")]
    [InlineData("verifier-cancelled")]
    public async Task EveryCasRetryRevalidatesCurrentAuthorityAndNeverDiscardsAnotherFence(string scenario)
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate(); await fixture.ActivateAsync(first);
        var target = await fixture.Authorities[0].AcquireAsync(); target.Retain(); await target.DisposeAsync();
        var unrelated = await fixture.Authorities[0].AcquireAsync();
        var retained = await fixture.Authorities[0].AcquireAsync(); retained.Retain(); await retained.DisposeAsync();
        var initial = (await fixture.Store.ReadAsync(default)).State;
        var fence = initial.Leases[0];
        fixture.Store.Coordinates = fixture.Store.Coordinates with { HttpEndpoint = "http://127.0.0.1:3600/" };
        for (int i = 0; i < fixture.Observations.Length; i++) fixture.Observations[i] = fixture.Observations[i] with
        {
            AuthorityCoordinates = fixture.Store.Coordinates,
            TopologyDocuments = [.. fixture.Observations[i].TopologyDocuments, Encoding.UTF8.GetBytes("actual-successor-coordinate")],
        };
        var hosts = fixture.Observations.Select(ObservedRouteCatalogHost.Identity).ToArray();
        var second = fixture.Candidate(2, first.RootDigest) with { RequiredHosts = hosts };
        second = second with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(second) };
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int verifications = 0;
        var verifier = Substitute.For<IRouteCatalogQuiescenceVerifier>();
        verifier.VerifyAsync(Arg.Any<RouteCatalogQuiescenceRequest>(), Arg.Any<RouteCatalogDispatchFence>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            if (Interlocked.Increment(ref verifications) == 1) { entered.TrySetResult(); await release.Task.WaitAsync(call.Arg<CancellationToken>()); }
            if (scenario == "verifier-failure") throw new RouteCatalogUnavailableException("Owner could not prove remote completion.");
            if (scenario == "verifier-cancelled") throw new OperationCanceledException();
        });
        var authority = fixture.CreateAuthority(hosts[0], verifier: verifier);
        await authority.PrepareAsync(second);
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Task reconciling = authority.ReconcileAsync(new(fence.LeaseId, fence.RootDigest, fence.Epoch, "explicit-test-owner-proof"), budget.Token);
        await entered.Task.WaitAsync(budget.Token);
        if (scenario == "unrelated-completion") await unrelated.DisposeAsync();
        else if (scenario == "pending-aborted") await authority.RollbackAsync(new(second.AttemptId, RouteCatalogActivationBinding.ComputeDigest(second), initial.Epoch, "pending"));
        else if (scenario is "pending-replaced" or "old-trust-revoked" or "epoch-changed" or "target-replaced")
        {
            var (state, etag) = await fixture.Store.ReadAsync(default);
            if (scenario == "pending-replaced")
            {
                var changed = fixture.Candidate(3, first.RootDigest, second.AttemptId) with { RequiredHosts = hosts };
                state = state with { Pending = changed with { TrustProof = RouteCatalogActivationBinding.ComputeDigest(changed) } };
            }
            if (scenario == "old-trust-revoked") state = state with { Committed = state.Committed! with { TrustProof = new string('0', 64) } };
            if (scenario == "epoch-changed") state = state with { Epoch = state.Epoch + 1 };
            if (scenario == "target-replaced") state = state with { Leases = state.Leases.Select(lease => lease.LeaseId == fence.LeaseId ? lease with { LeaseId = "another-dispatch" } : lease).ToArray() };
            (await fixture.Store.CompareExchangeAsync(state, etag, default)).ShouldBeTrue();
        }
        string afterInterleave = JsonSerializer.Serialize((await fixture.Store.ReadAsync(default)).State);
        release.TrySetResult();
        if (scenario == "unrelated-completion")
        {
            await reconciling;
            verifications.ShouldBe(2);
            var state = (await fixture.Store.ReadAsync(default)).State;
            state.Leases.ShouldBe([initial.Leases[2]]);
            state.Epoch.ShouldBe(initial.Epoch);
            state.Committed!.RootDigest.ShouldBe(first.RootDigest);
            state.Pending!.RootDigest.ShouldBe(second.RootDigest);
            var refusal = await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.ReadyAsync(second.AttemptId));
            refusal.Category.ShouldBe(RouteCatalogRefusalReason.Draining);
        }
        else
        {
            if (scenario == "verifier-cancelled") await Should.ThrowAsync<OperationCanceledException>(() => reconciling);
            else await Should.ThrowAsync<RouteCatalogUnavailableException>(() => reconciling);
            verifications.ShouldBe(1);
            JsonSerializer.Serialize((await fixture.Store.ReadAsync(default)).State).ShouldBe(afterInterleave);
        }
    }
}
