using System.Text.Json;

using Hexalith.EventStore.Contracts.Replay;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Projections;

public sealed class ProjectionRebuildEquivalenceOracleTests {
    private static readonly DateTimeOffset ProjectedAt = new(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Build_CanonicalStream_ProducesDetailIndexVersionAndCheckpoint() {
        ReplayEventEnvelope[] events = BuildEvents(7);

        ProjectionRebuildEquivalenceSnapshot snapshot = ProjectionRebuildEquivalenceOracle.Build(
            "test-tenant",
            "test-domain",
            "agg-001",
            events,
            upToSequence: 7,
            ProjectedAt);

        snapshot.Detail.Id.ShouldBe("agg-001");
        snapshot.Detail.Status.ShouldBe("status-7");
        snapshot.Detail.EventCount.ShouldBe(7);
        snapshot.Detail.ProjectedAt.ShouldBe(ProjectedAt);
        snapshot.Detail.ProjectionVersion.ShouldBe("7");
        snapshot.Index.AggregateIds.ShouldBe(["agg-001"]);
        snapshot.Index.ProjectedAt.ShouldBe(ProjectedAt);
        snapshot.Index.ProjectionVersion.ShouldBe("7");
        snapshot.ProjectionVersion.ShouldBe("7");
        snapshot.Checkpoint.ShouldBe(7);
    }

    [Theory]
    [MemberData(nameof(InvalidSequences))]
    public void Replay_InvalidSequenceShape_ReturnsCanonicalFailure(
        long[] sequences,
        long expectedFailedSequence,
        string expectedMessageFragment) {
        ReplayEventEnvelope[] events = [.. sequences.Select(CreateEvent)];

        AggregateReconstructionResult result = ProjectionRebuildEquivalenceOracle.Replay(
            "test-tenant",
            "test-domain",
            "agg-001",
            events,
            sequences.Max());

        result.Status.ShouldBe(AggregateReconstructionStatus.Failed);
        result.FailedSequenceNumber.ShouldBe(expectedFailedSequence);
        result.Message.ShouldNotBeNull().ShouldContain(expectedMessageFragment);
    }

    [Fact]
    public void Build_RepeatedInvocations_AreSideEffectFree() {
        ReplayEventEnvelope[] events = BuildEvents(5);
        byte[][] originalPayloads = [.. events.Select(static item => (byte[])item.Payload.Clone())];

        ProjectionRebuildEquivalenceSnapshot first = ProjectionRebuildEquivalenceOracle.Build(
            "test-tenant",
            "test-domain",
            "agg-001",
            events,
            upToSequence: 5,
            ProjectedAt);
        ProjectionRebuildEquivalenceSnapshot second = ProjectionRebuildEquivalenceOracle.Build(
            "test-tenant",
            "test-domain",
            "agg-001",
            events,
            upToSequence: 5,
            ProjectedAt);

        second.Detail.ShouldBe(first.Detail);
        second.Index.AggregateIds.ShouldBe(first.Index.AggregateIds);
        second.Index.ProjectedAt.ShouldBe(first.Index.ProjectedAt);
        second.Index.ProjectionVersion.ShouldBe(first.Index.ProjectionVersion);
        second.ProjectionVersion.ShouldBe(first.ProjectionVersion);
        second.Checkpoint.ShouldBe(first.Checkpoint);
        for (int index = 0; index < events.Length; index++) {
            events[index].Payload.ShouldBe(originalPayloads[index]);
        }
    }

    public static TheoryData<long[], long, string> InvalidSequences => new() {
        { [2], 1, "Missing stream sequence 1" },
        { [1, 3], 2, "Missing stream sequence 2" },
        { [1, 1, 2], 1, "Duplicate stream sequence 1" },
    };

    private static ReplayEventEnvelope[] BuildEvents(int count)
        => [.. Enumerable.Range(1, count).Select(sequence => CreateEvent(sequence))];

    private static ReplayEventEnvelope CreateEvent(long sequence) {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new ProjectionStatusChanged("agg-001", $"status-{sequence}"));
        return new ReplayEventEnvelope(
            sequence,
            nameof(ProjectionStatusChanged),
            payload,
            "json",
            1,
            $"01J{sequence:D23}",
            "01J00000000000000000000001",
            "01J00000000000000000000002");
    }
}
