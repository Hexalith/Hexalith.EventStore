using System.Text.Json;

using Dapr.Client;

using Hexalith.EventStore.Contracts.Identity;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Server.Configuration;
using Hexalith.EventStore.Server.Projections;

using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public partial class ProjectionDeliveryStateContractTests {
    [Fact]
    public void SourceGuardKeyAndStrictJsonArePinnedWithoutChangingWriterMarkerBytes()
    {
        ProjectionDeliveryStateKeys.GetSourceGuardKey(Identity, "order-detail")
            .ShouldBe("projection-delivery-source-guard:v1:8652ab6ecd743d066320b60cfb7cfcef180800d28cf3f2d55dc948f282ccb419");
        var marker = new ProjectionSourceGuard(1, 1, 2, 2, 1, Identity.TenantId, Identity.Domain,
            Identity.AggregateId, "order-detail", "01ARZ3NDEKTSV4RRFFQ69G5FAV", new string('a', 64),
            "single-event-v1", ProjectionSourceGuard.ApprovedSpecSha256, new string('b', 40), DateTimeOffset.UnixEpoch);
        marker.IsValid(Identity, "order-detail").ShouldBeTrue();
        string json = JsonSerializer.Serialize(marker, JsonSerializerOptions.Web);
        using JsonDocument valid = JsonDocument.Parse(json);
        ProjectionSourceGuard.Parse(valid.RootElement, JsonSerializerOptions.Web).ShouldBe(marker);
        using JsonDocument duplicate = JsonDocument.Parse(json.Replace("{", "{\"guardVersion\":1,", StringComparison.Ordinal));
        Should.Throw<JsonException>(() => ProjectionSourceGuard.Parse(duplicate.RootElement, JsonSerializerOptions.Web));
        using JsonDocument unknown = JsonDocument.Parse(json.Replace("{", "{\"wildcard\":true,", StringComparison.Ordinal));
        Should.Throw<JsonException>(() => ProjectionSourceGuard.Parse(unknown.RootElement, JsonSerializerOptions.Web));
        (marker with { GuardVersion = 2 }).IsValid(Identity, "order-detail").ShouldBeFalse();
        (marker with { SpecSha256 = new string('c', 64) }).IsValid(Identity, "order-detail").ShouldBeFalse();
    }

    private static readonly AggregateIdentity Identity = new("tenant-a", "sales", "order-42");

    [Fact]
    public void VersionedRow_PreservesReleasedCheckpointJsonPropertyNames() {
        ProjectionDeliveryState state = ProjectionDeliveryState.CreateEmpty(
            Identity,
            "order-detail",
            "v1:initial",
            new DateTimeOffset(2026, 7, 14, 10, 0, 0, TimeSpan.Zero));

        string json = JsonSerializer.Serialize(state);
        ProjectionCheckpoint legacy = JsonSerializer.Deserialize<ProjectionCheckpoint>(json).ShouldNotBeNull();

        legacy.TenantId.ShouldBe("tenant-a");
        legacy.Domain.ShouldBe("sales");
        legacy.AggregateId.ShouldBe("order-42");
        legacy.LastDeliveredSequence.ShouldBe(0);
        json.ShouldContain("\"WriterProtocolVersion\":2");
    }

    [Fact]
    public void DeliveryAndReconciliationSerialization_ContainNoEventPayloadOrOperationalSecrets() {
        ProjectionDeliveryState state = ProjectionDeliveryState.CreateEmpty(
            Identity,
            "order-detail",
            "v1:initial-digest",
            new DateTimeOffset(2026, 7, 14, 10, 0, 0, TimeSpan.Zero)) with {
            CompletedReceipts = [new ProjectionDeliveryReceipt(1, "message-1", "v1:event", "v1:prefix")],
            FirstRetainedSequence = 1,
            LastDeliveredSequence = 1,
            LastCompletedMessageId = "message-1",
            CompletedPrefixFingerprint = "v1:prefix",
        };
        var work = new ProjectionDeliveryReconciliationWork(
            Identity.TenantId,
            Identity.Domain,
            Identity.AggregateId,
            "order-detail",
            "bounded_reason",
            1,
            1,
            "operator-7",
            state.UpdatedAt);

        string serialized = JsonSerializer.Serialize(new { state, work });

        serialized.ShouldNotContain("payload", Case.Insensitive);
        serialized.ShouldNotContain("etag", Case.Insensitive);
        serialized.ShouldNotContain("stateKey", Case.Insensitive);
        JsonSerializer.Serialize(work).ShouldNotContain("fingerprint", Case.Insensitive);
    }

    [Fact]
    public void FiveFieldRows_AreClassifiedByCutoverMarkerAndSequence() {
        var zero = new ProjectionDeliveryState(
            0, 0, "tenant-a", "sales", "order-42", null, 0, null, null, null, 0, null,
            ProjectionDeliveryMigrationProvenance.None,
            DateTimeOffset.UtcNow);
        var nonZero = zero with { LastDeliveredSequence = 7 };

        ProjectionDeliveryStateClassifier.Classify(zero, protocolV2Active: false)
            .ShouldBe(ProjectionDeliveryStateClassification.LegacyZero);
        ProjectionDeliveryStateClassifier.Classify(nonZero, protocolV2Active: false)
            .ShouldBe(ProjectionDeliveryStateClassification.LegacyNonZero);
        ProjectionDeliveryStateClassifier.Classify(nonZero, protocolV2Active: true)
            .ShouldBe(ProjectionDeliveryStateClassification.SchemaRegression);
    }

    [Fact]
    public async Task ProtocolMarker_FirstWriteActivatesV2AndRefusesDowngrade() {
        DaprClient dapr = Substitute.For<DaprClient>();
        dapr.JsonSerializerOptions.Returns(JsonSerializerOptions.Web);
        _ = dapr.GetByteStateAndETagAsync(
                "statestore",
                ProjectionDeliveryStateKeys.WriterProtocol,
                consistencyMode: Arg.Any<ConsistencyMode?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns((ReadOnlyMemory<byte>.Empty, string.Empty));
        _ = dapr.TrySaveByteStateAsync(
                "statestore",
                ProjectionDeliveryStateKeys.WriterProtocol,
                Arg.Any<ReadOnlyMemory<byte>>(),
                string.Empty,
                stateOptions: Arg.Any<StateOptions?>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(true);
        var store = new DaprProjectionDeliveryStateStore(
            dapr,
            Options.Create(new ProjectionOptions()));
        var marker = new ProjectionDeliveryWriterProtocol(
            ProjectionDeliveryWriterProtocol.CurrentSchemaVersion,
            ProjectionDeliveryWriterProtocol.CurrentWriterProtocolVersion,
            "commit-abc",
            new DateTimeOffset(2026, 7, 14, 10, 0, 0, TimeSpan.Zero));

        byte[] originalMarkerBytes = JsonSerializer.SerializeToUtf8Bytes(marker, JsonSerializerOptions.Web);
        using ProjectionDeliveryCostScope cost = ProjectionDeliveryCostDiagnostics.BeginScope();
        (await store.TryActivateWriterProtocolAsync(marker)).ShouldBeTrue();

        _ = await dapr.Received(1).TrySaveByteStateAsync(
            "statestore",
            ProjectionDeliveryStateKeys.WriterProtocol,
            Arg.Is<ReadOnlyMemory<byte>>(value => value.ToArray().SequenceEqual(originalMarkerBytes)),
            string.Empty,
            Arg.Is<StateOptions?>(value => value != null && value.Concurrency == ConcurrencyMode.FirstWrite),
            metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
            cancellationToken: Arg.Any<CancellationToken>());
        cost[ProjectionDeliveryCost.WriterActivationAttempts].ShouldBe(1);
        cost[ProjectionDeliveryCost.MarkerReads].ShouldBe(1);
        cost[ProjectionDeliveryCost.MarkerReadBytes].ShouldBe(0);
        cost[ProjectionDeliveryCost.StateRpcAttempts].ShouldBe(2);
        cost[ProjectionDeliveryCost.MutationAttempts].ShouldBe(1);
        cost[ProjectionDeliveryCost.MutationBytes].ShouldBe(originalMarkerBytes.Length);
        cost[ProjectionDeliveryCost.MetadataSerializationBytes].ShouldBe(originalMarkerBytes.Length);
        Should.Throw<ArgumentException>(() => new ProjectionDeliveryWriterProtocol(1, 1, "old", DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task ReconciliationSave_UsesOneFirstWriteTransactionForRowAndOperatorEvidence() {
        DaprClient dapr = Substitute.For<DaprClient>();
        IReadOnlyList<StateTransactionRequest>? captured = null;
        _ = dapr.ExecuteStateTransactionAsync(
                "statestore",
                Arg.Any<IReadOnlyList<StateTransactionRequest>>(),
                metadata: Arg.Any<IReadOnlyDictionary<string, string>>(),
                cancellationToken: Arg.Any<CancellationToken>())
            .Returns(call => {
                captured = call.ArgAt<IReadOnlyList<StateTransactionRequest>>(1);
                return Task.CompletedTask;
            });
        var store = new DaprProjectionDeliveryStateStore(
            dapr,
            Options.Create(new ProjectionOptions()));
        DateTimeOffset now = new(2026, 7, 14, 10, 0, 0, TimeSpan.Zero);
        ProjectionDeliveryState state = ProjectionDeliveryState.CreateEmpty(
            Identity,
            "order-detail",
            "v1:initial",
            now);
        var work = new ProjectionDeliveryReconciliationWork(
            Identity.TenantId,
            Identity.Domain,
            Identity.AggregateId,
            "order-detail",
            "delivery_reconciled",
            0,
            ProjectionDeliveryState.CurrentSchemaVersion,
            "operator-7",
            now);

        bool result = await store.TrySaveWithReconciliationAsync(
            Identity,
            "order-detail",
            state,
            work,
            "etag-7");

        result.ShouldBeTrue();
        captured.ShouldNotBeNull();
        captured.Count.ShouldBe(2);
        StateTransactionRequest row = captured[0];
        row.Key.ShouldBe(ProjectionDeliveryStateKeys.GetStateKey(Identity, "order-detail"));
        row.OperationType.ShouldBe(StateOperationType.Upsert);
        row.ETag.ShouldBe("etag-7");
        row.Options.ShouldNotBeNull();
        row.Options.Concurrency.ShouldBe(ConcurrencyMode.FirstWrite);
        ProjectionDeliveryState persistedState = JsonSerializer.Deserialize<ProjectionDeliveryState>(row.Value!)
            .ShouldNotBeNull();
        persistedState.SchemaVersion.ShouldBe(state.SchemaVersion);
        persistedState.ProjectionName.ShouldBe(state.ProjectionName);
        persistedState.LastDeliveredSequence.ShouldBe(state.LastDeliveredSequence);
        persistedState.CompletedPrefixFingerprint.ShouldBe(state.CompletedPrefixFingerprint);
        persistedState.CompletedReceipts.ShouldBeEmpty();
        StateTransactionRequest evidence = captured[1];
        evidence.Key.ShouldBe(ProjectionDeliveryStateKeys.GetReconciliationKey(Identity, "order-detail"));
        evidence.OperationType.ShouldBe(StateOperationType.Upsert);
        evidence.ETag.ShouldBeNull();
        JsonSerializer.Deserialize<ProjectionDeliveryReconciliationWork>(evidence.Value!).ShouldBe(work);
    }
}
