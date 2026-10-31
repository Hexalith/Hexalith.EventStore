using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Consistency;
using Hexalith.EventStore.Admin.Abstractions.Models.Streams;
using Hexalith.EventStore.Admin.Abstractions.Services;
using Hexalith.EventStore.Admin.Server.Models;
using Hexalith.EventStore.Contracts.Routing;
using Hexalith.EventStore.ServiceDefaults.Routing;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Exercises the existing real 202 consistency workflow beyond request disposal under its own admitted lifetime.</summary>
public sealed class TechnicalHttpBackgroundTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DetachedConsistencyScanOwnsOnlyItsChildLeaseAndRetainsCaughtAmbiguity(bool loseCompletion)
    {
        await using var fixture = new TechnicalHttpCatalogFixture();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var streams = Substitute.For<IStreamQueryService>();
        streams.GetRecentlyActiveStreamsAsync(Arg.Any<string?>(), Arg.Any<string?>(), 1, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            entered.TrySetResult(); await resume.Task.WaitAsync(TestContext.Current.CancellationToken);
            var enclosing = RouteCatalogRemoteExecution.Current;
            await using (var nested = await fixture.Catalog.Authorities[0].AcquireAsync(TestContext.Current.CancellationToken))
            {
                using var scope = RouteCatalogRemoteCall.Enter(nested);
                await RouteCatalogRemoteExecution.InvokeAsync(() => ((Dapr.Client.DaprClient)fixture.State).GetStateAsync<object>("statestore", "nested-scope-proof", cancellationToken: TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
            }
            RouteCatalogRemoteExecution.Current.ShouldBeSameAs(enclosing);
            await using (var fork = await RouteCatalogRemoteExecution.AcquireBackgroundAsync(TestContext.Current.CancellationToken))
                await fork!.RunAsync(() => RouteCatalogRemoteExecution.InvokeAsync(() => ((Dapr.Client.DaprClient)fixture.State).GetStateAsync<object>("statestore", "subsequent-fork-proof", cancellationToken: TestContext.Current.CancellationToken), TestContext.Current.CancellationToken));
            RouteCatalogRemoteExecution.Current.ShouldBeSameAs(enclosing);
            return new PagedResult<StreamSummary>([], 0, null);
        });
        streams.GetRecentlyActiveStreamsAsync(Arg.Any<string?>(), Arg.Any<string?>(), 10000, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<StreamSummary>([], 0, null));
        await fixture.StartAsync(TestContext.Current.CancellationToken, services: services => services.AddSingleton(streams), fullInventory: true,
            inventoryAssembly: "Hexalith.EventStore.Admin.Server");
        fixture.State.AfterSaveTypedState = (_, value) =>
        {
            if (value is not ConsistencyCheckResult result) return;
            if (result.Status == ConsistencyCheckStatus.Completed && loseCompletion) throw new TimeoutException("Completed scan state persisted before native response was lost.");
            if (result.Status is ConsistencyCheckStatus.Completed or ConsistencyCheckStatus.Failed) terminal.TrySetResult();
        };
        try
        {
            using var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/consistency/checks",
                new ConsistencyCheckRequest("tenant-a", "orders", [ConsistencyCheckType.SequenceContinuity]), TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            var operation = (await response.Content.ReadFromJsonAsync<AdminOperationResult>(TestContext.Current.CancellationToken))!;
            operation.Success.ShouldBeTrue();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            // The real request has returned and released its lease; the blocked scan owns another.
            await WaitForLeaseCountAsync(fixture, 1);
            var active = (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State;
            active.Leases.ShouldHaveSingleItem().HostId.ShouldBe("eventstore");
            resume.TrySetResult();
            await terminal.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await WaitForLeaseCountAsync(fixture, loseCompletion ? 1 : 0);
            if (loseCompletion) (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldHaveSingleItem().LeaseId
                .ShouldBe(active.Leases.Single().LeaseId);
            var saved = JsonSerializer.Deserialize<ConsistencyCheckResult>(fixture.State.ByteStoreValue("admin:consistency:" + operation.OperationId), JsonSerializerOptions.Web)!;
            saved.Status.ShouldBe(loseCompletion ? ConsistencyCheckStatus.Failed : ConsistencyCheckStatus.Completed);
            // A nested normal scope must not recover the now-closed request lease.
            (saved.ErrorMessage ?? string.Empty).ShouldNotContain("disposed catalog dispatch scope");
            if (loseCompletion) saved.ErrorMessage.ShouldContain("native response was lost");
        }
        finally { resume.TrySetResult(); }
    }

    [Fact]
    public async Task ActiveBoundaryWithoutDetachedAdmissionRefusesBeforeScheduling()
    {
        var prior = RouteCatalogRemoteExecution.Current;
        RouteCatalogRemoteExecution.Current = Substitute.For<IRouteCatalogRemoteBoundary>();
        try { await Should.ThrowAsync<InvalidOperationException>(() => RouteCatalogRemoteExecution.AcquireBackgroundAsync(TestContext.Current.CancellationToken)); }
        finally { RouteCatalogRemoteExecution.Current = prior; }
    }

    [Fact]
    public async Task InheritedClosedRequestCannotMintWorkAndDisposedWorkCannotExecute()
    {
        await using var fixture = new TechnicalHttpCatalogFixture();
        await fixture.StartAsync(TestContext.Current.CancellationToken);
        var parent = await fixture.Catalog.Authorities[0].AcquireAsync(TestContext.Current.CancellationToken);
        var boundary = new RouteCatalogBackgroundBoundary(Substitute.For<IRouteCatalogRemoteBoundary>(), fixture.Catalog.Authorities[0], parent);
        var work = await boundary.AcquireAsync(TestContext.Current.CancellationToken);
        await work.DisposeAsync();
        int effects = 0;
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => work.RunAsync(() => { effects++; return Task.CompletedTask; }));
        effects.ShouldBe(0);
        var previous = RouteCatalogRemoteExecution.Current;
        RouteCatalogRemoteExecution.Current = boundary;
        try
        {
            await parent.DisposeAsync();
            var before = await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken);
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => RouteCatalogRemoteExecution.AcquireBackgroundAsync(TestContext.Current.CancellationToken));
            var after = await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken);
            after.ETag.ShouldBe(before.ETag);
            JsonSerializer.Serialize(after.State).ShouldBe(JsonSerializer.Serialize(before.State));
        }
        finally { RouteCatalogRemoteExecution.Current = previous; await parent.DisposeAsync(); }
    }

    [Theory]
    [InlineData("binding")]
    [InlineData("local")]
    [InlineData("coordinates")]
    [InlineData("epoch")]
    [InlineData("parent-closes-during-acquire")]
    public async Task ChildAdmissionComparesTheEntireRunAndReleasesAnyUnusableChild(string drift)
    {
        ArgumentNullException.ThrowIfNull(drift);
        await using var fixture = new TechnicalHttpCatalogFixture();
        await fixture.StartAsync(TestContext.Current.CancellationToken);
        await using var parent = await fixture.Catalog.Authorities[0].AcquireAsync(TestContext.Current.CancellationToken);
        var active = (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Committed!;
        var activation = drift == "binding" ? active with { AttemptId = "different-run-same-root" } : active;
        var child = new CommittedRouteCatalogSnapshot(activation, parent.Snapshot.Epoch + (drift == "epoch" ? 1 : 0),
            drift == "local" ? "different-local-peer" : parent.Snapshot.LocalAppId,
            drift == "coordinates" ? fixture.Catalog.Store.Coordinates with { HttpEndpoint = "http://127.0.0.1:3501/" } : fixture.Catalog.Store.Coordinates);
        int released = 0;
        var owned = new RouteCatalogDispatchLease(child, () => { released++; return Task.CompletedTask; });
        var authority = Substitute.For<IRouteCatalogAuthority>();
        authority.AcquireAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            if (drift == "parent-closes-during-acquire") await parent.DisposeAsync();
            return owned;
        });
        var boundary = new RouteCatalogBackgroundBoundary(Substitute.For<IRouteCatalogRemoteBoundary>(), authority, parent);
        await Should.ThrowAsync<RouteCatalogUnavailableException>(() => boundary.AcquireAsync(TestContext.Current.CancellationToken));
        released.ShouldBe(1);
    }

    private static async Task WaitForLeaseCountAsync(TechnicalHttpCatalogFixture fixture, int count)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        bound.CancelAfter(TimeSpan.FromSeconds(5));
        while ((await fixture.Catalog.Store.ReadAsync(bound.Token)).State.Leases.Count != count) await Task.Delay(10, bound.Token);
    }
}
