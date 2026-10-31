using System.Net;
using System.Net.Http.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Admin.Abstractions.Models.Common;
using Hexalith.EventStore.Admin.Abstractions.Models.Storage;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.ServiceDefaults.Routing;
using Hexalith.EventStore.Services;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;
using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Routing;

/// <summary>Actual SDK native RPC sinks retain receiver fences after ambiguous persisted effects, including caught failures.</summary>
public sealed class TechnicalHttpNativeRpcTests
{
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    public async Task DirectHumanAndSidecarTechnicalEffectsUseTheSameCommittedReceiverLease(bool active, bool channel, bool loseResponse)
    {
        await using var fixture = new TechnicalHttpCatalogFixture();
        await fixture.StartAsync(TestContext.Current.CancellationToken, activate: active);
        fixture.Human(channel: channel);
        fixture.State.BeforeTrySaveByteState = (_, _) =>
        {
            Hexalith.EventStore.Contracts.Routing.RouteCatalogRemoteExecution.Current.ShouldNotBeNull();
            fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken).GetAwaiter().GetResult().State.Leases.ShouldHaveSingleItem();
        };
        if (loseResponse) fixture.State.AfterTrySaveByteState = (_, _) => throw new TimeoutException("Persisted technical response lost.");
        using var response = await fixture.Client.PutAsJsonAsync("/api/v1/admin/storage/snapshot-policy",
            new SnapshotPolicySetRequest("tenant-a", "orders", "order", 25), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(active ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable);
        fixture.State.ByteStoreContains("admin:storage-snapshot-policies:all").ShouldBe(active);
        var state = (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State;
        state.Leases.Count.ShouldBe(loseResponse ? 1 : 0);
        if (!active) fixture.State.TrySaveByteOperations.ShouldBeEmpty();
        if (loseResponse)
        {
            (await response.Content.ReadFromJsonAsync<AdminOperationResult>(TestContext.Current.CancellationToken))!.ErrorCode.ShouldBe("UpstreamUnavailable");
            var next = fixture.Catalog.Candidate(2, state.Committed!.RootDigest);
            await fixture.Catalog.Authorities[0].PrepareAsync(next);
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Catalog.Authorities[0].ReadyAsync(next.AttemptId));
            System.Text.Json.JsonSerializer.Serialize((await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Committed)
                .ShouldBe(System.Text.Json.JsonSerializer.Serialize(state.Committed));
        }
    }

    [Theory]
    [InlineData("success", 200, false)]
    [InlineData("persist-then-timeout", 200, true)]
    [InlineData("persist-then-cancel", 500, true)]
    [InlineData("pre-send-validation", 200, false)]
    public async Task RealSnapshotPolicyHttpActionKeepsActualRpcFence(string scenario, int status, bool retained)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        await using var fixture = new TechnicalHttpCatalogFixture();
        await fixture.StartAsync(TestContext.Current.CancellationToken);
        if (scenario is "persist-then-timeout" or "persist-then-cancel")
            fixture.State.AfterTrySaveByteState = (_, _) =>
            {
                if (scenario == "persist-then-timeout") throw new TimeoutException("RPC persisted before response was lost.");
                throw new OperationCanceledException("RPC persisted before response was cancelled.");
            };
        using var response = await fixture.Client.PutAsJsonAsync("/api/v1/admin/storage/snapshot-policy",
            new SnapshotPolicySetRequest("tenant-a", "orders", "order", scenario == "pre-send-validation" ? 0 : 25), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe((HttpStatusCode)status);
        if (scenario != "persist-then-cancel")
        {
            var result = (await response.Content.ReadFromJsonAsync<AdminOperationResult>(TestContext.Current.CancellationToken))!;
            result.Success.ShouldBe(scenario == "success");
            if (scenario == "persist-then-timeout") result.ErrorCode.ShouldBe("UpstreamUnavailable");
        }
        fixture.State.ByteStoreContains("admin:storage-snapshot-policies:all").ShouldBe(scenario != "pre-send-validation");
        var state = (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State;
        state.Leases.Count.ShouldBe(retained ? 1 : 0);
        if (retained)
        {
            var next = fixture.Catalog.Candidate(2, state.Committed!.RootDigest);
            await fixture.Catalog.Authorities[0].PrepareAsync(next);
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Catalog.Authorities[0].ReadyAsync(next.AttemptId));
            await Should.ThrowAsync<RouteCatalogUnavailableException>(() => fixture.Catalog.Authorities[0].CommitAsync(next.AttemptId));
            (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Committed!.RootDigest.ShouldBe(state.Committed.RootDigest);
        }
    }

    [Fact]
    public async Task AlreadyCancelledNativePolicyCallDoesNotEnrollOrReachDapr()
    {
        await using var fixture = new TechnicalHttpCatalogFixture();
        await fixture.StartAsync(TestContext.Current.CancellationToken);
        int reads = 0;
        fixture.State.BeforeReadByteState = _ => reads++;
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await using (var lease = await fixture.Catalog.Authorities[0].AcquireAsync(TestContext.Current.CancellationToken))
        {
            using var ambient = RouteCatalogRemoteCall.Enter(lease);
            await Should.ThrowAsync<OperationCanceledException>(() => fixture.App.Services.GetRequiredService<DaprSnapshotPolicyRepository>()
                .SetPolicyAsync(new("tenant-a", "orders", "order", 25), cancelled.Token));
        }
        reads.ShouldBe(0); fixture.State.TrySaveByteOperations.ShouldBeEmpty();
        (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualManualSnapshotActorRpcRetainsPersistedThenLostResponse(bool cancel)
    {
        await using var fixture = new TechnicalHttpCatalogFixture();
        await fixture.StartAsync(TestContext.Current.CancellationToken);
        var actor = Substitute.For<IAggregateActor>();
        int persisted = 0;
        actor.CreateManualSnapshotAsync(Arg.Any<string?>()).Returns(_ =>
        {
            persisted++;
            return cancel ? Task.FromException<ManualSnapshotResult>(new OperationCanceledException("Persisted actor snapshot response cancelled."))
                : Task.FromException<ManualSnapshotResult>(new TimeoutException("Persisted actor snapshot response lost."));
        });
        fixture.App.Services.GetRequiredService<IActorProxyFactory>().CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), "AggregateActor").Returns(actor);
        using var response = await fixture.Client.PostAsJsonAsync("/api/v1/admin/storage/snapshot", new ManualSnapshotRequest("tenant-a", "orders", "order-1"), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        persisted.ShouldBe(1);
        (await fixture.Catalog.Store.ReadAsync(TestContext.Current.CancellationToken)).State.Leases.ShouldHaveSingleItem();
    }
}
