using System.Text.Json;

using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Contracts.Tests.Streams;

public class StreamReadContractTests {
    [Fact]
    public void StreamReadRequestJsonRoundTripUsesCamelCasePublicShape() {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        var request = new StreamReadRequest(
            Tenant: "tenant-a",
            Domain: "party",
            AggregateId: "party-1",
            FromSequence: 10,
            ToSequence: 20,
            Checkpoint: new ProjectionRebuildCheckpoint(
                Tenant: "tenant-a",
                Domain: "party",
                ProjectionName: "party-summary",
                AggregateId: "party-1",
                OperationId: "operation-1",
                LastAppliedSequence: 9,
                Status: ProjectionRebuildStatus.Running,
                UpdatedAt: DateTimeOffset.Parse("2026-05-15T07:00:00Z"),
                FailureReasonCode: null),
            ContinuationToken: new ReplayContinuationToken("opaque-token"),
            PageSize: 50,
            ProjectionName: "party-summary");

        string json = JsonSerializer.Serialize(request, options);
        StreamReadRequest? roundTripped = JsonSerializer.Deserialize<StreamReadRequest>(json, options);

        json.ShouldContain("\"tenant\":\"tenant-a\"");
        json.ShouldContain("\"aggregateId\":\"party-1\"");
        json.ShouldContain("\"fromSequence\":10");
        json.ShouldContain("\"continuationToken\":{\"value\":\"opaque-token\"}");
        _ = roundTripped.ShouldNotBeNull();
        _ = roundTripped.Checkpoint.ShouldNotBeNull();
        roundTripped.Checkpoint.Status.ShouldBe(ProjectionRebuildStatus.Running);
        _ = roundTripped.ContinuationToken.ShouldNotBeNull();
        roundTripped.ContinuationToken.Value.ShouldBe("opaque-token");
    }

    [Fact]
    public void StreamReadPageCarriesOrderedPageMetadataAndNoStateKeys() {
        var page = new StreamReadPage(
            Tenant: "tenant-a",
            Domain: "party",
            AggregateId: "party-1",
            Events: [
                new StreamReadEvent(
                    SequenceNumber: 11,
                    EventTypeName: "PartyRenamed",
                    Payload: [1, 2, 3],
                    SerializationFormat: "json",
                    MetadataVersion: 1,
                    MessageId: "01HZX",
                    CorrelationId: "corr-1",
                    CausationId: "cause-1",
                    Timestamp: DateTimeOffset.Parse("2026-05-15T07:01:00Z"),
                    UserId: "user-1"),
            ],
            Metadata: new StreamReadMetadata(
                FromSequence: 10,
                ToSequence: 20,
                LastSequenceReturned: 11,
                LatestSequence: 13,
                EventCount: 1,
                IsTruncated: true,
                NextContinuationToken: new ReplayContinuationToken("next-token")));

        page.Metadata.EventCount.ShouldBe(1);
        page.Metadata.IsTruncated.ShouldBeTrue();
        _ = page.Metadata.NextContinuationToken.ShouldNotBeNull();
        page.Events.Single().SequenceNumber.ShouldBe(11);
    }

    [Fact]
    public void StreamReplayReasonCodesExposeStablePublicTaxonomy() {
        StreamReplayReasonCodes.InvalidRange.ShouldBe("invalid-range");
        StreamReplayReasonCodes.MissingRequiredField.ShouldBe("missing-required-field");
        StreamReplayReasonCodes.InvalidAggregateIdentity.ShouldBe("invalid-aggregate-identity");
        StreamReplayReasonCodes.InvalidContinuation.ShouldBe("invalid-continuation");
        StreamReplayReasonCodes.TokenRequestMismatch.ShouldBe("token-request-mismatch");
        StreamReplayReasonCodes.CheckpointConflict.ShouldBe("checkpoint-conflict");
        StreamReplayReasonCodes.CheckpointDrift.ShouldBe("checkpoint-drift");
        StreamReplayReasonCodes.RebuildPaused.ShouldBe("rebuild-paused");
        StreamReplayReasonCodes.RebuildPrefixSafetyLimitExceeded.ShouldBe("rebuild_prefix_safety_limit_exceeded");
        StreamReplayReasonCodes.OperatorPreempted.ShouldBe("operator-preempted");
        StreamReplayReasonCodes.DomainFailure.ShouldBe("domain-failure");
        StreamReplayReasonCodes.PollerRebuildConflict.ShouldBe("poller-rebuild-conflict");
        StreamReplayReasonCodes.InternalError.ShouldBe("internal-error");
    }

    [Fact]
    public void EmptyStreamReadPageSerializesNullLastSequenceReturned() {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        var page = new StreamReadPage(
            "tenant-a",
            "party",
            "party-1",
            [],
            new StreamReadMetadata(0, null, null, 0, 0, false, null));

        string json = JsonSerializer.Serialize(page, options);

        json.ShouldContain("\"lastSequenceReturned\":null");
    }

    [Fact]
    public void NotStartedProjectionRebuildOperationCanRepresentMissingStartTime() {
        var operation = new ProjectionRebuildOperation(
            "operation-1",
            "tenant-a",
            "party",
            "party",
            null,
            ProjectionRebuildStatus.NotStarted,
            Checkpoint: null,
            StartedAt: null,
            CompletedAt: null,
            FailureReasonCode: null);

        operation.StartedAt.ShouldBeNull();
    }
}
