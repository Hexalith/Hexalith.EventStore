using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.EventStore.Client.Events;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Testing.Fakes;
using Shouldly;

namespace Hexalith.EventStore.DomainService.Tests;

public sealed partial class DomainPartitionedSharedProjectionRebuildTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task OmittedOriginalOwnerReplayRemainsExactAcrossNamedRebuilds(bool bufferedOwner, bool evolve)
    {
        var store = new InMemoryReadModelStore();
        PartitionedHandler handler = bufferedOwner ? new OutputOnlyPartitionedHandler() : new StreamedPartitionedHandler();
        handler.FoldOrdinaryRows = true;
        handler.CopyOnWrite = true;
        handler.FailBufferedOwnerFold = !bufferedOwner;
        var scope = handler.CreateScope("tenant-a");
        var coordinator = new SharedProjectionEpochCoordinator(store, store);
        using var provider = Provider(store, store, coordinator, handler,
            evolve ? new EventPayloadEvolutionRegistry([], []) : null);
        var original = Event([2]) with { GlobalPosition = 2 };
        var other = Event([3]) with { GlobalPosition = 3 };
        (await DeliverAsync(provider, "primer", Event([1]))).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        handler.OrdinaryFoldCount.ShouldBe(1);
        await RebuildAsync("original", ("owner-a", original), ("owner-b", other));
        var checkpoint = JsonSerializer.Serialize(await coordinator.GetCheckpointAsync(scope, "owner-a"));
        var row = JsonSerializer.Serialize((await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value);
        (await DeliverAsync(provider, "owner-a", original)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);

        foreach (string operation in new[] { "independent", "independent-again" })
        {
            await RebuildAsync(operation, ("owner-b", other));
            var epoch = (await store.GetAsync<JsonObject>(StoreName, "shared-epoch:" + scope.ComputeHash())).Value!;
            epoch["captureHighWatermarks"]!.AsObject().ContainsKey("owner-a").ShouldBeFalse();
            using var replacement = Provider(store, store, new SharedProjectionEpochCoordinator(store, store), handler,
                evolve ? new EventPayloadEvolutionRegistry([], []) : null);
            (await DeliverAsync(replacement, "owner-a", original)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
            JsonSerializer.Serialize(await coordinator.GetCheckpointAsync(scope, "owner-a")).ShouldBe(checkpoint);
            JsonSerializer.Serialize((await coordinator.ReadAsync<PayloadRow>(scope, "row:owner-a")).Value).ShouldBe(row);
            handler.OrdinaryFoldCount.ShouldBe(1);
        }

        var growth = original with { SequenceNumber = 2, GlobalPosition = 4, MessageId = "growth", Payload = [4] };
        (await DeliverAsync(provider, "owner-a", growth)).Status.ShouldBe(ProjectionDispatchStatus.Completed);
        handler.OrdinaryFoldCount.ShouldBe(2);
        (await DeliverAsync(provider, "owner-a", original)).Status.ShouldBe(ProjectionDispatchStatus.AlreadyCompleted);
        var failed = await DeliverAsync(provider, "owner-a", original with { MessageId = "altered" });
        failed.Status.ShouldBe(ProjectionDispatchStatus.Failed);
        failed.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryIdentityConflict);
        handler.OrdinaryFoldCount.ShouldBe(2);
        (await coordinator.GetCheckpointAsync(scope, "owner-a"))!.OwnerSequenceNumber.ShouldBe(2);

        async Task<ProjectionDispatchOutcome> DeliverAsync(IServiceProvider services, string owner, ProjectionEventDto item)
        {
            var request = new ProjectionDispatchRequest(new("tenant-a", "widget", owner, [item]),
                [handler.ProjectionType], "inherited-" + owner, Identity("unused").CatalogFingerprint);
            request = JsonSerializer.Deserialize<ProjectionDispatchRequest>(JsonSerializer.SerializeToUtf8Bytes(request,
                JsonSerializerOptions.Web), JsonSerializerOptions.Web)!;
            return (await DomainProjectionDispatcher.DispatchAsync(services, request,
                new ProjectionDispatchOptions(), new DomainProjectionIdentityOptions { AppId = "widget-service", ServiceVersion = "v1" },
                TestContext.Current.CancellationToken)).Outcomes.Single();
        }

        async Task RebuildAsync(string operation, params (string Owner, ProjectionEventDto Event)[] owners)
        {
            var identity = Identity(operation);
            string inventory = DomainSharedProjectionRebuildFingerprint.EmptyInventory;
            var histories = owners.Select(owner => DomainSharedProjectionRebuildFingerprint.ComputeHistory(owner.Owner, false, [owner.Event])).ToArray();
            for (int ordinal = 0; ordinal < owners.Length; ordinal++)
                inventory = DomainSharedProjectionRebuildFingerprint.AppendInventory(inventory, ordinal, histories[ordinal]);
            (await BoundedRebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.Begin,
                identity, CaptureInventoryFingerprint: inventory, SourceHighWatermarks: owners.ToDictionary(owner => owner.Owner, owner => owner.Event.GlobalPosition))))
                .Status.ShouldBe(ProjectionDispatchStatus.Completed);
            DomainSharedProjectionRebuildResponse? accepted = null;
            for (int ordinal = 0; ordinal < owners.Length; ordinal++)
            {
                (await BoundedRebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulatePart,
                    identity, AggregateOrdinal: ordinal, AggregateId: owners[ordinal].Owner, Events: [owners[ordinal].Event], PartOrdinal: 0)))
                    .Status.ShouldBe(ProjectionDispatchStatus.Completed);
                accepted = await BoundedRebuildAsync(provider, new(DomainSharedProjectionRebuildProtocol.Version, DomainSharedProjectionRebuildAction.AccumulateComplete,
                    identity, AggregateOrdinal: ordinal, AggregateId: owners[ordinal].Owner, PartCount: 1, ExpectedHistoryFingerprint: histories[ordinal]));
                accepted.Status.ShouldBe(ProjectionDispatchStatus.Completed);
            }
            await BoundedFinishAsync(provider, identity, accepted!);
        }
    }
}
