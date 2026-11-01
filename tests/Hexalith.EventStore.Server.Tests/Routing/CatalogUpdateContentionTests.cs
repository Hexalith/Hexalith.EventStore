using System.Text.Json;

using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>CAS conflict scheduling must permit independent progress without weakening fresh authority checks or bounds.</summary>
public sealed class CatalogUpdateContentionTests
{
    [Fact]
    public async Task AwaitedAcquireLetsTheIndependentContentionOwnerFinishBeforeTheBoundIsExhausted()
    {
        var fixture = new RouteCatalogTestFixture();
        await fixture.ActivateAsync(fixture.Candidate());
        var store = new ControlledCatalogContentionStore(fixture.Store);
        var authority = Authority(fixture, store);
        Task<RouteCatalogDispatchLease> acquiring = authority.AcquireAsync(TestContext.Current.CancellationToken);
        // This independent owner can run only after Acquire has returned its incomplete task.
        // A synchronous completed-provider spin exhausts the bound before the owner gets control.
        store.AllowExchange = true;
        await using var lease = await acquiring.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        store.Attempts.ShouldBeGreaterThanOrEqualTo(2);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldHaveSingleItem();
        var next = fixture.Candidate(2, lease.Snapshot.RootDigest);
        await authority.PrepareAsync(next);
        var refused = await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.ReadyAsync(next.AttemptId));
        refused.Category.ShouldBe(RouteCatalogRefusalReason.Draining);
        await lease.DisposeAsync();
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
        foreach (var peer in fixture.Authorities) await peer.ReadyAsync(next.AttemptId);
        await authority.CommitAsync(next.AttemptId);
        (await authority.ReadbackAsync()).Generation.ShouldBe(2);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("observed-host")]
    public async Task EveryRetryRevalidatesTheCurrentStateAndIndependentObservation(string mutation)
    {
        var fixture = new RouteCatalogTestFixture();
        var first = fixture.Candidate(); await fixture.ActivateAsync(first);
        var store = new ControlledCatalogContentionStore(fixture.Store);
        var authority = Authority(fixture, store);
        store.Conflict = async (attempt, token) =>
        {
            attempt.ShouldBe(1);
            if (mutation == "pending") await fixture.Authorities[0].PrepareAsync(fixture.Candidate(2, first.RootDigest), token);
            else fixture.Observations[0] = fixture.Observations[0] with { AuthenticationProfile = "changed-independent-host"u8.ToArray() };
            store.AllowExchange = true;
        };
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => authority.AcquireAsync(TestContext.Current.CancellationToken));
        store.Attempts.ShouldBe(1);
        store.Reads.ShouldBe(2);
        var state = (await fixture.Store.ReadAsync(default)).State;
        state.Leases.ShouldBeEmpty(); state.Epoch.ShouldBe(1); state.Committed!.RootDigest.ShouldBe(first.RootDigest);
        (state.Pending is not null).ShouldBe(mutation == "pending");
    }

    [Fact]
    public async Task CancellationAfterAConflictStopsBeforeAnotherProviderReadOrExchange()
    {
        var fixture = new RouteCatalogTestFixture(); await fixture.ActivateAsync(fixture.Candidate());
        var before = JsonSerializer.Serialize((await fixture.Store.ReadAsync(default)).State);
        var store = new ControlledCatalogContentionStore(fixture.Store);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        store.Conflict = (_, _) => { cancelled.Cancel(); return Task.CompletedTask; };
        await Should.ThrowAsync<OperationCanceledException>(() => Authority(fixture, store).AcquireAsync(cancelled.Token));
        store.Reads.ShouldBe(1); store.Attempts.ShouldBe(1);
        JsonSerializer.Serialize((await fixture.Store.ReadAsync(default)).State).ShouldBe(before);
    }

    [Fact]
    public async Task PermanentContentionRetainsExactlyThirtyTwoFreshAttemptsAndNoDispatchFence()
    {
        var fixture = new RouteCatalogTestFixture(); await fixture.ActivateAsync(fixture.Candidate());
        string before = JsonSerializer.Serialize((await fixture.Store.ReadAsync(default)).State);
        var store = new ControlledCatalogContentionStore(fixture.Store);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => Authority(fixture, store).AcquireAsync(TestContext.Current.CancellationToken));
        store.Reads.ShouldBe(32); store.Attempts.ShouldBe(32);
        JsonSerializer.Serialize((await fixture.Store.ReadAsync(default)).State).ShouldBe(before);
    }

    [Fact]
    public async Task QueuedAdmissionCanCancelWhileSameAuthorityControlStillAdvancesAndInvalidatesTheHeldRead()
    {
        var fixture = new RouteCatalogTestFixture(); var first = fixture.Candidate(); await fixture.ActivateAsync(first);
        var store = new ControlledCatalogContentionStore(fixture.Store) { AllowExchange = true };
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int observations = 0;
        var options = Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts[0], Profile = "Development", DevelopmentFixture = true });
        var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns("Development");
        var authority = new RouteCatalogAuthority(store, new DevelopmentRouteCatalogTrustValidator(options, environment), options,
            new ObservedRouteCatalogHost(async token =>
            {
                if (Interlocked.Increment(ref observations) == 1) { entered.SetResult(); await resume.Task.WaitAsync(token); }
                return fixture.Observations[0];
            }));
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(10));
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(budget.Token);
        Task<RouteCatalogDispatchLease> held = authority.AcquireAsync(budget.Token);
        Task<RouteCatalogDispatchLease>? queued = null;
        try
        {
            await entered.Task.WaitAsync(budget.Token);
            queued = authority.AcquireAsync(cancelled.Token);
            cancelled.Cancel();
            await Should.ThrowAsync<OperationCanceledException>(() => queued);
            store.Reads.ShouldBe(1); store.Attempts.ShouldBe(0);
            var next = fixture.Candidate(2, first.RootDigest);
            await authority.PrepareAsync(next, budget.Token);
            (await fixture.Store.ReadAsync(budget.Token)).State.Pending!.AttemptId.ShouldBe(next.AttemptId);
            resume.SetResult();
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => held);
            (await fixture.Store.ReadAsync(budget.Token)).State.Leases.ShouldBeEmpty();
        }
        finally
        {
            resume.TrySetResult();
            foreach (var task in new[] { held, queued }.OfType<Task<RouteCatalogDispatchLease>>())
            {
                try { await (await task).DisposeAsync(); }
                catch (Exception exception) when (exception is RouteCatalogUnavailableException or OperationCanceledException) { }
            }
        }
    }

    [Fact]
    public async Task PreCancelledAcquireNeverTouchesTheProvider()
    {
        var fixture = new RouteCatalogTestFixture(); await fixture.ActivateAsync(fixture.Candidate());
        var store = new ControlledCatalogContentionStore(fixture.Store);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => Authority(fixture, store).AcquireAsync(cancelled.Token));
        store.Reads.ShouldBe(0); store.Attempts.ShouldBe(0);
        (await fixture.Store.ReadAsync(default)).State.Leases.ShouldBeEmpty();
    }

    private static RouteCatalogAuthority Authority(RouteCatalogTestFixture fixture, IRouteCatalogStateStore store)
    {
        var options = Options.Create(new RouteCatalogHostOptions { Identity = fixture.Hosts[0], Profile = "Development", DevelopmentFixture = true });
        var environment = Substitute.For<IHostEnvironment>(); environment.EnvironmentName.Returns("Development");
        return new(store, new DevelopmentRouteCatalogTrustValidator(options, environment), options,
            new ObservedRouteCatalogHost(_ => Task.FromResult(fixture.Observations[0])));
    }
}
