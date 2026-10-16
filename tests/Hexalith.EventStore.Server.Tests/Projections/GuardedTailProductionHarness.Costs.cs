using System.Text.Json;

using Dapr.Actors;
using Dapr.Actors.Runtime;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;
using Hexalith.EventStore.Server.Tests.Actors;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Hexalith.EventStore.Server.Tests.Projections;

internal sealed partial class GuardedTailProductionHarness
{
    internal IProjectionDeliveryRetryScheduler Scheduler => _scheduler;
    internal int TransitionAttempts { get; }

    internal async Task RunPersistedRetryWorkerAsync()
    {
        using var worker = new ProjectionDeliveryRetryWorker(_scheduler, _named, _actors, Protection,
            Options.Create(new EventStoreActorOptions()), Options.Create(new ProjectionDispatchOptions()),
            Substitute.For<IProjectionRebuildCheckpointStore>(), Clock,
            NullLogger<ProjectionDeliveryRetryWorker>.Instance, Probe);
        await worker.RunOnceAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private AggregateActor? _productionSource;
    private FaultInjectingActorStateManager? _productionSourceState;
    internal Func<Task>? BeforeDeliveryReleaseAsync { get; set; }
    internal Func<Task>? AfterDeliveryReleaseAsync { get; set; }
    internal Func<Task>? AfterCompletionSaveAsync { get; set; }

    internal async Task AppendPersistedSourceEventAsync()
    {
        AppendSourceEvent();
        await _productionSourceState!.SetStateAsync(Identity.EventStreamKeyPrefix + Head.HeadSequence, Events[^1]).ConfigureAwait(false);
        await _productionSourceState.SetStateAsync(Identity.MetadataKey,
            new AggregateMetadata(Head.HeadSequence, Clock.GetUtcNow(), null) { SourceProof = Head, RetainedFloor = Head.RetainedFloor })
            .ConfigureAwait(false);
        await _productionSourceState.SaveStateAsync().ConfigureAwait(false);
    }

    internal async Task AdvancePersistedRetainedFloorAsync(long floor)
    {
        Head = Head with { RetainedFloor = floor };
        await _productionSourceState!.SetStateAsync(Identity.MetadataKey,
            new AggregateMetadata(Head.HeadSequence, Clock.GetUtcNow(), null)
                { SourceProof = Head, RetainedFloor = floor }).ConfigureAwait(false);
        await _productionSourceState.SaveStateAsync().ConfigureAwait(false);
    }
    private DaprProjectionDeliveryStateStore? _productionStates;

    internal void AdvanceRetainedFloor(long floor) => Head = Head with { RetainedFloor = floor };

    internal void SetTailPayloadSizes(int completed, params int[] sizes)
    {
        for (int index = 0; index < sizes.Length; index++)
        {
            EventEnvelope original = Events[completed + index];
            byte[] payload = new byte[sizes[index]];
            original.Payload.CopyTo(payload, 0);
            payload.AsSpan(original.Payload.Length).Fill((byte)' ');
            Events[completed + index] = original with { Payload = payload };
        }
        Head = SourceProofCodec.CreateSeed(Identity, Head.SourceIncarnation);
        foreach (EventEnvelope item in Events) Head = SourceProofCodec.Extend(Head, item);
    }

    internal void SetTailEnvelopeSizes(int completed, params int[] sizes)
    {
        for (int index = 0; index < sizes.Length; index++)
        {
            EventEnvelope original = Events[completed + index] with { UserId = string.Empty };
            int baseline = JsonSerializer.SerializeToUtf8Bytes(original, JsonSerializerOptions.Web).Length;
            Events[completed + index] = original with { UserId = new string('x', sizes[index] - baseline) };
        }
        Head = SourceProofCodec.CreateSeed(Identity, Head.SourceIncarnation);
        foreach (EventEnvelope item in Events) Head = SourceProofCodec.Extend(Head, item);
    }

    /// <summary>Uses actual aggregate/lifecycle actors and delivery/model stores for blocking cost observations.</summary>
    internal async Task UseProductionOwnersAsync(bool activateGuard = true)
    {
        var sourceState = new FaultInjectingActorStateManager();
        _productionSourceState = sourceState;
        var seed = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Identity.MetadataKey] = new AggregateMetadata(Head.HeadSequence, Clock.GetUtcNow(), null)
                { SourceProof = Head, RetainedFloor = Head.RetainedFloor },
        };
        foreach (EventEnvelope item in Events.Where(item => item.SequenceNumber >= Head.RetainedFloor))
            seed[Identity.EventStreamKeyPrefix + item.SequenceNumber] = item;
        await sourceState.SeedCommittedStateAsync(seed).ConfigureAwait(false);
        var host = ActorHost.CreateForTest<AggregateActor>(new ActorTestOptions { ActorId = new ActorId(Identity.ActorId) });
        _productionSource = new(host, NullLogger<AggregateActor>.Instance, Substitute.For<IDomainServiceInvoker>(),
            Substitute.For<ISnapshotManager>(), new NoOpEventPayloadProtectionService(), Substitute.For<ICommandStatusStore>(),
            Substitute.For<IEventPublisher>(), Options.Create(new EventDrainOptions()), Options.Create(new BackpressureOptions()),
            Substitute.For<IDeadLetterPublisher>());
        ActorStateManagerTestHelper.SetStateManager(_productionSource, sourceState);
        foreach (string route in Catalog.OrdinaryProjectionTypes)
        {
            if (!activateGuard) Rows[route] = Rows[route] with { SchemaVersion = 1, GuardEvidenceVersion = null, GuardEvidence = null };
            var lifecycle = new ProjectionLifecycleActor(ActorHost.CreateForTest<ProjectionLifecycleActor>(
                new ActorTestOptions { ActorId = new ActorId(Identity.ActorId + ":" + route) }),
                NullLogger<ProjectionLifecycleActor>.Instance, Clock);
            var state = new FaultInjectingActorStateManager();
            await state.SeedCommittedStateAsync(new Dictionary<string, object> { ["projection-lifecycle-epoch"] = 1L }).ConfigureAwait(false);
            ActorStateManagerTestHelper.SetStateManager(lifecycle, state);
            _lifecycle.ReadSnapshotAsync(Identity, route, Arg.Any<CancellationToken>()).Returns(_ => lifecycle.ReadSnapshotAsync());
            _lifecycle.BeginDeliveryWriteAsync(Identity, route, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => lifecycle.BeginDeliveryWriteAsync(new(call.ArgAt<string>(2))));
            _lifecycle.CompleteDeliveryWriteAsync(Identity, route, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(async call =>
                {
                    if (BeforeDeliveryReleaseAsync is { } before)
                    {
                        BeforeDeliveryReleaseAsync = null;
                        await before().ConfigureAwait(false);
                    }
                    Clock.Advance(ReleaseDelay);
                    call.ArgAt<CancellationToken>(3).ThrowIfCancellationRequested();
                    bool released = await lifecycle.CompleteDeliveryWriteAsync(new(call.ArgAt<string>(2))).ConfigureAwait(false);
                    if (released && AfterDeliveryReleaseAsync is { } after)
                    {
                        AfterDeliveryReleaseAsync = null;
                        await after().ConfigureAwait(false);
                    }
                    return released;
                });
            _lifecycle.BeginRebuildAsync(Identity, route, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => lifecycle.BeginRebuildAsync(new(call.ArgAt<string>(2))));
            _lifecycle.CompleteRebuildAsync(Identity, route, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(call => lifecycle.CompleteRebuildAsync(new(call.ArgAt<string>(2))));
            Backend.SeedByteStore(ProjectionDeliveryStateKeys.GetStateKey(Identity, route),
                JsonSerializer.SerializeToUtf8Bytes(Rows[route], JsonSerializerOptions.Web));
            if (activateGuard && Catalog.SourceGuardCapabilities.TryGetValue(route, out var capability))
                Backend.SeedByteStore(ProjectionDeliveryStateKeys.GetSourceGuardKey(Identity, route),
                JsonSerializer.SerializeToUtf8Bytes(new ProjectionSourceGuard(1, 1, 2, 2, 1, Identity.TenantId,
                    Identity.Domain, Identity.AggregateId, route, Head.SourceIncarnation, Catalog.CatalogFingerprint,
                    capability.HandlerSemantics, ProjectionSourceGuard.ApprovedSpecSha256,
                    new string('b', 40), Clock.GetUtcNow()), JsonSerializerOptions.Web));
        }
        Backend.SeedByteStore(ProjectionDeliveryStateKeys.WriterProtocol, JsonSerializer.SerializeToUtf8Bytes(
            new ProjectionDeliveryWriterProtocol(1, 2, new string('b', 40), Clock.GetUtcNow()), JsonSerializerOptions.Web));
        _productionStates = new(Backend, Options.Create(new ProjectionOptions()), Options.Create(new ProjectionDeliveryIdempotencyOptions
            { CompletedReceiptLimit = ReceiptLimit, MaxStateTransitionAttempts = TransitionAttempts }));
    }
}
