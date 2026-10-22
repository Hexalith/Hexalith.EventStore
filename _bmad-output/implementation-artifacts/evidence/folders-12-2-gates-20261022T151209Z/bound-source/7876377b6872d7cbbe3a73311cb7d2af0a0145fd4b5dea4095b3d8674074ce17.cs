using System.Text.Json;
using System.Net;
using System.Text;
using System.Runtime.CompilerServices;

using Dapr.Actors.Client;
using Dapr.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Actors;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.DomainServices;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed partial class NamedProjectionDispatchCoordinatorTests {
    private static readonly AggregateIdentity Identity = new("tenant-a", "widget", "widget-1");

    [Fact]
    public async Task RebuildOnlyCatalogFallsBackToLegacyDeliveryWithoutLosingRebuildOwnership() {
        var snapshot = new NamedProjectionRouteCatalogSnapshot([
            new NamedProjectionRouteCatalogEntry("widget-service", "v1", "widget",
                ProjectionDispatchProtocol.Version, ProjectionDispatchProtocol.Capability, "rebuild-only",
                ["widget-shared"], null, null, null, null, null, ordinaryProjectionTypes: []),
        ]);
        var handler = new ProjectionDispatchHttpMessageHandler("{}");
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(snapshot, handler,
            out IProjectionDeliveryCheckpointStore checkpoints, out IProjectionLifecycleGateway lifecycle);

        (await coordinator.TryDispatchAsync(Identity, Registration(), [Envelope(1)], [ProjectionEvent(1)],
            TestContext.Current.CancellationToken)).ShouldBeFalse();
        handler.Requests.ShouldBeEmpty();
        _ = await checkpoints.DidNotReceiveWithAnyArgs().ReadDeliveredSequenceAsync(default!, default!, default);
        _ = await lifecycle.DidNotReceiveWithAnyArgs().BeginDeliveryWriteAsync(default!, default!, default!, default);
        NamedProjectionRebuildResult rebuild = await coordinator.AcquireRebuildLifecyclesAsync(Identity,
            Registration(), "explicit-rebuild", TestContext.Current.CancellationToken);
        rebuild.Owned.ShouldBeTrue();
        rebuild.Succeeded.ShouldBeTrue();
        rebuild.LifecycleProjectionTypes.ShouldBe(["widget-shared"]);
    }

    [Fact]
    public async Task MappedPartStreamsCrossFormerOneGibibyteCeilingWithBoundedBuffers() {
        byte[] source = new byte[ProjectionRebuildTransportReference.PartBytes];
        Random.Shared.NextBytes(source);
        using var measure = new ProjectionRebuildHashCountStream();
        int largestPart = 0;
        using var uploads = new ProjectionRebuildPartUploadStream((_, data, _) => {
            largestPart = Math.Max(largestPart, data.Length);
            return Task.CompletedTask;
        });
        for (int index = 0; index < 4097; index++) {
            await measure.WriteAsync(source, TestContext.Current.CancellationToken);
            await uploads.WriteAsync(source, TestContext.Current.CancellationToken);
        }
        await uploads.CompleteAsync(TestContext.Current.CancellationToken);

        measure.Length.ShouldBeGreaterThan(1L << 30);
        measure.Digest.Length.ShouldBe(64);
        uploads.PartCount.ShouldBe(4097);
        largestPart.ShouldBe(ProjectionRebuildTransportReference.PartBytes);
        ProjectionRebuildTransportReference.MaximumParts.ShouldBeGreaterThan(uploads.PartCount);
    }

    [Fact]
    public async Task TryRebuildAsync_CoordinatesEveryCatalogRouteWithoutDeliveryCheckpointMutation() {
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [
                new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null),
                new ProjectionDispatchOutcome("widget-index", ProjectionDispatchStatus.AlreadyCompleted, null, null),
            ]));
        var handler = new ProjectionDispatchHttpMessageHandler(responseJson);
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-index", "widget-detail"),
            handler,
            out IProjectionDeliveryCheckpointStore checkpoints,
            out IProjectionLifecycleGateway lifecycle);

        NamedProjectionRebuildResult result = await coordinator.TryRebuildAsync(
            Identity,
            Registration(),
            [ProjectionEvent(1), ProjectionEvent(2)],
            2,
            "operation-1",
            CancellationToken.None);

        result.Owned.ShouldBeTrue();
        result.Succeeded.ShouldBeTrue();
        result.Outcomes.Select(static outcome => outcome.ProjectionType)
            .ShouldBe(["widget-detail", "widget-index"]);
        result.LifecycleProjectionTypes.ShouldBe(["widget-detail", "widget-index"]);
        handler.RequestUri!.AbsolutePath.ShouldEndWith("/project/rebuild/stage/v1");
        ProjectionDispatchRequest request = JsonSerializer.Deserialize<ProjectionDispatchRequest>(
            handler.RequestJson!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldNotBeNull();
        request.DispatchId.ShouldBe("operation-1");
        request.Request.RebuildSourceHeadSequence.ShouldBe(2);
        request.CatalogFingerprint.ShouldBe("fingerprint");
        request.ProjectionTypes.ShouldBe(["widget-detail", "widget-index"]);
        _ = await checkpoints.DidNotReceiveWithAnyArgs().ReadDeliveredSequenceAsync(default!, default!, default);
        _ = await checkpoints.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default!, default, default);
        _ = await lifecycle.DidNotReceiveWithAnyArgs().BeginDeliveryWriteAsync(default!, default!, default!, default);
        _ = await lifecycle.Received(1).BeginRebuildAsync(Identity, "widget-detail", "operation-1", Arg.Any<CancellationToken>());
        _ = await lifecycle.Received(1).BeginRebuildAsync(Identity, "widget-index", "operation-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LargeMappedReplayUploadsBoundedPartsBeforeProtectedStage() {
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null)]));
        var handler = new ProjectionDispatchHttpMessageHandler(responseJson);
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail"), handler, out _, out _);
        ProjectionEventDto large = ProjectionEvent(1) with { Payload = new byte[400_000] };

        NamedProjectionRebuildResult result = await coordinator.TryRebuildAsync(
            Identity, Registration(), [large], 1, "bounded-rebuild", CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
        handler.Requests.Count.ShouldBeGreaterThan(2);
        handler.Requests.Take(handler.Requests.Count - 1).ShouldAllBe(request =>
            request.Path.EndsWith("/project/rebuild/part/v1", StringComparison.Ordinal));
        handler.Requests.Last().Path.ShouldEndWith("/project/rebuild/stage/v1");
        ProjectionDispatchRequest staged = JsonSerializer.Deserialize<ProjectionDispatchRequest>(
            handler.Requests.Last().Body!, new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldNotBeNull();
        staged.Request.Events.ShouldBeEmpty();
        staged.Request.RebuildSourceHeadSequence.ShouldBe(1);
        staged.RebuildTransport.ShouldNotBeNull().PartCount.ShouldBe(handler.Requests.Count - 1);
        handler.Requests.Take(handler.Requests.Count - 1).ShouldAllBe(request =>
            request.Body!.Length < 512_000);
    }

    [Fact]
    public async Task SourceStreamedReplayUploadsMoreThan64MiBWithoutRetainingPartBodies() {
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null)]));
        var handler = new ProjectionDispatchHttpMessageHandler(responseJson, retainPartBodies: false);
        var snapshot = new NamedProjectionRouteCatalogSnapshot([
            new NamedProjectionRouteCatalogEntry("widget-service", "v1", "widget",
                ProjectionDispatchProtocol.Version, ProjectionDispatchProtocol.Capability,
                "fingerprint", ["widget-detail"], ["widget-detail"], ["widget-detail"]),
        ]);
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(snapshot, handler, out _, out _);
        byte[] entropy = new byte[96 * 1024];
        Random.Shared.NextBytes(entropy);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(Convert.ToBase64String(entropy));
        async Task<string?> WriteSource(Stream output, CancellationToken token) {
            await output.WriteAsync(new byte[] { (byte)'[' }, token);
            for (int position = 1; position <= 400; position++) {
                if (position > 1) await output.WriteAsync(new byte[] { (byte)',' }, token);
                await JsonSerializer.SerializeAsync(output,
                    ProjectionEvent(position) with { Payload = payload },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web), token);
            }
            await output.WriteAsync(new byte[] { (byte)']' }, token);
            return null;
        }

        (await coordinator.SupportsMappedOnlyStreamedRebuildAsync(
            Identity, Registration(), "widget-detail", CancellationToken.None)).ShouldBeTrue();
        ProjectionMappedReplayStageResult staged = await coordinator
            .StageMappedOnlyStreamedRebuildAsync(Identity, Registration(), "source-large",
                400, WriteSource, CancellationToken.None);

        staged.Result.Succeeded.ShouldBeTrue();
        ProjectionDispatchRequest header = staged.Header.ShouldNotBeNull();
        header.Request.Events.ShouldBeEmpty();
        header.Request.RebuildSourceHeadSequence.ShouldBe(400);
        header.RebuildTransport.ShouldNotBeNull().PartCount.ShouldBeGreaterThan(256);
        handler.Requests.Count.ShouldBe(header.RebuildTransport.PartCount + 1);
        handler.Requests.Last().Path.ShouldEndWith("/project/rebuild/stage/v1");
        handler.LargestPartBodyBytes.ShouldBeLessThan(512_000);
        handler.Requests.Take(handler.Requests.Count - 1)
            .ShouldAllBe(request => request.Body == null);
    }

    [Fact]
    public async Task MappedOrdinaryPartialCommitRetriesAfterCoordinatorRestartWithoutRepeatingSharedRoute() {
        var snapshot = new NamedProjectionRouteCatalogSnapshot([
            new NamedProjectionRouteCatalogEntry("widget-service", "v1", "widget",
                ProjectionDispatchProtocol.Version, ProjectionDispatchProtocol.Capability,
                "fingerprint", ["widget-detail", "widget-index", "widget-token-index"], ["widget-detail"],
                ["widget-detail"], ["widget-index"], ["widget-token-index"]),
        ]);
        var reader = new FixedPagedHistoryReader([ProjectionEvent(1), ProjectionEvent(2)]);
        var scheduler = new MemoryRetryScheduler();
        var wire = new MappedOrdinaryHttpHandler();
        var states = new Dictionary<string, (ProjectionDeliveryState State, int Revision)>(StringComparer.Ordinal);
        IProjectionDeliveryStateStore store = Substitute.For<IProjectionDeliveryStateStore>();
        _ = store.ReadAsync(Identity, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => {
                string route = call.ArgAt<string>(1);
                bool found = states.TryGetValue(route, out (ProjectionDeliveryState State, int Revision) entry);
                ProjectionDeliveryState? state = found ? entry.State : null;
                return new ProjectionDeliveryStateReadResult(state,
                    found ? entry.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
                    ProjectionDeliveryStateClassifier.Classify(state, protocolV2Active: true), true);
            });
        _ = store.TrySaveAsync(Identity, Arg.Any<string>(), Arg.Any<ProjectionDeliveryState>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => {
                string route = call.ArgAt<string>(1);
                int revision = states.TryGetValue(route, out (ProjectionDeliveryState State, int Revision) entry)
                    ? entry.Revision : 0;
                if (!string.Equals(call.ArgAt<string>(3), revision == 0 ? string.Empty :
                    revision.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal))
                    return false;
                states[route] = (call.ArgAt<ProjectionDeliveryState>(2), revision + 1);
                return true;
            });
        var idempotency = new ProjectionDeliveryIdempotencyCoordinator(store,
            Options.Create(new ProjectionDeliveryIdempotencyOptions()), TimeProvider.System);
        NamedProjectionDispatchCoordinator first = CreateCoordinator(snapshot, wire, out _, out _,
            scheduler, idempotencyCoordinator: idempotency, historyReader: reader);

        (await first.SupportsMappedOnlyStreamedDeliveryAsync(Identity, Registration(),
            CancellationToken.None)).ShouldBeTrue();
        (await first.TryDispatchMappedOnlyAsync(Identity, Registration(), 2, "message-2",
            CancellationToken.None)).ShouldBeTrue();

        scheduler.Work.ShouldNotBeNull().PendingRoutes.ShouldBe(["widget-detail"]);
        states["widget-index"].State.LastDeliveredSequence.ShouldBe(2);
        states["widget-token-index"].State.LastDeliveredSequence.ShouldBe(2);
        states["widget-detail"].State.LastDeliveredSequence.ShouldBe(0);
        wire.SharedCalls.ShouldBe(2);
        wire.CommitCalls.ShouldBe(1);

        NamedProjectionDispatchCoordinator restarted = CreateCoordinator(snapshot, wire, out _, out _,
            scheduler, idempotencyCoordinator: idempotency, historyReader: reader);
        (await restarted.TryDispatchMappedOnlyAsync(Identity, Registration(), 2, "message-2",
            CancellationToken.None)).ShouldBeTrue();

        scheduler.Work.ShouldBeNull();
        states["widget-index"].State.LastDeliveredSequence.ShouldBe(2);
        states["widget-token-index"].State.LastDeliveredSequence.ShouldBe(2);
        states["widget-detail"].State.LastDeliveredSequence.ShouldBe(2);
        wire.SharedCalls.ShouldBe(2);
        wire.CommitCalls.ShouldBe(2);
    }

    [Fact]
    public async Task MappedOrdinaryOrchestratorDeliversMoreThan64MiBFromPagedSource() {
        byte[] payload = new byte[128 * 1024];
        Random.Shared.NextBytes(payload);
        (JsonSerializer.SerializeToUtf8Bytes(ProjectionEvent(1) with { Payload = payload }).LongLength * 400)
            .ShouldBeGreaterThan(64L * 1024 * 1024);
        var snapshot = new NamedProjectionRouteCatalogSnapshot([
            new NamedProjectionRouteCatalogEntry("widget-service", "v1", "widget",
                ProjectionDispatchProtocol.Version, ProjectionDispatchProtocol.Capability,
                "fingerprint", ["widget-detail", "widget-index", "widget-token-index"], ["widget-detail"],
                ["widget-detail"], ["widget-index"], ["widget-token-index"]),
        ]);
        var scheduler = new MemoryRetryScheduler();
        var wire = new MappedOrdinaryHttpHandler(failFirstCommit: false);
        IProjectionDeliveryIdempotencyCoordinator idempotency = Substitute.For<IProjectionDeliveryIdempotencyCoordinator>();
        _ = idempotency.TryAdmitHistoryAsync(Identity, Arg.Any<string>(),
                Arg.Any<ProjectionDeliveryFingerprintHistory>(), true, Arg.Any<CancellationToken>(),
                Arg.Any<long?>())
            .Returns(call => new ProjectionDeliveryAdmissionResult(
                ProjectionDeliveryAdmissionDisposition.Dispatch, null,
                Reservation("message-400") with {
                    EndSequence = 400,
                    ManifestFingerprint = call.ArgAt<ProjectionDeliveryFingerprintHistory>(2).PrefixFingerprint,
                }));
        _ = idempotency.CompleteHistoryAsync(Identity, Arg.Any<string>(),
                Arg.Any<ProjectionDeliveryFingerprintHistory>(),
                Arg.Any<ProjectionDeliveryReservation>(), Arg.Any<CancellationToken>())
            .Returns(ProjectionDeliveryCompletion.Completed);
        IActorProxyFactory proxies = Substitute.For<IActorProxyFactory>();
        IAggregateActor source = Substitute.For<IAggregateActor>();
        _ = source.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(true, 400));
        _ = source.ReadEventsRangeAsync(Arg.Any<long>(), 400, 1).Returns(call =>
            [Envelope(call.ArgAt<long>(0) + 1) with {
                MessageId = $"01J{call.ArgAt<long>(0) + 1:D23}", Payload = payload,
            }]);
        _ = source.GetEventsAsync(0).Returns<EventEnvelope[]>(_ =>
            throw new InvalidOperationException("LegacyArrayLimit"));
        _ = proxies.CreateActorProxy<IAggregateActor>(Arg.Any<Dapr.Actors.ActorId>(), nameof(AggregateActor))
            .Returns(source);
        var reader = new EventStoreProjectionDeliveryHistoryReader(proxies,
            new NoOpEventPayloadProtectionService(), Options.Create(new EventStoreActorOptions()));
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(snapshot, wire, out _, out _,
            scheduler, actorProxyFactory: proxies, idempotencyCoordinator: idempotency,
            historyReader: reader);
        IDomainServiceResolver resolver = Substitute.For<IDomainServiceResolver>();
        _ = resolver.ResolveAsync(Identity.TenantId, Identity.Domain, "v1", Arg.Any<CancellationToken>())
            .Returns(Registration());
        IProjectionCheckpointTracker tracker = Substitute.For<IProjectionCheckpointTracker>();
        _ = tracker.ReadLastDeliveredSequenceAsync(Identity, Arg.Any<CancellationToken>()).Returns(0);
        IProjectionActivationOutbox outbox = Substitute.For<IProjectionActivationOutbox>();
        ProjectionActivationWorkItem activation = ProjectionActivationWorkItem.Create(Identity, DateTimeOffset.UtcNow);
        _ = outbox.GetAsync(Identity, Arg.Any<CancellationToken>()).Returns(activation);
        var orchestrator = new ProjectionUpdateOrchestrator(proxies, new DaprClientBuilder().Build(),
            Substitute.For<IHttpClientFactory>(), resolver, tracker, Options.Create(new ProjectionOptions()),
            NullLogger<ProjectionUpdateOrchestrator>.Instance,
            namedProjectionDispatchCoordinator: coordinator, projectionActivationOutbox: outbox);

        await orchestrator.DeliverProjectionAsync(Identity, CancellationToken.None);

        _ = await source.DidNotReceive().GetEventsAsync(0);
        _ = await source.Received().ReadEventsRangeAsync(399, 400, 1);
        wire.SharedCalls.ShouldBe(400);
        wire.PartCalls.ShouldBeGreaterThan(256);
        wire.LargestPartBodyBytes.ShouldBeLessThan(512_000);
        wire.StageHeaders.ShouldBe(1);
        wire.CommitCalls.ShouldBe(1);
        scheduler.Work.ShouldBeNull();
        await outbox.Received(1).CompleteAsync(activation, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MappedOrdinaryDeliveryAcceptsTenThousandEventSourcePrefix() {
        const int count = 10_000;
        var snapshot = new NamedProjectionRouteCatalogSnapshot([
            new NamedProjectionRouteCatalogEntry("widget-service", "v1", "widget",
                ProjectionDispatchProtocol.Version, ProjectionDispatchProtocol.Capability,
                "fingerprint", ["widget-detail", "widget-index", "widget-token-index"],
                ["widget-detail"], ["widget-detail"], ["widget-index"],
                ["widget-token-index"]),
        ]);
        var reader = new FixedPagedHistoryReader([.. Enumerable.Range(1, count)
            .Select(sequence => ProjectionEvent(sequence))]);
        var scheduler = new MemoryRetryScheduler();
        var wire = new MappedOrdinaryHttpHandler(failFirstCommit: false);
        IProjectionDeliveryIdempotencyCoordinator idempotency = Substitute.For<IProjectionDeliveryIdempotencyCoordinator>();
        _ = idempotency.TryAdmitHistoryAsync(Identity, Arg.Any<string>(),
                Arg.Any<ProjectionDeliveryFingerprintHistory>(), true,
                Arg.Any<CancellationToken>(), Arg.Any<long?>())
            .Returns(call => new ProjectionDeliveryAdmissionResult(
                ProjectionDeliveryAdmissionDisposition.Dispatch, null,
                Reservation("message-10000") with {
                    EndSequence = count,
                    ManifestFingerprint = call.ArgAt<ProjectionDeliveryFingerprintHistory>(2).PrefixFingerprint,
                }));
        _ = idempotency.CompleteHistoryAsync(Identity, Arg.Any<string>(),
                Arg.Any<ProjectionDeliveryFingerprintHistory>(),
                Arg.Any<ProjectionDeliveryReservation>(), Arg.Any<CancellationToken>())
            .Returns(ProjectionDeliveryCompletion.Completed);
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(snapshot, wire, out _, out _,
            scheduler, idempotencyCoordinator: idempotency, historyReader: reader);

        (await coordinator.TryDispatchMappedOnlyAsync(Identity, Registration(), count,
            "message-10000", CancellationToken.None)).ShouldBeTrue();

        wire.SharedCalls.ShouldBe(count);
        wire.StageHeaders.ShouldBe(1);
        wire.CommitCalls.ShouldBe(1);
        scheduler.Work.ShouldBeNull();
    }

    [Fact]
    public async Task MappedOrdinaryNextEventUsesAcceptedSuffixForAllThreeRoutes() {
        const int head = 10_000;
        var snapshot = new NamedProjectionRouteCatalogSnapshot([
            new NamedProjectionRouteCatalogEntry("widget-service", "v1", "widget",
                ProjectionDispatchProtocol.Version, ProjectionDispatchProtocol.Capability,
                "fingerprint", ["widget-detail", "widget-index", "widget-token-index"],
                ["widget-detail"], ["widget-detail"],
                ["widget-index", "widget-token-index"], [], ["widget-detail"]),
        ]);
        var reader = new SuffixOnlyPagedHistoryReader(ProjectionEvent(head));
        var scheduler = new MemoryRetryScheduler();
        var wire = new IncrementalMappedHttpHandler();
        IProjectionDeliveryIdempotencyCoordinator idempotency = Substitute.For<IProjectionDeliveryIdempotencyCoordinator>();
        _ = idempotency.ReadAcceptedPrefixAsync(Identity, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new ProjectionDeliveryAcceptedPrefix(head - 1,
                ProjectionDeliveryFingerprint.ComputeInitial(Identity, call.ArgAt<string>(1),
                    positionSensitive: true)));
        _ = idempotency.TryAdmitHistoryAsync(Identity, Arg.Any<string>(),
                Arg.Any<ProjectionDeliveryFingerprintHistory>(), true,
                Arg.Any<CancellationToken>(), Arg.Any<long?>())
            .Returns(call => new ProjectionDeliveryAdmissionResult(
                ProjectionDeliveryAdmissionDisposition.Dispatch, null,
                Reservation("message-10000") with {
                    EndSequence = head,
                    ManifestFingerprint = call.ArgAt<ProjectionDeliveryFingerprintHistory>(2).PrefixFingerprint,
                }));
        _ = idempotency.CompleteHistoryAsync(Identity, Arg.Any<string>(),
                Arg.Any<ProjectionDeliveryFingerprintHistory>(),
                Arg.Any<ProjectionDeliveryReservation>(), Arg.Any<CancellationToken>())
            .Returns(ProjectionDeliveryCompletion.Completed);
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(snapshot, wire, out _, out _,
            scheduler, idempotencyCoordinator: idempotency, historyReader: reader);

        (await coordinator.TryDispatchMappedOnlyAsync(Identity, Registration(), head,
            "message-10000", CancellationToken.None)).ShouldBeTrue();

        reader.FullReads.ShouldBe(0);
        reader.SuffixReads.ShouldBe(7);
        wire.IncrementalCalls.ShouldBe(1);
        wire.StagedCalls.ShouldBe(0);
        scheduler.Work.ShouldBeNull();
    }

    [Fact]
    public async Task IncrementalFallbackResumesAfterRestartWithoutRepeatingSharedRoutes() {
        const int head = 3;
        var snapshot = new NamedProjectionRouteCatalogSnapshot([
            new NamedProjectionRouteCatalogEntry("widget-service", "v1", "widget",
                ProjectionDispatchProtocol.Version, ProjectionDispatchProtocol.Capability,
                "fingerprint", ["widget-detail", "widget-index", "widget-token-index"],
                ["widget-detail"], ["widget-detail"],
                ["widget-index", "widget-token-index"], [], ["widget-detail"]),
        ]);
        var reader = new FixedPagedHistoryReader([ProjectionEvent(1), ProjectionEvent(2), ProjectionEvent(3)]);
        var scheduler = new MemoryRetryScheduler();
        var wire = new IncrementalFallbackHttpHandler();
        var completed = new Dictionary<string, string>(StringComparer.Ordinal);
        IProjectionDeliveryIdempotencyCoordinator idempotency = Substitute.For<IProjectionDeliveryIdempotencyCoordinator>();
        _ = idempotency.ReadAcceptedPrefixAsync(Identity, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => {
                string route = call.ArgAt<string>(1);
                return completed.TryGetValue(route, out string? fingerprint)
                    ? new ProjectionDeliveryAcceptedPrefix(head, fingerprint)
                    : new ProjectionDeliveryAcceptedPrefix(head - 1,
                        ProjectionDeliveryFingerprint.ComputeInitial(Identity, route, positionSensitive: true));
            });
        _ = idempotency.UseLegacyMappedIdentityAsync(Identity, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _ = idempotency.TryAdmitHistoryAsync(Identity, Arg.Any<string>(),
                Arg.Any<ProjectionDeliveryFingerprintHistory>(), true,
                Arg.Any<CancellationToken>(), Arg.Any<long?>())
            .Returns(call => completed.ContainsKey(call.ArgAt<string>(1))
                ? new ProjectionDeliveryAdmissionResult(ProjectionDeliveryAdmissionDisposition.AlreadyCompleted,
                    null, null)
                : new ProjectionDeliveryAdmissionResult(ProjectionDeliveryAdmissionDisposition.Dispatch, null,
                    Reservation("message-3") with {
                        EndSequence = head,
                        ManifestFingerprint = call.ArgAt<ProjectionDeliveryFingerprintHistory>(2).PrefixFingerprint,
                    }));
        _ = idempotency.CompleteHistoryAsync(Identity, Arg.Any<string>(),
                Arg.Any<ProjectionDeliveryFingerprintHistory>(),
                Arg.Any<ProjectionDeliveryReservation>(), Arg.Any<CancellationToken>())
            .Returns(call => {
                completed[call.ArgAt<string>(1)] = call.ArgAt<ProjectionDeliveryFingerprintHistory>(2)
                    .PrefixFingerprint;
                return ProjectionDeliveryCompletion.Completed;
            });
        NamedProjectionDispatchCoordinator first = CreateCoordinator(snapshot, wire, out _, out _,
            scheduler, idempotencyCoordinator: idempotency, historyReader: reader);

        (await first.TryDispatchMappedOnlyAsync(Identity, Registration(), head,
            "message-3", CancellationToken.None)).ShouldBeTrue();
        scheduler.Work.ShouldNotBeNull().PendingRoutes.ShouldBe(["widget-detail"]);
        completed.Keys.Order(StringComparer.Ordinal).ShouldBe(["widget-index", "widget-token-index"]);
        wire.SharedDeliveries.ShouldBe(2);
        wire.CommitCalls.ShouldBe(1);

        NamedProjectionDispatchCoordinator restarted = CreateCoordinator(snapshot, wire, out _, out _,
            scheduler, idempotencyCoordinator: idempotency, historyReader: reader);
        (await restarted.TryDispatchMappedOnlyAsync(Identity, Registration(), head,
            "message-3", CancellationToken.None)).ShouldBeTrue();

        scheduler.Work.ShouldBeNull();
        completed.Keys.Order(StringComparer.Ordinal)
            .ShouldBe(["widget-detail", "widget-index", "widget-token-index"]);
        wire.SharedDeliveries.ShouldBe(2);
        wire.CommitCalls.ShouldBe(2);
        wire.StageCalls.ShouldBe(2);
    }

    [Fact]
    public async Task SequentialNearLimitDeliveryReadsOnlyNextSourceEventAfterCompletedPrefix() {
        var snapshot = new NamedProjectionRouteCatalogSnapshot([
            new NamedProjectionRouteCatalogEntry("widget-service", "v1", "widget",
                ProjectionDispatchProtocol.Version, ProjectionDispatchProtocol.Capability,
                "fingerprint", ["widget-detail", "widget-index", "widget-token-index"],
                ["widget-detail"], ["widget-detail"],
                ["widget-index", "widget-token-index"], [], ["widget-detail"]),
        ]);
        var reader = new FixedPagedHistoryReader([.. Enumerable.Range(1, 10_001)
            .Select(sequence => ProjectionEvent(sequence))]);
        var scheduler = new MemoryRetryScheduler();
        var wire = new MappedOrdinaryHttpHandler(failFirstCommit: false, enableIncremental: true);
        var states = new Dictionary<string, (ProjectionDeliveryState State, int Revision)>(StringComparer.Ordinal);
        IProjectionDeliveryStateStore store = Substitute.For<IProjectionDeliveryStateStore>();
        _ = store.ReadAsync(Identity, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => {
                string route = call.ArgAt<string>(1);
                bool found = states.TryGetValue(route, out (ProjectionDeliveryState State, int Revision) entry);
                ProjectionDeliveryState? state = found ? entry.State : null;
                return new ProjectionDeliveryStateReadResult(state,
                    found ? entry.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
                    ProjectionDeliveryStateClassifier.Classify(state, protocolV2Active: true), true);
            });
        _ = store.TrySaveAsync(Identity, Arg.Any<string>(), Arg.Any<ProjectionDeliveryState>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => {
                string route = call.ArgAt<string>(1);
                int revision = states.TryGetValue(route, out (ProjectionDeliveryState State, int Revision) entry)
                    ? entry.Revision : 0;
                if (!string.Equals(call.ArgAt<string>(3), revision == 0 ? string.Empty :
                    revision.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal))
                    return false;
                states[route] = (call.ArgAt<ProjectionDeliveryState>(2), revision + 1);
                return true;
            });
        var idempotency = new ProjectionDeliveryIdempotencyCoordinator(store,
            Options.Create(new ProjectionDeliveryIdempotencyOptions()), TimeProvider.System);
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(snapshot, wire, out _, out _,
            scheduler, idempotencyCoordinator: idempotency, historyReader: reader);

        (await coordinator.TryDispatchMappedOnlyAsync(Identity, Registration(), 9_999,
            "message-9999", CancellationToken.None)).ShouldBeTrue();
        scheduler.Work.ShouldBeNull();
        wire.SharedCalls.ShouldBe(9_999);
        wire.StageHeaders.ShouldBe(1);
        int fullCalls = reader.FullReadCalls;
        int suffixEvents = reader.SuffixEventsRead;
        int partCalls = wire.PartCalls;

        (await coordinator.TryDispatchMappedOnlyAsync(Identity, Registration(), 10_000,
            "message-10000", CancellationToken.None)).ShouldBeTrue();

        scheduler.Work.ShouldBeNull();
        reader.FullReadCalls.ShouldBe(fullCalls);
        (reader.SuffixEventsRead - suffixEvents).ShouldBe(7);
        wire.SharedCalls.ShouldBe(10_000);
        wire.IncrementalCalls.ShouldBe(1);
        wire.StageHeaders.ShouldBe(1);
        wire.PartCalls.ShouldBe(partCalls);
        states.Values.ShouldAllBe(entry => entry.State.LastDeliveredSequence == 10_000);

        _ = await Should.ThrowAsync<ArgumentException>(() => coordinator.TryDispatchMappedOnlyAsync(
            Identity, Registration(), 10_001, "message-10001", CancellationToken.None));

        reader.FullReadCalls.ShouldBe(fullCalls);
        wire.SharedCalls.ShouldBe(10_000);
        wire.IncrementalCalls.ShouldBe(1);
        wire.StageHeaders.ShouldBe(1);
        states.Values.ShouldAllBe(entry => entry.State.LastDeliveredSequence == 10_000);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyReplayRequiresAggregateOwnedCompletedErasure(bool erased) {
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null)]));
        var handler = new ProjectionDispatchHttpMessageHandler(responseJson);
        IActorProxyFactory proxies = Substitute.For<IActorProxyFactory>();
        IAggregateActor source = Substitute.For<IAggregateActor>();
        _ = source.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(
            Exists: false, CurrentSequence: 0, Erased: erased));
        _ = proxies.CreateActorProxy<IAggregateActor>(Arg.Any<Dapr.Actors.ActorId>(), "AggregateActor")
            .Returns(source);
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail"), handler, out _, out _, actorProxyFactory: proxies);

        NamedProjectionRebuildResult result = await coordinator.TryRebuildAsync(
            Identity, Registration(), [], 0, "empty-rebuild", CancellationToken.None);

        result.Owned.ShouldBeTrue();
        result.Succeeded.ShouldBe(erased);
        if (erased) {
            ProjectionDispatchRequest request = JsonSerializer.Deserialize<ProjectionDispatchRequest>(
                handler.RequestJson!, new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldNotBeNull();
            request.Request.Events.ShouldBeEmpty();
            request.Request.RebuildSourceHeadSequence.ShouldBe(0);
            request.Request.RebuildSourceErased.ShouldBeTrue();
        }
        else {
            handler.RequestJson.ShouldBeNull();
        }

        _ = await source.Received(1).GetStreamMetadataAsync();
    }

    [Fact]
    public async Task EmptyErasedReplayUsesConfiguredAggregateActorNameAndPrunes() {
        const string configuredActorName = "CustomAggregateActor";
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null)]));
        var handler = new ProjectionDispatchHttpMessageHandler(responseJson);
        IActorProxyFactory proxies = Substitute.For<IActorProxyFactory>();
        IAggregateActor source = Substitute.For<IAggregateActor>();
        _ = source.GetStreamMetadataAsync().Returns(new AggregateStreamMetadata(
            Exists: false, CurrentSequence: 0, Erased: true));
        _ = proxies.CreateActorProxy<IAggregateActor>(Arg.Any<Dapr.Actors.ActorId>(), configuredActorName)
            .Returns(source);
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail"), handler, out _, out _,
            actorProxyFactory: proxies, aggregateActorTypeName: configuredActorName);

        NamedProjectionRebuildResult result = await coordinator.TryRebuildAsync(
            Identity, Registration(), [], 0, "configured-erasure", CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
        ProjectionDispatchRequest request = JsonSerializer.Deserialize<ProjectionDispatchRequest>(
            handler.RequestJson!, new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldNotBeNull();
        request.Request.RebuildSourceErased.ShouldBeTrue();
        _ = proxies.Received(1).CreateActorProxy<IAggregateActor>(
            Arg.Any<Dapr.Actors.ActorId>(), configuredActorName);
    }

    [Fact]
    public async Task TryRebuildAsync_StagesOnlyFullReplayRoutesFromMixedCatalog() {
        const string fingerprint = "full-catalog-fingerprint";
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null)]));
        var handler = new ProjectionDispatchHttpMessageHandler(responseJson);
        var snapshot = new NamedProjectionRouteCatalogSnapshot([
            new NamedProjectionRouteCatalogEntry(
                "widget-service", "v1", "widget", ProjectionDispatchProtocol.Version,
                ProjectionDispatchProtocol.Capability, fingerprint,
                ["widget-detail", "widget-index"], ["widget-detail"]),
        ]);
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            snapshot, handler, out _, out IProjectionLifecycleGateway lifecycle);

        NamedProjectionRebuildResult result = await coordinator.TryRebuildAsync(
            Identity, Registration(), [ProjectionEvent(1)], 1, "mixed-rebuild", CancellationToken.None);

        result.Owned.ShouldBeTrue();
        result.Succeeded.ShouldBeTrue();
        result.LifecycleProjectionTypes.ShouldBe(["widget-detail"]);
        ProjectionDispatchRequest request = JsonSerializer.Deserialize<ProjectionDispatchRequest>(
            handler.RequestJson!, new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldNotBeNull();
        request.ProjectionTypes.ShouldBe(["widget-detail"]);
        request.CatalogFingerprint.ShouldBe(fingerprint);
        _ = await lifecycle.DidNotReceive().BeginRebuildAsync(
            Identity, "widget-index", Arg.Any<string>(), Arg.Any<CancellationToken>());

        (await coordinator.CommitRebuildAsync(Identity, Registration(), [ProjectionEvent(1)],
            "mixed-rebuild", CancellationToken.None)).Succeeded.ShouldBeTrue();
        (await coordinator.VerifyRebuildAsync(Identity, Registration(), [ProjectionEvent(1)],
            "mixed-rebuild", CancellationToken.None)).Succeeded.ShouldBeTrue();
        ProjectionDispatchRequest verification = JsonSerializer.Deserialize<ProjectionDispatchRequest>(
            handler.RequestJson!, new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldNotBeNull();
        verification.ProjectionTypes.ShouldBe(["widget-detail"]);
        verification.Request.RebuildSourceHeadSequence.ShouldBe(1);
    }

    [Fact]
    public async Task TryRebuildAsync_PartialOutcomeIsOwnedButCannotSucceed() {
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [
                new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null),
                new ProjectionDispatchOutcome(
                    "widget-index",
                    ProjectionDispatchStatus.Retryable,
                    null,
                    ProjectionDispatchReasonCodes.PartialRetry),
            ]));
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail", "widget-index"),
            new ProjectionDispatchHttpMessageHandler(responseJson),
            out _,
            out _);

        NamedProjectionRebuildResult result = await coordinator.TryRebuildAsync(
            Identity,
            Registration(),
            [ProjectionEvent(1)],
            1,
            "operation-1",
            CancellationToken.None);

        result.Owned.ShouldBeTrue();
        result.Succeeded.ShouldBeFalse();
        result.Outcomes[0].Status.ShouldBe(ProjectionDispatchStatus.Completed);
        result.Outcomes[1].Status.ShouldBe(ProjectionDispatchStatus.Retryable);
    }

    [Fact]
    public void NamedProjectionRebuildResult_AnyFailedRequiredRouteIsTerminal() {
        var result = new NamedProjectionRebuildResult(
            Owned: true,
            Succeeded: false,
            [
                new ProjectionDispatchOutcome(
                    "widget-detail",
                    ProjectionDispatchStatus.Failed,
                    null,
                    ProjectionDispatchReasonCodes.HandlerFailure),
                new ProjectionDispatchOutcome(
                    "widget-index",
                    ProjectionDispatchStatus.Retryable,
                    null,
                    ProjectionDispatchReasonCodes.PartialRetry),
            ],
            ["widget-detail", "widget-index"]);

        result.IsTerminalFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task TryDispatchAsync_FirstLifecycleReleaseFailureStillAttemptsEveryRoute() {
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [
                new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null),
                new ProjectionDispatchOutcome("widget-index", ProjectionDispatchStatus.Completed, null, null),
            ]));
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail", "widget-index"),
            new ProjectionDispatchHttpMessageHandler(responseJson),
            out IProjectionDeliveryCheckpointStore checkpoints,
            out IProjectionLifecycleGateway lifecycle);
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0);
        _ = checkpoints.SaveDeliveredSequenceAsync(Identity, Arg.Any<string>(), 1, Arg.Any<CancellationToken>()).Returns(true);
        // The delivery lifecycle lease is keyed by an opaque per-invocation token (not the stable
        // dispatch id), so the release op id is matched with Arg.Any rather than a fixed message id.
        _ = lifecycle.CompleteDeliveryWriteAsync(
                Identity,
                Arg.Any<string>(),
                Arg.Any<string>(),
                CancellationToken.None)
            .Returns(call => !string.Equals(call.ArgAt<string>(1), "widget-detail", StringComparison.Ordinal));

        _ = await Should.ThrowAsync<InvalidOperationException>(() => coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1)],
            [ProjectionEvent(1)],
            CancellationToken.None));

        _ = await lifecycle.Received(2).CompleteDeliveryWriteAsync(
            Identity,
            "widget-detail",
            Arg.Any<string>(),
            CancellationToken.None);
        _ = await lifecycle.Received(1).CompleteDeliveryWriteAsync(
            Identity,
            "widget-index",
            Arg.Any<string>(),
            CancellationToken.None);
    }

    [Fact]
    public async Task TryDispatchAsync_ReturnsFalseWhenExactCatalogBindingIsMissing() {
        ProjectionDispatchHttpMessageHandler handler = new("{}");
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            NamedProjectionRouteCatalogSnapshot.Empty,
            handler,
            out _,
            out _);

        bool handled = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1)],
            [],
            CancellationToken.None);

        handled.ShouldBeFalse();
        handler.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task TryDispatchAsync_MissingBindingRefreshesFromResolverAlignedRegistration() {
        var catalog = new NamedProjectionRouteCatalog();
        INamedProjectionCatalogRefresher refresher = Substitute.For<INamedProjectionCatalogRefresher>();
        _ = refresher.RefreshAsync(Arg.Any<DomainServiceRegistration>(), Arg.Any<CancellationToken>())
            .Returns(_ => {
                catalog.Upsert(Snapshot("fingerprint", "widget-detail").Entries);
                return true;
            });
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null)]));
        var handler = new ProjectionDispatchHttpMessageHandler(responseJson);
        IProjectionDeliveryCheckpointStore checkpoints = Substitute.For<IProjectionDeliveryCheckpointStore>();
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(0);
        _ = checkpoints.SaveDeliveredSequenceAsync(Identity, "widget-detail", 1, Arg.Any<CancellationToken>()).Returns(true);
        IProjectionLifecycleGateway lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        _ = lifecycle.BeginDeliveryWriteAsync(Identity, "widget-detail", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _ = lifecycle.CompleteDeliveryWriteAsync(Identity, "widget-detail", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));
        var coordinator = new NamedProjectionDispatchCoordinator(
            catalog,
            checkpoints,
            lifecycle,
            Substitute.For<IActorProxyFactory>(),
            new DaprClientBuilder().Build(),
            httpClientFactory,
            Options.Create(new ProjectionDispatchOptions()),
            NullLogger<NamedProjectionDispatchCoordinator>.Instance,
            catalogRefresher: refresher);

        bool handled = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1)],
            [],
            CancellationToken.None);

        handled.ShouldBeTrue();
        handler.CallCount.ShouldBe(1);
        _ = await refresher.Received(1).RefreshAsync(
            Arg.Is<DomainServiceRegistration>(registration => registration.AppId == "widget-service"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryDispatchAsync_UsesStableHeadIdentityAndAdvancesOnlySuccessfulProjection() {
        const string fingerprint = "catalog-fingerprint";
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [
                new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null),
                new ProjectionDispatchOutcome("widget-index", ProjectionDispatchStatus.Retryable, null, ProjectionDispatchReasonCodes.PartialRetry),
            ]));
        var handler = new ProjectionDispatchHttpMessageHandler(responseJson);
        NamedProjectionRouteCatalogSnapshot snapshot = Snapshot(fingerprint, "widget-index", "widget-detail");
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            snapshot,
            handler,
            out IProjectionDeliveryCheckpointStore checkpoints,
            out IProjectionLifecycleGateway lifecycle);
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0);
        _ = checkpoints.SaveDeliveredSequenceAsync(Identity, Arg.Any<string>(), 2, Arg.Any<CancellationToken>()).Returns(true);
        _ = lifecycle.BeginDeliveryWriteAsync(Identity, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        bool handled = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1), Envelope(2)],
            [],
            CancellationToken.None);

        handled.ShouldBeTrue();
        handler.CallCount.ShouldBe(1);
        ProjectionDispatchRequest request = JsonSerializer.Deserialize<ProjectionDispatchRequest>(
            handler.RequestJson!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldNotBeNull();
        request.DispatchId.ShouldBe("message-2");
        request.CatalogFingerprint.ShouldBe(fingerprint);
        request.ProjectionTypes.ShouldBe(["widget-detail", "widget-index"]);
        _ = await checkpoints.Received(1).SaveDeliveredSequenceAsync(
            Identity,
            "widget-detail",
            2,
            Arg.Any<CancellationToken>());
        _ = await checkpoints.DidNotReceive().SaveDeliveredSequenceAsync(
            Identity,
            "widget-index",
            Arg.Any<long>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryDispatchAsync_SkipsEndpointWhenEveryRouteIsDeniedBeforePersistence() {
        var handler = new ProjectionDispatchHttpMessageHandler("{}");
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail", "widget-index"),
            handler,
            out IProjectionDeliveryCheckpointStore checkpoints,
            out IProjectionLifecycleGateway lifecycle);
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(3);
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, "widget-index", Arg.Any<CancellationToken>()).Returns(0);
        _ = lifecycle.BeginDeliveryWriteAsync(Identity, "widget-index", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        bool handled = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1), Envelope(2)],
            [],
            CancellationToken.None);

        handled.ShouldBeTrue();
        handler.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task TryDispatchAsync_PersistsWorkBeforeDispatchAndRetainsOnlyPendingRouteWithBackoff() {
        DateTimeOffset now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        IProjectionDeliveryRetryScheduler scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        _ = scheduler.ScheduleAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<ProjectionDeliveryRetryWorkItem>(0));
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [
                new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null),
                new ProjectionDispatchOutcome("widget-index", ProjectionDispatchStatus.Indeterminate, null, ProjectionDispatchReasonCodes.HandlerFailure),
            ]));
        var handler = new ProjectionDispatchHttpMessageHandler(responseJson);
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail", "widget-index"),
            handler,
            out IProjectionDeliveryCheckpointStore checkpoints,
            out IProjectionLifecycleGateway lifecycle,
            scheduler,
            timeProvider);
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0);
        _ = checkpoints.SaveDeliveredSequenceAsync(Identity, Arg.Any<string>(), 2, Arg.Any<CancellationToken>()).Returns(true);
        _ = lifecycle.BeginDeliveryWriteAsync(Identity, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        _ = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1), Envelope(2)],
            [],
            CancellationToken.None);

        _ = await scheduler.Received(1).ScheduleAsync(
            Arg.Is<ProjectionDeliveryRetryWorkItem>(item =>
                item.HeadSequence == 2
                && item.HeadMessageId == "message-2"
                && item.DispatchId == "message-2"
                && item.PendingRoutes.SequenceEqual(new[] { "widget-detail", "widget-index" })),
            Arg.Any<CancellationToken>());
        scheduler.ReceivedCalls().First().GetMethodInfo().Name.ShouldBe(nameof(IProjectionDeliveryRetryScheduler.ScheduleAsync));
        _ = await scheduler.Received(1).TryUpdateAsync(
            Arg.Is<ProjectionDeliveryRetryWorkItem>(item =>
                item.PendingRoutes.SequenceEqual(new[] { "widget-index" })
                && item.TerminalRoutes.Count == 0
                && item.Attempt == 1
                && item.NextDueUtc == now.AddSeconds(1)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryDispatchAsync_TransportFailureRetainsPendingRoutesWithBackoff() {
        DateTimeOffset now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        IProjectionDeliveryRetryScheduler scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        _ = scheduler.ScheduleAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<ProjectionDeliveryRetryWorkItem>(0));
        var handler = new ProjectionDispatchHttpMessageHandler("{}", System.Net.HttpStatusCode.ServiceUnavailable);
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail"),
            handler,
            out IProjectionDeliveryCheckpointStore checkpoints,
            out IProjectionLifecycleGateway lifecycle,
            scheduler,
            timeProvider);
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(0);
        _ = lifecycle.BeginDeliveryWriteAsync(Identity, "widget-detail", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        _ = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1), Envelope(2)],
            [],
            CancellationToken.None);

        _ = await scheduler.Received(1).TryUpdateAsync(
            Arg.Is<ProjectionDeliveryRetryWorkItem>(item =>
                item.PendingRoutes.SequenceEqual(new[] { "widget-detail" })
                && item.Attempt == 1
                && item.NextDueUtc == now.AddSeconds(1)
                && item.LastReasonCode == ProjectionDispatchReasonCodes.PartialRetry),
            Arg.Any<CancellationToken>());
        _ = await scheduler.DidNotReceiveWithAnyArgs().TryDeleteAsync(default!, default);
    }

    [Fact]
    public async Task TryDispatchAsync_KnownTerminalOutcomeRemainsOperatorVisibleWithoutRetry() {
        DateTimeOffset now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        IProjectionDeliveryRetryScheduler scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        _ = scheduler.ScheduleAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<ProjectionDeliveryRetryWorkItem>(0));
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome(
                "widget-detail",
                ProjectionDispatchStatus.Failed,
                null,
                ProjectionDispatchReasonCodes.HandlerFailure)]));
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail"),
            new ProjectionDispatchHttpMessageHandler(responseJson),
            out IProjectionDeliveryCheckpointStore checkpoints,
            out IProjectionLifecycleGateway lifecycle,
            scheduler,
            new FakeTimeProvider(now));
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(0);
        _ = lifecycle.BeginDeliveryWriteAsync(Identity, "widget-detail", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        _ = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1), Envelope(2)],
            [],
            CancellationToken.None);

        _ = await scheduler.Received(1).TryUpdateAsync(
            Arg.Is<ProjectionDeliveryRetryWorkItem>(item =>
                item.PendingRoutes.Count == 0
                && item.TerminalRoutes.SequenceEqual(new[] { "widget-detail" })
                && item.LastReasonCode == ProjectionDispatchReasonCodes.HandlerFailure),
            Arg.Any<CancellationToken>());
        _ = await scheduler.DidNotReceiveWithAnyArgs().TryDeleteAsync(default!, default);
        _ = await checkpoints.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task TryDispatchAsync_ReconciledTerminalRouteDeletesDurableRetryWork() {
        IProjectionDeliveryRetryScheduler scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        _ = scheduler.ScheduleAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<ProjectionDeliveryRetryWorkItem>(0));
        string failed = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome(
                "widget-detail",
                ProjectionDispatchStatus.Failed,
                null,
                ProjectionDispatchReasonCodes.HandlerFailure)]));
        string reconciled = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null)]));
        var handler = new ProjectionDispatchHttpMessageHandler(
            failed,
            responsesByPath: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["/project/v2/reconcile"] = reconciled,
            });
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail"),
            handler,
            out IProjectionDeliveryCheckpointStore checkpoints,
            out IProjectionLifecycleGateway lifecycle,
            scheduler);
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(0);
        _ = lifecycle.BeginDeliveryWriteAsync(Identity, "widget-detail", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        _ = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1), Envelope(2)],
            [],
            CancellationToken.None);

        handler.CallCount.ShouldBe(2);
        _ = await scheduler.Received(1).TryDeleteAsync(
            Arg.Is<ProjectionDeliveryRetryWorkItem>(item =>
                item.PendingRoutes.SequenceEqual(new[] { "widget-detail" })),
            Arg.Any<CancellationToken>());
        _ = await scheduler.DidNotReceiveWithAnyArgs().TryUpdateAsync(default!, default);
        _ = await checkpoints.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task TryDispatchAsync_IdentityConflictCannotBeClearedByHandlerReconciliation() {
        IProjectionDeliveryRetryScheduler scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        _ = scheduler.ScheduleAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<ProjectionDeliveryRetryWorkItem>(0));
        string failed = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Failed, null,
                ProjectionDispatchReasonCodes.DeliveryIdentityConflict)]));
        string reconciled = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null)]));
        var handler = new ProjectionDispatchHttpMessageHandler(
            failed,
            responsesByPath: new Dictionary<string, string>(StringComparer.Ordinal) {
                ["/project/v2/reconcile"] = reconciled,
            });
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail"), handler,
            out IProjectionDeliveryCheckpointStore checkpoints,
            out _, scheduler);

        _ = await coordinator.TryDispatchAsync(
            Identity, Registration(), [Envelope(1)], [], CancellationToken.None);

        handler.CallCount.ShouldBe(1);
        _ = await scheduler.Received(1).TryUpdateAsync(
            Arg.Is<ProjectionDeliveryRetryWorkItem>(item =>
                item.PendingRoutes.Count == 0
                && item.TerminalRoutes.SequenceEqual(new[] { "widget-detail" })
                && item.LastReasonCode == ProjectionDispatchReasonCodes.DeliveryIdentityConflict),
            Arg.Any<CancellationToken>());
        _ = await scheduler.DidNotReceiveWithAnyArgs().TryDeleteAsync(default!, default);
        _ = await checkpoints.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task TryDispatchAsync_ExhaustedAttemptRemainsPendingAtBoundedBackoff() {
        DateTimeOffset now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        IProjectionDeliveryRetryScheduler scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        _ = scheduler.ScheduleAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<ProjectionDeliveryRetryWorkItem>(0) with { Attempt = 8 });
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome(
                "widget-detail",
                ProjectionDispatchStatus.Retryable,
                null,
                ProjectionDispatchReasonCodes.PartialRetry)]));
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail"),
            new ProjectionDispatchHttpMessageHandler(responseJson),
            out IProjectionDeliveryCheckpointStore checkpoints,
            out IProjectionLifecycleGateway lifecycle,
            scheduler,
            new FakeTimeProvider(now));
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(0);
        _ = lifecycle.BeginDeliveryWriteAsync(Identity, "widget-detail", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        _ = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1), Envelope(2)],
            [],
            CancellationToken.None);

        _ = await scheduler.Received(1).TryUpdateAsync(
            Arg.Is<ProjectionDeliveryRetryWorkItem>(item =>
                item.PendingRoutes.SequenceEqual(new[] { "widget-detail" })
                && item.Attempt == 8
                && item.NextDueUtc == now.AddSeconds(128)),
            Arg.Any<CancellationToken>());
        _ = await scheduler.DidNotReceiveWithAnyArgs().TryDeleteAsync(default!, default);
    }

    [Fact]
    public async Task TryDispatchAsync_StateBearingSuccessPreservesAdmissionWriteEtagCheckpointOrder() {
        JsonElement state = JsonSerializer.SerializeToElement(new { value = 42 });
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome(
                "widget-detail",
                ProjectionDispatchStatus.Completed,
                state,
                null)]));
        IActorProxyFactory actorProxyFactory = Substitute.For<IActorProxyFactory>();
        IProjectionWriteActor writeActor = Substitute.For<IProjectionWriteActor>();
        IETagActor eTagActor = Substitute.For<IETagActor>();
        _ = actorProxyFactory.CreateActorProxy<IProjectionWriteActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>())
            .Returns(writeActor);
        _ = actorProxyFactory.CreateActorProxy<IETagActor>(Arg.Any<Dapr.Actors.ActorId>(), Arg.Any<string>())
            .Returns(eTagActor);
        _ = eTagActor.RegenerateAsync().Returns("etag-1");
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail"),
            new ProjectionDispatchHttpMessageHandler(responseJson),
            out IProjectionDeliveryCheckpointStore checkpoints,
            out IProjectionLifecycleGateway lifecycle,
            actorProxyFactory: actorProxyFactory);
        _ = checkpoints.ReadDeliveredSequenceAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(0);
        _ = checkpoints.SaveDeliveredSequenceAsync(Identity, "widget-detail", 2, Arg.Any<CancellationToken>()).Returns(true);
        _ = lifecycle.BeginDeliveryWriteAsync(Identity, "widget-detail", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        _ = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1), Envelope(2)],
            [],
            CancellationToken.None);

        Received.InOrder(() => {
            _ = lifecycle.BeginDeliveryWriteAsync(Identity, "widget-detail", Arg.Any<string>(), Arg.Any<CancellationToken>());
            _ = writeActor.UpdateProjectionAsync(Arg.Any<ProjectionState>());
            _ = eTagActor.RegenerateAsync();
            _ = checkpoints.SaveDeliveredSequenceAsync(Identity, "widget-detail", 2, Arg.Any<CancellationToken>());
            _ = lifecycle.CompleteDeliveryWriteAsync(Identity, "widget-detail", Arg.Any<string>(), CancellationToken.None);
        });
    }

    [Fact]
    public async Task TryDispatchAsync_V2IdempotencyPartitionsRoutesAndCompletesOnlyReservedRoute() {
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome("widget-index", ProjectionDispatchStatus.Completed, null, null)]));
        IProjectionDeliveryIdempotencyCoordinator idempotency = Substitute.For<IProjectionDeliveryIdempotencyCoordinator>();
        ProjectionDeliveryReservation reservation = Reservation("message-1");
        _ = idempotency.TryAdmitAsync(
                Identity,
                "widget-detail",
                Arg.Any<IReadOnlyList<ProjectionEventDto>>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(new ProjectionDeliveryAdmissionResult(
                ProjectionDeliveryAdmissionDisposition.AlreadyCompleted,
                ProjectionDispatchReasonCodes.DeliveryAlreadyCompleted,
                null));
        _ = idempotency.TryAdmitAsync(
                Identity,
                "widget-index",
                Arg.Any<IReadOnlyList<ProjectionEventDto>>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(new ProjectionDeliveryAdmissionResult(
                ProjectionDeliveryAdmissionDisposition.Dispatch,
                null,
                reservation));
        _ = idempotency.CompleteAsync(
                Identity,
                "widget-index",
                Arg.Any<IReadOnlyList<ProjectionEventDto>>(),
                reservation,
                Arg.Any<CancellationToken>())
            .Returns(ProjectionDeliveryCompletion.Completed);
        var handler = new ProjectionDispatchHttpMessageHandler(responseJson);
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail", "widget-index"),
            handler,
            out IProjectionDeliveryCheckpointStore checkpoints,
            out IProjectionLifecycleGateway lifecycle,
            idempotencyCoordinator: idempotency);
        _ = lifecycle.BeginDeliveryWriteAsync(Identity, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        _ = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1)],
            [ProjectionEvent(1)],
            CancellationToken.None);

        handler.CallCount.ShouldBe(1);
        ProjectionDispatchRequest request = JsonSerializer.Deserialize<ProjectionDispatchRequest>(
            handler.RequestJson!,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldNotBeNull();
        request.ProjectionTypes.ShouldBe(["widget-index"]);
        _ = await idempotency.Received(1).CompleteAsync(
            Identity,
            "widget-index",
            Arg.Any<IReadOnlyList<ProjectionEventDto>>(),
            reservation,
            Arg.Any<CancellationToken>());
        _ = await checkpoints.DidNotReceiveWithAnyArgs().ReadDeliveredSequenceAsync(default!, default!, default);
        _ = await checkpoints.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default!, default, default);
    }

    [Fact]
    public async Task TryDispatchAsync_CompletionStateUnavailable_RetainsRouteAndReservationFenceForRetry() {
        DateTimeOffset now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        IProjectionDeliveryRetryScheduler scheduler = Substitute.For<IProjectionDeliveryRetryScheduler>();
        _ = scheduler.ScheduleAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<ProjectionDeliveryRetryWorkItem>(0));
        IProjectionDeliveryIdempotencyCoordinator idempotency = Substitute.For<IProjectionDeliveryIdempotencyCoordinator>();
        ProjectionDeliveryReservation reservation = Reservation("message-1") with { FencingToken = 7 };
        _ = idempotency.TryAdmitAsync(
                Identity,
                "widget-detail",
                Arg.Any<IReadOnlyList<ProjectionEventDto>>(),
                true,
                Arg.Any<CancellationToken>())
            .Returns(new ProjectionDeliveryAdmissionResult(
                ProjectionDeliveryAdmissionDisposition.Dispatch,
                null,
                reservation));
        _ = idempotency.CompleteAsync(
                Identity,
                "widget-detail",
                Arg.Any<IReadOnlyList<ProjectionEventDto>>(),
                reservation,
                Arg.Any<CancellationToken>())
            .Returns(ProjectionDeliveryCompletion.StateUnavailable);
        string responseJson = JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null)]));
        NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
            Snapshot("fingerprint", "widget-detail"),
            new ProjectionDispatchHttpMessageHandler(responseJson),
            out _,
            out IProjectionLifecycleGateway lifecycle,
            scheduler,
            new FakeTimeProvider(now),
            idempotencyCoordinator: idempotency);
        _ = lifecycle.BeginDeliveryWriteAsync(Identity, "widget-detail", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        _ = await coordinator.TryDispatchAsync(
            Identity,
            Registration(),
            [Envelope(1)],
            [ProjectionEvent(1)],
            CancellationToken.None);

        _ = await scheduler.Received(1).TryUpdateAsync(
            Arg.Is<ProjectionDeliveryRetryWorkItem>(item =>
                item.PendingRoutes.SequenceEqual(new[] { "widget-detail" })
                && item.ReservationFencingTokens.Count == 1
                && item.ReservationFencingTokens["widget-detail"] == 7
                && item.Attempt == 1
                && item.NextDueUtc == now.AddSeconds(1)),
            Arg.Any<CancellationToken>());
        _ = await scheduler.DidNotReceiveWithAnyArgs().TryDeleteAsync(default!, default);
    }

    [Fact]
    public async Task TryDispatchAsync_InvalidOutcomeSetsFailClosedWithoutAdvancingCheckpoint() {
        ProjectionDispatchResponse[] invalidResponses = [
            new ProjectionDispatchResponse(
                ProjectionDispatchProtocol.Version,
                [
                    new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null),
                    new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null),
                ]),
            new ProjectionDispatchResponse(
                ProjectionDispatchProtocol.Version,
                [new ProjectionDispatchOutcome("widget-detail", (ProjectionDispatchStatus)999, null, null)]),
            new ProjectionDispatchResponse(
                ProjectionDispatchProtocol.Version,
                [new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Retryable, null, new string('r', 129))]),
            new ProjectionDispatchResponse(
                ProjectionDispatchProtocol.Version,
                [.. Enumerable.Range(0, 33).Select(_ =>
                    new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null))]),
        ];

        foreach (ProjectionDispatchResponse invalidResponse in invalidResponses) {
            NamedProjectionDispatchCoordinator coordinator = CreateCoordinator(
                Snapshot("fingerprint", "widget-detail"),
                new ProjectionDispatchHttpMessageHandler(JsonSerializer.Serialize(invalidResponse)),
                out IProjectionDeliveryCheckpointStore checkpoints,
                out IProjectionLifecycleGateway lifecycle);
            _ = checkpoints.ReadDeliveredSequenceAsync(Identity, "widget-detail", Arg.Any<CancellationToken>()).Returns(0);
            _ = lifecycle.BeginDeliveryWriteAsync(Identity, "widget-detail", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

            _ = await coordinator.TryDispatchAsync(
                Identity,
                Registration(),
                [Envelope(1)],
                [],
                CancellationToken.None);

            _ = await checkpoints.DidNotReceiveWithAnyArgs().SaveDeliveredSequenceAsync(default!, default!, default, default);
        }
    }

    private static NamedProjectionDispatchCoordinator CreateCoordinator(
        NamedProjectionRouteCatalogSnapshot snapshot,
        HttpMessageHandler handler,
        out IProjectionDeliveryCheckpointStore checkpoints,
        out IProjectionLifecycleGateway lifecycle,
        IProjectionDeliveryRetryScheduler? scheduler = null,
        TimeProvider? timeProvider = null,
        IActorProxyFactory? actorProxyFactory = null,
        IProjectionDeliveryIdempotencyCoordinator? idempotencyCoordinator = null,
        string? aggregateActorTypeName = null,
        IProjectionDeliveryHistoryReader? historyReader = null) {
        var catalog = new NamedProjectionRouteCatalog();
        catalog.Replace(snapshot);
        checkpoints = Substitute.For<IProjectionDeliveryCheckpointStore>();
        lifecycle = Substitute.For<IProjectionLifecycleGateway>();
        _ = lifecycle.BeginRebuildAsync(
                Arg.Any<AggregateIdentity>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        _ = lifecycle.BeginDeliveryWriteAsync(
                Arg.Any<AggregateIdentity>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        _ = lifecycle.CompleteDeliveryWriteAsync(
                Arg.Any<AggregateIdentity>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));
        if (scheduler is not null && scheduler is not MemoryRetryScheduler) {
            _ = scheduler.TryAcquireAsync(
                    Arg.Any<ProjectionDeliveryRetryWorkItem>(),
                    Arg.Any<string>(),
                    Arg.Any<DateTimeOffset>(),
                    Arg.Any<TimeSpan>(),
                    Arg.Any<CancellationToken>())
                .Returns(call => {
                    ProjectionDeliveryRetryWorkItem item = call.ArgAt<ProjectionDeliveryRetryWorkItem>(0);
                    return item with {
                        Revision = item.Revision + 1,
                        LeaseOwner = call.ArgAt<string>(1),
                        LeaseExpiresUtc = call.ArgAt<DateTimeOffset>(2) + call.ArgAt<TimeSpan>(3),
                    };
                });
            _ = scheduler.TryUpdateAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>())
                .Returns(true);
            _ = scheduler.TryDeleteAsync(Arg.Any<ProjectionDeliveryRetryWorkItem>(), Arg.Any<CancellationToken>())
                .Returns(true);
        }

        return new NamedProjectionDispatchCoordinator(
            catalog,
            checkpoints,
            lifecycle,
            actorProxyFactory ?? Substitute.For<IActorProxyFactory>(),
            new DaprClientBuilder().Build(),
            httpClientFactory,
            Options.Create(new ProjectionDispatchOptions()),
            NullLogger<NamedProjectionDispatchCoordinator>.Instance,
            scheduler,
            timeProvider,
            idempotencyCoordinator: idempotencyCoordinator,
            historyReader: historyReader,
            actorOptions: Options.Create(new EventStoreActorOptions {
                AggregateActorTypeName = aggregateActorTypeName ?? nameof(AggregateActor)
            }));
    }

    private static NamedProjectionRouteCatalogSnapshot Snapshot(string fingerprint, params string[] projectionTypes)
        => new([
            new NamedProjectionRouteCatalogEntry(
                "widget-service",
                "v1",
                "widget",
                ProjectionDispatchProtocol.Version,
                ProjectionDispatchProtocol.Capability,
                fingerprint,
                projectionTypes),
        ]);

    private static DomainServiceRegistration Registration()
        => new("widget-service", "process", "tenant-a", "widget", "v1");

    private static EventEnvelope Envelope(long sequence)
        => new(
            $"message-{sequence}",
            "widget-1",
            "widget",
            "tenant-a",
            "widget",
            sequence,
            sequence,
            DateTimeOffset.UnixEpoch,
            "correlation",
            "causation",
            "user",
            "v1",
            "widget-updated",
            1,
            "json",
            [],
            null);

    private static ProjectionEventDto ProjectionEvent(long sequence) => new(
        "widget-updated",
        [],
        "json",
        sequence,
        DateTimeOffset.UnixEpoch,
        "correlation",
        $"message-{sequence}",
        "user");

    private static ProjectionDeliveryReservation Reservation(string messageId) => new(
        1,
        1,
        messageId,
        messageId,
        "v1:manifest",
        1,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch.AddMinutes(5),
        1);

    private sealed class FixedPagedHistoryReader(ProjectionEventDto[] events) : IProjectionDeliveryHistoryReader {
        public int FullReadCalls { get; private set; }
        public int SuffixEventsRead { get; private set; }

        public Task<IReadOnlyList<ProjectionEventDto>> ReadAsync(AggregateIdentity identity,
            long throughSequence, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Mapped delivery must not materialize complete history.");

        public async IAsyncEnumerable<ProjectionEventDto[]> ReadPagesAsync(AggregateIdentity identity,
            long throughSequence, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            FullReadCalls++;
            foreach (ProjectionEventDto item in events.Where(item => item.SequenceNumber <= throughSequence)) {
                cancellationToken.ThrowIfCancellationRequested();
                yield return [item];
                await Task.Yield();
            }
        }

        public async IAsyncEnumerable<ProjectionEventDto[]> ReadPagesFromAsync(AggregateIdentity identity,
            long afterSequence, long throughSequence,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            foreach (ProjectionEventDto item in events.Where(item => item.SequenceNumber > afterSequence
                && item.SequenceNumber <= throughSequence)) {
                cancellationToken.ThrowIfCancellationRequested();
                SuffixEventsRead++;
                yield return [item];
                await Task.Yield();
            }
        }
    }

    private sealed class SuffixOnlyPagedHistoryReader(ProjectionEventDto head) : IProjectionDeliveryHistoryReader {
        public int FullReads { get; private set; }
        public int SuffixReads { get; private set; }

        public Task<IReadOnlyList<ProjectionEventDto>> ReadAsync(AggregateIdentity identity,
            long throughSequence, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Mapped delivery must not materialize complete history.");

        public async IAsyncEnumerable<ProjectionEventDto[]> ReadPagesAsync(AggregateIdentity identity,
            long throughSequence, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            FullReads++;
            throw new InvalidOperationException("Accepted suffix delivery must not scan the full source.");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }

        public async IAsyncEnumerable<ProjectionEventDto[]> ReadPagesFromAsync(AggregateIdentity identity,
            long afterSequence, long throughSequence,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            SuffixReads++;
            afterSequence.ShouldBe(head.SequenceNumber - 1);
            throughSequence.ShouldBe(head.SequenceNumber);
            cancellationToken.ThrowIfCancellationRequested();
            yield return [head];
            await Task.Yield();
        }
    }

    private sealed class IncrementalMappedHttpHandler : HttpMessageHandler {
        public int IncrementalCalls { get; private set; }
        public int StagedCalls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) {
            string path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/project/v2", StringComparison.Ordinal)) {
                IncrementalCalls++;
                ProjectionDispatchRequest parsed = JsonSerializer.Deserialize<ProjectionDispatchRequest>(
                    await request.Content!.ReadAsByteArrayAsync(cancellationToken),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldNotBeNull();
                parsed.ProjectionTypes.ShouldBe(["widget-detail", "widget-index", "widget-token-index"]);
                parsed.Request.Events.ShouldHaveSingleItem().SequenceNumber.ShouldBe(10_000);
                return Reply(new ProjectionDispatchResponse(ProjectionDispatchProtocol.Version,
                    [.. parsed.ProjectionTypes.Select(type => new ProjectionDispatchOutcome(type,
                        ProjectionDispatchStatus.Completed, null, null))]));
            }
            StagedCalls++;
            throw new InvalidOperationException("Accepted suffix delivery must not stage a replay.");
        }

        private static HttpResponseMessage Reply(ProjectionDispatchResponse response)
            => new(HttpStatusCode.OK) {
                Content = new StringContent(JsonSerializer.Serialize(response), Encoding.UTF8, "application/json"),
            };
    }

    private sealed class IncrementalFallbackHttpHandler : HttpMessageHandler {
        public int SharedDeliveries { get; private set; }
        public int CommitCalls { get; private set; }
        public int StageCalls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) {
            string path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/project/v2", StringComparison.Ordinal)) {
                ProjectionDispatchRequest parsed = JsonSerializer.Deserialize<ProjectionDispatchRequest>(
                    await request.Content!.ReadAsByteArrayAsync(cancellationToken),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldNotBeNull();
                parsed.Request.Events.ShouldHaveSingleItem().SequenceNumber.ShouldBe(3);
                SharedDeliveries += parsed.ProjectionTypes.Count(type => type != "widget-detail");
                return Reply(new ProjectionDispatchResponse(ProjectionDispatchProtocol.Version,
                    [.. parsed.ProjectionTypes.Select(type => new ProjectionDispatchOutcome(type,
                        type == "widget-detail" ? ProjectionDispatchStatus.Retryable : ProjectionDispatchStatus.Completed,
                        null, type == "widget-detail" ? ProjectionDispatchReasonCodes.DeliveryRebuildRequired : null))]));
            }
            if (path.EndsWith("/project/rebuild/part/v1", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                };
            if (path.EndsWith("/project/rebuild/stage/v1", StringComparison.Ordinal)) StageCalls++;
            if (path.EndsWith("/project/rebuild/commit/v1", StringComparison.Ordinal)) {
                CommitCalls++;
                if (CommitCalls == 1) return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                };
            }
            if (path.EndsWith("/project/rebuild/verify/v1", StringComparison.Ordinal)
                && CommitCalls == 1)
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                };
            return Reply(new ProjectionDispatchResponse(ProjectionDispatchProtocol.Version,
                [new ProjectionDispatchOutcome("widget-detail", ProjectionDispatchStatus.Completed, null, null)]));
        }

        private static HttpResponseMessage Reply(ProjectionDispatchResponse response)
            => new(HttpStatusCode.OK) {
                Content = new StringContent(JsonSerializer.Serialize(response), Encoding.UTF8, "application/json"),
            };
    }

    private sealed class MemoryRetryScheduler : IProjectionDeliveryRetryScheduler {
        public ProjectionDeliveryRetryWorkItem? Work { get; private set; }

        public Task<ProjectionDeliveryRetryWorkItem> ScheduleAsync(ProjectionDeliveryRetryWorkItem workItem,
            CancellationToken cancellationToken = default) {
            Work ??= workItem;
            return Task.FromResult(Work);
        }

        public Task<ProjectionDeliveryRetryWorkItem?> TryAcquireAsync(ProjectionDeliveryRetryWorkItem workItem,
            string leaseOwner, DateTimeOffset nowUtc, TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) {
            Work = Work is null ? null : Work with { Revision = Work.Revision + 1,
                LeaseOwner = leaseOwner, LeaseExpiresUtc = nowUtc + leaseDuration };
            return Task.FromResult(Work);
        }

        public Task<bool> TryUpdateAsync(ProjectionDeliveryRetryWorkItem workItem,
            CancellationToken cancellationToken = default) {
            Work = workItem;
            return Task.FromResult(true);
        }

        public Task<bool> TryDeleteAsync(ProjectionDeliveryRetryWorkItem workItem,
            CancellationToken cancellationToken = default) {
            Work = null;
            return Task.FromResult(true);
        }

        public Task UpdateAsync(ProjectionDeliveryRetryWorkItem workItem,
            CancellationToken cancellationToken = default) {
            Work = workItem;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string workId, CancellationToken cancellationToken = default) {
            Work = null;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ProjectionDeliveryRetryWorkItem>> GetDueAsync(DateTimeOffset dueUtc,
            int maximumCount, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProjectionDeliveryRetryWorkItem>>(
                Work is null ? [] : [Work]);
    }

    private sealed class MappedOrdinaryHttpHandler(bool failFirstCommit = true,
        bool enableIncremental = false) : HttpMessageHandler {
        public int SharedCalls { get; private set; }
        public int IncrementalCalls { get; private set; }
        public int CommitCalls { get; private set; }
        public int PartCalls { get; private set; }
        public int StageHeaders { get; private set; }
        public int LargestPartBodyBytes { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) {
            string path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/project/rebuild/part/v1", StringComparison.Ordinal)) {
                PartCalls++;
                byte[] body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
                LargestPartBodyBytes = Math.Max(LargestPartBodyBytes, body.Length);
                return Reply("{}");
            }
            if (path.EndsWith("/project/v2", StringComparison.Ordinal)) {
                SharedCalls++;
                byte[] body = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
                ProjectionDispatchRequest parsed = JsonSerializer.Deserialize<ProjectionDispatchRequest>(body,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldNotBeNull();
                if (enableIncremental && parsed.ProjectionTypes.Contains("widget-detail", StringComparer.Ordinal)) {
                    parsed.ProjectionTypes.ShouldBe(["widget-detail", "widget-index", "widget-token-index"]);
                    IncrementalCalls++;
                }
                else parsed.ProjectionTypes.ShouldBe(["widget-index", "widget-token-index"]);
                parsed.Request.Events.Length.ShouldBe(1);
                parsed.Request.Events[0].SequenceNumber.ShouldBe(SharedCalls);
                return Reply(JsonSerializer.Serialize(new ProjectionDispatchResponse(
                    ProjectionDispatchProtocol.Version,
                    [.. parsed.ProjectionTypes.Select(type => new ProjectionDispatchOutcome(type,
                        ProjectionDispatchStatus.Completed, null, null))])));
            }
            if (path.EndsWith("/project/rebuild/stage/v1", StringComparison.Ordinal)) {
                StageHeaders++;
                ProjectionDispatchRequest parsed = JsonSerializer.Deserialize<ProjectionDispatchRequest>(
                    await request.Content!.ReadAsByteArrayAsync(cancellationToken),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldNotBeNull();
                parsed.ProjectionTypes.ShouldBe(["widget-detail"]);
                parsed.Request.Events.ShouldBeEmpty();
                parsed.RebuildTransport.ShouldNotBeNull().PartCount.ShouldBeGreaterThan(0);
            }
            if (path.EndsWith("/project/rebuild/commit/v1", StringComparison.Ordinal)) {
                CommitCalls++;
                if (failFirstCommit && CommitCalls == 1)
                    return Reply("{}", HttpStatusCode.ServiceUnavailable);
            }
            if (path.EndsWith("/project/rebuild/verify/v1", StringComparison.Ordinal)
                && failFirstCommit && CommitCalls == 1)
                return Reply("{}", HttpStatusCode.ServiceUnavailable);
            return Reply(Outcome("widget-detail"));
        }

        private static string Outcome(string route) => JsonSerializer.Serialize(new ProjectionDispatchResponse(
            ProjectionDispatchProtocol.Version,
            [new ProjectionDispatchOutcome(route, ProjectionDispatchStatus.Completed, null, null)]));

        private static HttpResponseMessage Reply(string json, HttpStatusCode status = HttpStatusCode.OK)
            => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
