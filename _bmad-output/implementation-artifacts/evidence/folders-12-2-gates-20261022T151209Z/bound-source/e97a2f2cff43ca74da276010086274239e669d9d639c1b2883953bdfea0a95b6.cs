using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Projections;

/// <summary>Persisted row and owning lifecycle/source seams for the production finalization state machine.</summary>
internal sealed class GuardedFinalizationFixture
{
    internal static readonly AggregateIdentity Identity = new("tenant-a", "sales", "order-42");
    internal static readonly DateTimeOffset Now = new(2026, 7, 14, 10, 0, 0, TimeSpan.Zero);
    internal const string Projection = "order-detail";
    internal ProjectionDeliveryState State { get; private set; }
    internal ProjectionDeliveryFingerprintHistory History { get; }
    internal ProjectionDeliveryIdempotencyCoordinator Coordinator { get; }
    internal ProjectionSourceGuardFinalizer Finalizer { get; }
    internal IProjectionDeliveryStateStore Store { get; } = Substitute.For<IProjectionDeliveryStateStore>();
    internal IProjectionLifecycleGateway Lifecycle { get; } = Substitute.For<IProjectionLifecycleGateway>();
    internal IProjectionSelectedOwnerProofGateway Owners { get; } = Substitute.For<IProjectionSelectedOwnerProofGateway>();
    internal IAggregateActor Source { get; } = Substitute.For<IAggregateActor>();
    internal NamedProjectionRouteCatalogEntry Catalog { get; }
    internal ProjectionOwnerProof Selected { get; }
    internal SourceProof Seed { get; }
    internal SourceProof Head { get; }
    internal FakeTimeProvider Clock { get; } = new(Now);
    internal int Writes { get; private set; }
    internal long IdleRevision { get; set; } = 41;
    internal bool Released { get; private set; }
    internal bool LoseCompletionResponse { get; set; }
    internal bool ChangeLifecycleAfterCompletion { get; set; }

    internal GuardedFinalizationFixture()
    {
        Seed = SourceProofCodec.CreateSeed(Identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        var stored = new EventEnvelope("01ARZ3NDEKTSV4RRFFQ69G5FAW", Identity.AggregateId, "Order", Identity.TenantId, Identity.Domain,
            1, 1, Now, "correlation-1", "cause-1", "user-1", "v1", "OrderChanged", 1, "json", [1], null);
        Head = SourceProofCodec.Extend(Seed, stored);
        var wire = new ProjectionEventDto(stored.EventTypeName, stored.Payload, stored.SerializationFormat,
            1, stored.Timestamp, stored.CorrelationId, stored.MessageId, stored.UserId, stored.GlobalPosition);
        History = ProjectionDeliveryFingerprint.ComputeHistory(Identity, Projection, [wire]);
        string fingerprint = new('a', 64);
        Catalog = new("sample", "v1", Identity.Domain, 2, ProjectionDispatchProtocol.Capability, fingerprint, [Projection]);
        var marker = new ProjectionSourceGuard(1, 1, 2, 2, 1, Identity.TenantId, Identity.Domain, Identity.AggregateId,
            Projection, Seed.SourceIncarnation, fingerprint, "single-event-v1", ProjectionSourceGuard.ApprovedSpecSha256,
            new string('b', 40), Now);
        Selected = new(new(1, Identity.TenantId, Identity.Domain, Identity.AggregateId, Projection,
            "owner-1", "generation-1", 1, stored.MessageId, stored.MessageId, "version-1"), "receipt-1", "v1:" + new string('A', 43));
        State = ProjectionDeliveryState.CreateEmpty(Identity, Projection, History.InitialFingerprint, Now) with
        {
            SchemaVersion = 2, GuardEvidenceVersion = 1,
        };
        Store.ReadAsync(Identity, Projection, Arg.Any<CancellationToken>()).Returns(_ =>
            new ProjectionDeliveryStateReadResult(State, Writes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ProjectionDeliveryStateClassification.Current, true) { SourceGuard = marker });
        Store.TrySaveAsync(Identity, Projection, Arg.Any<ProjectionDeliveryState>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string etag = call.ArgAt<string>(3);
                if (etag != Writes.ToString(System.Globalization.CultureInfo.InvariantCulture)) return false;
                State = call.ArgAt<ProjectionDeliveryState>(2);
                Writes++;
                if (State.LastDeliveredSequence > 0 && LoseCompletionResponse) throw new InvalidOperationException("lost response");
                return true;
            });
        Lifecycle.BeginDeliveryWriteAsync(Identity, Projection, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        Lifecycle.CompleteDeliveryWriteAsync(Identity, Projection, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            Released = true;
            return true;
        });
        Lifecycle.ReadSnapshotAsync(Identity, Projection, Arg.Any<CancellationToken>()).Returns(_ =>
            new ProjectionLifecycleSnapshot(Released ? ProjectionLifecyclePhase.Idle : ProjectionLifecyclePhase.Delivering,
                IdleRevision + (ChangeLifecycleAfterCompletion && State.LastDeliveredSequence > 0 ? 1 : 0)));
        Owners.ReadAsync(Identity, Catalog, Projection, Arg.Any<ProjectionLifecycleSnapshot>(), Arg.Any<CancellationToken>())
            .Returns(new ProjectionOwnerProofResult(Selected, null, 4, 0));
        Source.InspectSourceProofAsync().Returns(new SourceProofInspection(SourceProofInspectionStatus.Available, true, 1, false, Head));
        Source.ReadMeasuredEventsBoundedRangeAsync(0, 1, 1, 2_097_152).Returns(new SourceEventRange([stored],
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new[] { stored }, System.Text.Json.JsonSerializerOptions.Web).Length));
        IActorProxyFactory actors = Substitute.For<IActorProxyFactory>();
        actors.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(Source);
        var actorOptions = Options.Create(new EventStoreActorOptions());
        Coordinator = new(Store, Options.Create(new ProjectionDeliveryIdempotencyOptions()), Clock);
        Finalizer = new(Coordinator, Store, Lifecycle, Owners,
            new ProjectionSourceGuardHistoryReader(actors, Substitute.For<IEventPayloadProtectionService>(), actorOptions), actors, actorOptions, Clock);
    }

    internal async Task<ProjectionDeliveryReservation> ReserveAsync()
    {
        var binding = new ProjectionDeliveryGuardReservation(Seed, Head, Catalog.CatalogFingerprint, "single-event-v1", "generation-1", null);
        ProjectionDeliveryAdmissionResult admitted = await Coordinator.TryAdmitHistoryAsync(Identity, Projection, History,
            true, guardBinding: binding).ConfigureAwait(false);
        return admitted.Reservation ?? throw new InvalidOperationException("fixture admission failed");
    }
}
