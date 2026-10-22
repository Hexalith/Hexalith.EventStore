using System.Text.Json;
using System.Diagnostics.Metrics;

using Dapr.Actors;
using Dapr.Actors.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Contracts.Security;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class ProjectionReplayRecoveryWorkerTests
{
    [Theory]
    [InlineData("complete", 257, null)]
    [InlineData("owner", 10_001, "delivery_owner_limit_exceeded")]
    [InlineData("page", 1, "delivery_page_limit_exceeded")]
    [InlineData("expanded", 72, "delivery_byte_limit_exceeded")]
    [InlineData("gap", 2, "delivery_gap")]
    [InlineData("reverse", 2, "delivery_gap")]
    [InlineData("repeat", 2, "delivery_identity_conflict")]
    [InlineData("cancel", 257, null)]
    public async Task RecoveryBoundWorker_EnforcesOwnerSourceAndReadableBoundsBeforeDispatchAndRetainsOriginalWork(
        string scenario, int head, string? expectedReason)
    {
        var identity = new AggregateIdentity("tenant-a", "counter", "counter-1");
        DateTimeOffset now = DateTimeOffset.UnixEpoch;
        string Id(int sequence) => $"01J{sequence:D23}";
        var work = new ProjectionDeliveryRetryWorkItem(ProjectionDeliveryRetryWorkItem.CreateWorkId(identity.TenantId, identity.Domain,
            identity.AggregateId, head), identity.TenantId, identity.Domain, identity.AggregateId, "sample", "v1", head, Id(head),
            ["counter-detail"], [], Id(head), new string('a', 64), 0, now, null)
            { ReplayRecoveryBindings = new Dictionary<string, string> { ["counter-detail"] = new('b', 64) } };
        var scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        scheduler.GetDueAsync(now, 64, Arg.Any<CancellationToken>()).Returns([work]);
        scheduler.TryAcquireAsync(work, Arg.Any<string>(), now, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(work with { Revision = 1, LeaseOwner = "owner" });
        scheduler.TryUpdateAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>()).Returns(true);
        IAggregateActor source = Substitute.For<IAggregateActor>();
        var metadata = new EventStorePayloadProtectionMetadata(PayloadProtectionState.Protected, 1, "fixture", null, null, null);
        EventEnvelope Envelope(int sequence) => new(Id(sequence), identity.AggregateId, "counter", identity.TenantId, identity.Domain,
            sequence, sequence, now, "correlation", "cause", "user", "v1", "counter-incremented", 1,
            scenario == "expanded" ? "fixture+protected+json" : "json", "{}"u8.ToArray(), scenario == "expanded"
                ? EventStorePayloadProtectionMetadataCarrier.Write((IDictionary<string, string>?)null, metadata) : null);
        source.ReadMeasuredEventsBoundedRangeAsync(Arg.Any<long>(), head, 256, 2_097_152).Returns(call =>
        {
            if (scenario == "page") return new SourceEventRange([], 2) { ReasonCode = "delivery_page_limit_exceeded" };
            if (scenario == "gap") return new([], 2);
            int after = checked((int)call.ArgAt<long>(0));
            EventEnvelope[] page = [.. Enumerable.Range(after + 1, Math.Min(256, head - after)).Select(Envelope)];
            if (scenario == "reverse") page = [page[1], page[0]];
            if (scenario == "repeat") page = [page[0], page[0]];
            return new(page, JsonSerializer.SerializeToUtf8Bytes(page, JsonSerializerOptions.Web).Length);
        });
        IActorProxyFactory actors = Substitute.For<IActorProxyFactory>();
        actors.CreateActorProxy<IAggregateActor>(Arg.Any<ActorId>(), Arg.Any<string>(), Arg.Any<ActorProxyOptions>()).Returns(source);
        IEventPayloadProtectionService protection = Substitute.For<IEventPayloadProtectionService>();
        int providerCalls = 0;
        protection.TryUnprotectEventPayloadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<string>(), Arg.Any<byte[]>(),
            Arg.Any<string>(), Arg.Any<EventStorePayloadProtectionMetadata?>(), Arg.Any<CancellationToken>()).Returns(_ =>
            { providerCalls++; return PayloadUnprotectionOutcome.Readable(new byte[1_048_576], "json", metadata); });
        INamedProjectionDispatchCoordinator coordinator = Substitute.For<INamedProjectionDispatchCoordinator>();
        coordinator.TryDispatchHeadAsync(Arg.Any<AggregateIdentity>(), Arg.Any<Server.DomainServices.DomainServiceRegistration>(),
            head, Id(head), Arg.Any<ProjectionEventDto[]>(), Arg.Any<CancellationToken>()).Returns(true);
        using var worker = new ProjectionDeliveryRetryWorker(scheduler, coordinator, actors, protection,
            Options.Create(new EventStoreActorOptions()), Options.Create(new ProjectionDispatchOptions()),
            Substitute.For<IProjectionRebuildCheckpointStore>(), new FakeTimeProvider(now), NullLogger<ProjectionDeliveryRetryWorker>.Instance);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        using var cancellation = new CancellationTokenSource();
        var active = new AsyncLocal<bool> { Value = true };
        int decoded = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        { if (scenario == "cancel" && instrument.Name == ProjectionDeliveryCostDiagnostics.CounterName) current.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            if (active.Value && tags.ToArray().Any(static tag => tag.Key == ProjectionDeliveryCostDiagnostics.CategoryTag
                && tag.Value as string == nameof(ProjectionDeliveryCost.WireEvents)) && ++decoded == 256) cancellation.Cancel();
        });
        listener.Start();
        if (scenario == "cancel") await Should.ThrowAsync<OperationCanceledException>(() => worker.RunOnceAsync(cancellation.Token));
        else await worker.RunOnceAsync(CancellationToken.None);
        active.Value = false;
        _ = coordinator.Received(expectedReason is null && scenario != "cancel" ? 1 : 0).TryDispatchHeadAsync(Arg.Any<AggregateIdentity>(),
            Arg.Any<Server.DomainServices.DomainServiceRegistration>(), head, work.HeadMessageId,
            Arg.Is<ProjectionEventDto[]>(events => events.Length == head && events[events.Length - 1].MessageId == work.HeadMessageId), Arg.Any<CancellationToken>());
        _ = source.DidNotReceiveWithAnyArgs().GetEventsAsync(default);
        _ = source.DidNotReceiveWithAnyArgs().ReadEventsRangeAsync(default, default, default);
        if (expectedReason is not null)
        {
            _ = scheduler.Received(1).TryUpdateAsync(Arg.Is<ProjectionDeliveryRetryWorkItem>(updated => updated.WorkId == work.WorkId
                && updated.DispatchId == work.DispatchId && updated.ReplayRecoveryBindings == work.ReplayRecoveryBindings
                && updated.Attempt == 1 && updated.LastReasonCode == expectedReason), Arg.Any<CancellationToken>());
            _ = scheduler.DidNotReceiveWithAnyArgs().TryDeleteAsync(default!, default);
        }
        if (scenario == "owner") _ = actors.DidNotReceiveWithAnyArgs().CreateActorProxy<IAggregateActor>(default!, default!, default!);
        if (scenario == "cancel")
        {
            decoded.ShouldBe(256);
            _ = source.Received(1).ReadMeasuredEventsBoundedRangeAsync(0, head, 256, 2_097_152);
            _ = source.DidNotReceive().ReadMeasuredEventsBoundedRangeAsync(256, head, 256, 2_097_152);
            _ = scheduler.DidNotReceiveWithAnyArgs().TryDeleteAsync(default!, default);
        }
        if (scenario == "expanded")
        {
            providerCalls.ShouldBe(48);
            cost[ProjectionDeliveryCost.WireDecodedEvents].ShouldBe(48);
            cost[ProjectionDeliveryCost.WireSizingBytes].ShouldBeGreaterThan(67_108_864);
            cost[ProjectionDeliveryCost.WireBytes].ShouldBeLessThan(67_108_864);
        }
    }
}
