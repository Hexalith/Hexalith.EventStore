using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public partial class ProjectionDeliveryReconcilerTests
{
    [Fact]
    public async Task RetainedAdministratorCheckpoint_PersistsOnlyHydratedTechnicalEvidenceAndAttributableWork()
    {
        var identity = new AggregateIdentity("system", "global-administrators", "global-administrators");
        const string projection = "global-administrators";
        using var backend = new RecordingDaprClient { EnforceTransactionConcurrency = true };
        var source = new EventEnvelope("01J00000000000000000000000", identity.AggregateId, "GlobalAdministrators",
            identity.TenantId, identity.Domain, 1, 7, Now, "original-correlation", "original-command", "original-actor",
            "v1", "Hexalith.Tenants.Contracts.Events.GlobalAdministratorSet", 1, "json", "{\"userId\":\"original-actor\"}"u8.ToArray(), null);
        string sourceKey = identity.EventStreamKeyPrefix + "1";
        byte[] sourceBytes = JsonSerializer.SerializeToUtf8Bytes(source, backend.JsonSerializerOptions);
        byte[] metadataBytes = "{\"currentSequence\":1}"u8.ToArray();
        backend.SeedByteStore(sourceKey, sourceBytes);
        backend.SeedByteStore(identity.MetadataKey, metadataBytes);
        ProjectionDeliveryState legacy = LegacyState(1) with {
            TenantId = identity.TenantId, Domain = identity.Domain, AggregateId = identity.AggregateId,
        };
        string stateKey = ProjectionDeliveryStateKeys.GetStateKey(identity, projection);
        backend.SeedByteStore(stateKey, JsonSerializer.SerializeToUtf8Bytes(legacy, backend.JsonSerializerOptions));
        string foreignKey = ProjectionDeliveryStateKeys.GetStateKey(Identity, "order-detail");
        byte[] foreignBytes = JsonSerializer.SerializeToUtf8Bytes(LegacyState(2), backend.JsonSerializerOptions);
        backend.SeedByteStore(foreignKey, foreignBytes);

        IAggregateActor actor = Substitute.For<IAggregateActor>();
        actor.ReadEventsRangeAsync(0, 1, 1).Returns(_ =>
            [JsonSerializer.Deserialize<EventEnvelope>(backend.ByteStoreValue(sourceKey), backend.JsonSerializerOptions)!]);
        IActorProxyFactory factory = Substitute.For<IActorProxyFactory>();
        factory.CreateActorProxy<IAggregateActor>(Arg.Is<ActorId>(id => id.ToString() == identity.ActorId), nameof(AggregateActor))
            .Returns(actor);
        var history = new EventStoreProjectionDeliveryHistoryReader(factory, Substitute.For<IEventPayloadProtectionService>(),
            Options.Create(new EventStoreActorOptions()));
        var store = new DaprProjectionDeliveryStateStore(backend, Options.Create(new ProjectionOptions()));
        ProjectionDeliveryReconciliationResult result = await CreateReconciler(store, history)
            .ReconcileFromEventStoreAsync(identity, projection, "maintenance-operator");

        result.Status.ShouldBe(ProjectionDeliveryReconciliationStatus.Completed);
        result.PreservedSequence.ShouldBe(1);
        ProjectionDeliveryState persisted = (await store.ReadAsync(identity, projection)).State.ShouldNotBeNull();
        persisted.LastDeliveredSequence.ShouldBe(1);
        persisted.LastCompletedMessageId.ShouldBe(source.MessageId);
        persisted.CompletedReceipts.ShouldNotBeNull().Single().MessageId.ShouldBe(source.MessageId);
        persisted.MigrationProvenance.ShouldBe(ProjectionDeliveryMigrationProvenance.HydratedFromPersistedCheckpoint);
        ProjectionDeliveryReconciliationWork work = (await store.ReadReconciliationAsync(identity, projection)).ShouldNotBeNull();
        work.OperatorId.ShouldBe("maintenance-operator");
        work.TenantId.ShouldBe("system");
        work.Domain.ShouldBe(projection);
        work.AggregateId.ShouldBe(projection);
        work.ProjectionName.ShouldBe(projection);
        work.ObservedSequence.ShouldBe(1);
        work.ReasonCode.ShouldBe(ProjectionDispatchReasonCodes.DeliveryReconciled);
        backend.ByteStoreValue(sourceKey).ShouldBe(sourceBytes);
        backend.ByteStoreValue(identity.MetadataKey).ShouldBe(metadataBytes);
        backend.ByteStoreValue(foreignKey).ShouldBe(foreignBytes);
        backend.TransactionOperations.Select(operation => operation.Key).Order()
            .ShouldBe(new[] { stateKey, ProjectionDeliveryStateKeys.GetReconciliationKey(identity, projection) }.Order());
        backend.ExecuteStateTransactionCallCount.ShouldBe(1);
        backend.SaveByteStateCallCount.ShouldBe(0);
        actor.ReceivedCalls().Select(call => call.GetMethodInfo().Name).ShouldBe([nameof(IAggregateActor.ReadEventsRangeAsync)]);
    }
}
