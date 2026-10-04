using Hexalith.EventStore.Contracts.Events;

using Shouldly;

namespace Hexalith.EventStore.Contracts.Tests.Events;

public sealed class AuthenticatedRawEventPageTests {
    [Fact]
    public void Constructor_CopiesProofAndValidatesContiguousAddressedPage() {
        byte[] proof = [1, 2, 3];
        var events = new[] {
            CreateEvent(4),
            CreateEvent(5),
        };

        var page = new AuthenticatedRawEventPage(
            "tenant",
            "domain",
            "aggregate",
            "id",
            4,
            5,
            "etag",
            "namespace",
            events,
            proof);

        proof[0] = 9;
        page.ReadbackProof.ShouldBe([1, 2, 3]);
        page.Events.Select(static item => item.SequenceNumber).ShouldBe([4L, 5L]);
    }

    [Fact]
    public void Constructor_RejectsSequenceGapAndProofOverLimit() {
        Should.Throw<ArgumentException>(() => new AuthenticatedRawEventPage(
            "tenant", "domain", "aggregate", "id", 4, 6, "etag", "namespace",
            [CreateEvent(4), CreateEvent(6)], [1]));

        Should.Throw<ArgumentOutOfRangeException>(() => new AuthenticatedRawEventPage(
            "tenant", "domain", "aggregate", "id", 4, 4, "etag", "namespace",
            [CreateEvent(4)], new byte[(1024 * 1024) + 1]));
    }

    [Fact]
    public void Constructor_RejectsEmptyPageWhenActorHeadHasUnreadEvents() {
        Should.Throw<ArgumentException>(() => new AuthenticatedRawEventPage(
            "tenant", "domain", "aggregate", "id", 4, 5, "etag", "namespace", [], [1]));

        var emptyTail = new AuthenticatedRawEventPage(
            "tenant", "domain", "aggregate", "id", 6, 5, "etag", "namespace", [], [1]);
        emptyTail.Events.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_CopiesRawAndSidecarPayloads() {
        byte[] rawBytes = [1, 2];
        byte[] evidenceBytes = [3, 4];
        var item = new AuthenticatedRawEvent(
            "key-4", 4, new TestPayload(rawBytes), new TestPayload(evidenceBytes), null, null, null, null);
        var page = new AuthenticatedRawEventPage(
            "tenant", "domain", "aggregate", "id", 4, 4, "etag", "namespace", [item], [1]);

        rawBytes.AsSpan().Fill(9);
        evidenceBytes.AsSpan().Fill(8);
        byte[] copiedRaw = new byte[2];
        byte[] copiedEvidence = new byte[2];
        page.Events[0].RawEnvelope.CopyTo(0, copiedRaw);
        page.Events[0].EncodingEvidence!.CopyTo(0, copiedEvidence);

        copiedRaw.ShouldBe([1, 2]);
        copiedEvidence.ShouldBe([3, 4]);
    }

    [Fact]
    public void Constructor_ChargesAllSidecarsAgainstRawPageBudget() {
        var oversizedEvidence = new AuthenticatedRawEvent(
            "key-4", 4, new SizedPayload(1), new SizedPayload(128 * 1024 * 1024), null, null, null, null);

        Should.Throw<ArgumentOutOfRangeException>(() => new AuthenticatedRawEventPage(
            "tenant", "domain", "aggregate", "id", 4, 4, "etag", "namespace", [oversizedEvidence], [1]));
        Should.Throw<ArgumentOutOfRangeException>(() => AuthenticatedRawEventPage.ValidateReadablePayloadBytes(64L * 1024 * 1024 + 1));
        Should.NotThrow(() => AuthenticatedRawEventPage.ValidateReadablePayloadBytes(64L * 1024 * 1024));
    }

    private static AuthenticatedRawEvent CreateEvent(long sequence)
        => new($"key-{sequence}", sequence, new TestPayload([1, 2]), null, null, null, null, null);

    private sealed class TestPayload(byte[] bytes) : IReadOnlyPayload {
        public int Length => bytes.Length;

        public void CopyTo(int sourceOffset, Span<byte> destination)
            => bytes.AsSpan(sourceOffset, destination.Length).CopyTo(destination);
    }

    private sealed class SizedPayload(int length) : IReadOnlyPayload {
        public int Length => length;

        public void CopyTo(int sourceOffset, Span<byte> destination)
            => throw new InvalidOperationException("An over-budget payload must be rejected before copying.");
    }
}
