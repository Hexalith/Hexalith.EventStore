using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Dapr.Client;

using Grpc.Net.Client;

using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Client.Tests.Projections;
using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Contracts.Projections;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Events;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class ProjectionProofWorkingMemoryTests
{
    [Theory]
    [InlineData(26, 256)]
    [InlineData(36, 256)]
    [InlineData(26, 4096)]
    [InlineData(36, 4096)]
    public async Task GuardCompletionRetainedProofAndActualSdkSendRootsFitApprovedEnvelope(int idLength, int receiptLimit)
    {
        const int head = 10_000;
        var identity = new AggregateIdentity("tenant-a", "counter", "counter-1");
        const string route = "counter-detail";
        string initial = ProjectionDeliveryFingerprint.ComputeInitial(identity, route);
        var table = new ProjectionSourceAdmittedDigests(0, head, false, initial);
        string prefix = initial;
        for (int index = 1; index <= head; index++)
        {
            string id = index.ToString("D" + idLength, System.Globalization.CultureInfo.InvariantCulture);
            var item = new ProjectionEventDto("counter-incremented", "{}"u8.ToArray(), "json", index,
                DateTimeOffset.UnixEpoch, "correlation", id, "user");
            string fingerprint = ProjectionDeliveryFingerprint.ComputeEvent(item);
            prefix = ProjectionDeliveryFingerprint.Extend(prefix, fingerprint);
            table.Add(index, id, new string('a', 64), fingerprint, prefix);
        }
        table.Seal();
        var history = new ProjectionDeliveryFingerprintHistory(initial, prefix, table.RouteEvents);
        using var backend = new RecordingDaprClient { PersistTypedState = true };
        var options = Options.Create(new ProjectionDeliveryIdempotencyOptions { CompletedReceiptLimit = receiptLimit });
        ProjectionDeliveryState? latestRead = null, underSave = null;
        ProjectionDeliveryStateReadResult? latestReadResult = null;
        List<ProjectionDeliveryReceipt>? receiptWork = null;
        ProjectionDeliveryEventDigest? headWork = null;
        long observed = 0, serialized = 0, transport = 0, serializing = 0;
        byte[]? serializedCarrier = null;
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(type =>
        {
            if (type.Type != typeof(ProjectionDeliveryState)) return;
            type.Properties.Single(property => property.Name == "completedMessageIdDigests").CustomConverter =
                new ObservedProjectionMessageIdDigestsConverter(writer =>
                {
                    if (underSave?.LastDeliveredSequence == head)
                        serializing = Math.Max(serializing, ProjectionRetainedGraphSize.Measure(history, latestReadResult,
                            underSave, receiptWork, headWork, writer));
                });
        });
        backend.JsonSerializerOptions.TypeInfoResolver = resolver;
        var owning = new DaprProjectionDeliveryStateStore(backend, Options.Create(new ProjectionOptions()), options);
        (await owning.TryActivateWriterProtocolAsync(new(1, 2, new string('b', 40), DateTimeOffset.UtcNow)).ConfigureAwait(true)).ShouldBeTrue();
        // Populate the real guarded schema and pending transition without running
        // ten thousand model batches. These bounded fixture references represent
        // already verified source/owner observations, not production activation.
        SourceProof seed = SourceProofCodec.CreateSeed(identity, "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        SourceProof source = seed with { HeadSequence = head, HeadMessageId = table.RouteEvents[^1].MessageId,
            CumulativeDigest = new string('c', 64) };
        string catalog = new('d', 64);
        string commit = new('b', 40);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var marker = new ProjectionSourceGuard(1, 1, 2, 2, 1, identity.TenantId, identity.Domain,
            identity.AggregateId, route, seed.SourceIncarnation, catalog, "single-event-v1",
            ProjectionSourceGuard.ApprovedSpecSha256, commit, now);
        var maintenance = new ProjectionSourceGuardMaintenance("fixture-operator", commit, "fixture-backup",
            ProjectionSourceGuard.ApprovedSpecSha256, true, true, true,
            [.. new[] { "append", "immediate", "poller", "retry", "reconciliation", "rebuild", "erasure" }
                .Select(role => new ProjectionSourceGuardWriterInventory(role, commit, "fixture-package", true))]);
        var work = new ProjectionDeliveryReconciliationWork(identity.TenantId, identity.Domain,
            identity.AggregateId, route, "guard_activation_required", 0, 2, maintenance.OperatorId, now)
            { SourceGuardMaintenance = maintenance, QualifiedSourceGuard = marker };
        ProjectionDeliveryState empty = ProjectionDeliveryState.CreateEmpty(identity, route, initial, now)
            with { SchemaVersion = 2, GuardEvidenceVersion = 1 };
        (await owning.TryActivateSourceGuardAsync(identity, route, empty, marker, work, "", "").ConfigureAwait(true)).ShouldBeTrue();
        var binding = new ProjectionDeliveryGuardReservation(seed, source, catalog, "single-event-v1", "fixture-generation", null);
        var owner = new ProjectionOwnerProof(new(1, identity.TenantId, identity.Domain, identity.AggregateId,
            route, "fixture-owner", binding.SelectedOwnerGeneration, head, source.HeadMessageId!,
            source.HeadMessageId!, "fixture-freshness"), "fixture-receipt", ReadModelBatchFingerprint.ComputeFromManifest("{}"u8));
        IProjectionDeliveryStateStore states = Substitute.For<IProjectionDeliveryStateStore>();
        states.ReadAsync(identity, route, Arg.Any<CancellationToken>()).Returns(async call =>
        {
            ProjectionDeliveryStateReadResult read = await owning.ReadAsync(identity, route, call.ArgAt<CancellationToken>(2)).ConfigureAwait(false);
            latestReadResult = read;
            latestRead = read.State;
            return read;
        });
        states.TrySaveAsync(identity, route, Arg.Any<ProjectionDeliveryState>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            underSave = call.ArgAt<ProjectionDeliveryState>(2);
            if (underSave.LastDeliveredSequence == head)
            {
                // Conservatively retain the completion loop's bounded working
                // list and materialized head in addition to its compacted row.
                receiptWork = [.. underSave.CompletedReceipts!];
                headWork = history.Events[^1];
            }
            return await owning.TrySaveAsync(identity, route, underSave, call.ArgAt<string>(3), call.ArgAt<CancellationToken>(4)).ConfigureAwait(false);
        });
        backend.BeforeTrySaveByteState = (key, value) =>
        {
            if (underSave?.LastDeliveredSequence != head || key != ProjectionDeliveryStateKeys.GetStateKey(identity, route)) return;
            MemoryMarshal.TryGetArray(value, out ArraySegment<byte> carrier).ShouldBeTrue();
            serialized = value.Length;
            serializedCarrier = carrier.Array;
            observed = ProjectionRetainedGraphSize.Measure(history, latestReadResult, underSave,
                receiptWork, headWork, carrier.Array);
        };
        var coordinator = new ProjectionDeliveryIdempotencyCoordinator(states, options, TimeProvider.System);
        ProjectionDeliveryReservation reservation = (await coordinator.TryAdmitHistoryAsync(identity, route, history, true, guardBinding: binding)
            .ConfigureAwait(true)).Reservation.ShouldNotBeNull();
        (await coordinator.PrepareIdleBindingAsync(identity, route, reservation, "fixture-lifecycle-token", owner).ConfigureAwait(true)).ShouldBeTrue();
        (await coordinator.CompleteHistoryAsync(identity, route, history, reservation,
            guardEvidence: new(source, catalog, "single-event-v1", owner, 7)).ConfigureAwait(true)).ShouldBe(ProjectionDeliveryCompletion.Completed);
        latestRead!.SchemaVersion.ShouldBe(2);
        latestRead.IdleBindingPending.ShouldNotBeNull();
        underSave!.SchemaVersion.ShouldBe(2);
        underSave.GuardEvidence.ShouldNotBeNull().IdleLifecycleRevision.ShouldBe(7);
        long validating = MeasureValidationRoots(history, latestReadResult!, underSave, receiptWork!, headWork!, serializedCarrier!);
        int sendArrayBytes = 0;
        using var handler = new ProofGrpcSendHandler((payload, memory) =>
        {
            MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> rented).ShouldBeTrue();
            sendArrayBytes = Math.Max(sendArrayBytes, rented.Array!.Length);
            // Retain one exact row carrier conservatively even if an async caller
            // has already stopped retaining its original before this send begins.
            transport = Math.Max(transport, ProjectionRetainedGraphSize.Measure(history, latestReadResult, underSave,
                receiptWork, headWork, serializedCarrier, payload, rented.Array));
        });
        using DaprClient actualClient = new DaprClientBuilder().UseGrpcEndpoint("http://127.0.0.1:1")
            .UseGrpcChannelOptions(new GrpcChannelOptions { HttpHandler = handler }).Build();
        var actualStore = new DaprProjectionDeliveryStateStore(actualClient, Options.Create(new ProjectionOptions()), options);
        (await actualStore.TrySaveAsync(identity, route, underSave!, "observed-etag").ConfigureAwait(true)).ShouldBeTrue();
        long ceiling = 4L * (16_384L + 80L * head + 512L * Math.Min(head, receiptLimit));
        TestContext.Current.TestOutputHelper?.WriteLine("ProjectionProofWorkingRoots " + JsonSerializer.Serialize(new
        { head, checkpoint = 0, idLength, receiptLimit, serialized, retainedRootBytes = observed,
            validationRootBytes = validating, serializerRootBytes = serializing, transportRootBytes = transport,
            sendArrayBytes, ceiling,
            coverage = "schema2 pending/current/completed proof roots, actual validation set, active serializer and installed Dapr ByteString/gRPC send array by phase; event/provider phases remain separate" }));
        observed.ShouldBeGreaterThan(0);
        observed.ShouldBeLessThanOrEqualTo(ceiling);
        validating.ShouldBeGreaterThan(observed);
        validating.ShouldBeLessThanOrEqualTo(ceiling);
        serializing.ShouldBeGreaterThan(0);
        serializing.ShouldBeLessThanOrEqualTo(ceiling);
        ((long)sendArrayBytes).ShouldBeGreaterThan(serialized);
        transport.ShouldBeGreaterThan(observed);
        transport.ShouldBeLessThanOrEqualTo(ceiling);
        ProjectionDeliveryStateReadResult completedRead = await owning.ReadAsync(identity, route).ConfigureAwait(true);
        HashSet<ProjectionMessageIdDigest> readbackSet = ProjectionMessageIdDigests.CreateSet(
            completedRead.State!.CompletedMessageIdDigests!).ShouldNotBeNull();
        // Include the old completion frame's whole row, the separately decoded
        // completed readback row, and both exact read carriers. The installed
        // Dapr client may still retain its protobuf ByteString during an inline
        // continuation; the returned read carrier is a distinct array.
        Google.Protobuf.ByteString readResponse = Google.Protobuf.ByteString.CopyFrom(serializedCarrier!);
        long readbackBytes = ProjectionRetainedGraphSize.Measure(history, latestReadResult, underSave,
            receiptWork, headWork, completedRead, readbackSet, serializedCarrier, readResponse);
        TestContext.Current.TestOutputHelper?.WriteLine("ProjectionProofReadbackWorkingRoots " + JsonSerializer.Serialize(new
        { head, checkpoint = 0, idLength, receiptLimit, readbackRootBytes = readbackBytes, ceiling,
            coverage = "retained admission/pending and old completed frame plus separate completed readback/validation set and exact carrier/protobuf ByteString" }));
        readbackBytes.ShouldBeLessThanOrEqualTo(ceiling);
    }

    private static long MeasureValidationRoots(ProjectionDeliveryFingerprintHistory history,
        ProjectionDeliveryStateReadResult current, ProjectionDeliveryState completed,
        List<ProjectionDeliveryReceipt> receipts, ProjectionDeliveryEventDigest head, byte[] carrier)
    {
        HashSet<ProjectionMessageIdDigest> validated = ProjectionMessageIdDigests.CreateSet(completed.CompletedMessageIdDigests!).ShouldNotBeNull();
        validated.Count.ShouldBe(history.Events.Count);
        long binary = ProjectionRetainedGraphSize.Measure(history, current, completed, receipts, head, carrier, validated);
        // The raw-ID admission/history sets retain ordinal strings, unlike the
        // complete hashed-ID membership set. Reproduce their actual population
        // and growth with the immutable table, conservatively beside both rows
        // and a row carrier even though validation precedes the final save.
        var supplied = new HashSet<string>(StringComparer.Ordinal);
        foreach (ProjectionDeliveryEventDigest item in history.Events) supplied.Add(item.MessageId).ShouldBeTrue();
        return Math.Max(binary, ProjectionRetainedGraphSize.Measure(history, current, completed, receipts, head, carrier, supplied));
    }
}
