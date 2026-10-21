using System.Text.Json;

using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Replay;

namespace Hexalith.EventStore.Contracts.Tests.Replay;

public sealed class PagedReplayContractTests {
    [Fact]
    public void LegacyReplayWire_OmitsAbsentPagedMembers() {
        var request = new AggregateReconstructionRequest("tenant", "domain", "type", "aggregate", 0, [], false, null);
        var result = AggregateReconstructionResult.Succeeded("{}", 0);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        JsonSerializer.Serialize(request, options).ShouldNotContain("pagedContext");
        JsonSerializer.Serialize(result, options).ShouldNotContain("pagedProgress");
    }

    [Fact]
    public void PagedContext_SnapshotsMutableInputsAndReturnsCopies() {
        byte[] proof = [1];
        byte[] handle = [2];
        byte[] continuation = [3];
        byte[] stored = new byte[32];
        byte[] effective = new byte[32];
        var views = new List<VerifiedEffectiveEventView>();
        var context = new PagedContext(
            views, proof, "tenant", "domain", "type", "aggregate", 0, 0, 1, 0, 0,
            "operation", handle, continuation, stored, effective, true, new UnusedSession());

        proof[0] = 9;
        handle[0] = 9;
        continuation[0] = 9;
        stored[0] = 9;
        effective[0] = 9;
        context.EventEvolutionProof.ShouldBe([1]);
        context.ScratchHandleToken.ShouldBe([2]);
        context.ContinuationToken.ShouldBe([3]);
        context.StoredAccumulator[0].ShouldBe((byte)0);
        context.EffectiveAccumulator[0].ShouldBe((byte)0);
        views.Add(null!);
        context.VerifiedPageViews.ShouldBeEmpty();

        context.EventEvolutionProof[0] = 8;
        context.ScratchHandleToken[0] = 8;
        context.StoredAccumulator[0] = 8;
        context.EventEvolutionProof.ShouldBe([1]);
        context.ScratchHandleToken.ShouldBe([2]);
        context.StoredAccumulator[0].ShouldBe((byte)0);
    }

    [Fact]
    public void PagedProgress_SnapshotsHandlesAndRetainsAdditiveStatusValue() {
        byte[] handle = [1];
        byte[] stored = new byte[32];
        byte[] effective = new byte[32];
        var progress = new PagedProgress(handle, stored, effective, 1, 1, 1, false);
        handle[0] = 9;
        stored[0] = 9;
        effective[0] = 9;

        progress.SuccessorHandleToken.ShouldBe([1]);
        progress.StoredAccumulator[0].ShouldBe((byte)0);
        progress.EffectiveAccumulator[0].ShouldBe((byte)0);
        ((int)AggregateReconstructionStatus.InProgress).ShouldBe(3);
        var result = new AggregateReconstructionResult(
            AggregateReconstructionStatus.InProgress, null, 1, null, null,
            AggregateReconstructionErrorCategory.None, null, null) { PagedProgress = progress };
        string json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        json.ShouldContain("\"pagedProgress\"");
        json.ShouldContain("\"status\":3");
    }

    private sealed class UnusedSession : IPagedReplayStateSession {
        public ValueTask<ReadOnlyMemory<byte>> ReadPriorAsync(byte[] handle, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public ValueTask<byte[]> WriteSuccessorAsync(
            byte[] priorHandle, ReadOnlyMemory<byte> canonicalState, string serializerId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public void AppendTimelineEntry(long sequence, ReadOnlySpan<byte> canonicalPostApplyState)
            => throw new NotSupportedException();
    }
}
