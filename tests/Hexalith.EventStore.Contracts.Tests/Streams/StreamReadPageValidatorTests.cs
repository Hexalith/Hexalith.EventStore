using Hexalith.EventStore.Contracts.Streams;

namespace Hexalith.EventStore.Contracts.Tests.Streams;

/// <summary>Protects strict, exclusive stream-page continuation for all downstream recovery readers.</summary>
public sealed class StreamReadPageValidatorTests
{
    /// <summary>Two pages advance from the last returned sequence without skipping the next event.</summary>
    [Fact]
    public void TruncatedPageReturnsLastPositionAsExclusiveCursor()
    {
        StreamReadRequest firstRequest = new("tenant-a", "work", "item-1");
        StreamReadPage first = Page(firstRequest, [Event(1)], isTruncated: true, latestSequence: 2);

        long next = StreamReadPageValidator.ValidateAndGetNextSequence(firstRequest, first);

        next.ShouldBe(1);
        StreamReadRequest secondRequest = firstRequest with { FromSequence = next };
        StreamReadPage second = Page(secondRequest, [Event(2)], isTruncated: false, latestSequence: 2);
        StreamReadPageValidator.ValidateAndGetNextSequence(secondRequest, second).ShouldBe(2);
    }

    /// <summary>A forged tenant, domain, or aggregate cannot enter a recovery fold.</summary>
    [Theory]
    [InlineData("tenant-b", "work", "item-1")]
    [InlineData("tenant-a", "other", "item-1")]
    [InlineData("tenant-a", "work", "item-2")]
    public void ForeignIdentityFailsClosed(string tenant, string domain, string aggregateId)
    {
        StreamReadRequest request = new("tenant-a", "work", "item-1");
        StreamReadPage page = Page(request, [Event(1)], false, 1) with
        {
            Tenant = tenant,
            Domain = domain,
            AggregateId = aggregateId,
        };

        _ = Should.Throw<InvalidOperationException>(() => StreamReadPageValidator.ValidateAndGetNextSequence(request, page));
    }

    /// <summary>Duplicate, reversed, and skipped-cursor positions are rejected before checkpointing.</summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    public void NonIncreasingPositionsFailClosed(long first, long second)
    {
        StreamReadRequest request = new("tenant-a", "work", "item-1");
        StreamReadPage page = Page(request, [Event(first), Event(second)], false, 2);

        _ = Should.Throw<InvalidOperationException>(() => StreamReadPageValidator.ValidateAndGetNextSequence(request, page));
    }

    /// <summary>A truncated empty page cannot supply a safe continuation.</summary>
    [Fact]
    public void TruncatedEmptyPageFailsClosed()
    {
        StreamReadRequest request = new("tenant-a", "work", "item-1");
        StreamReadPage page = Page(request, [], true, 10);

        _ = Should.Throw<InvalidOperationException>(() => StreamReadPageValidator.ValidateAndGetNextSequence(request, page));
    }

    /// <summary>An empty read beyond the latest event is a valid completed page.</summary>
    [Fact]
    public void CompletedEmptyPageBeyondLatestSequenceKeepsCursor()
    {
        StreamReadRequest request = new("tenant-a", "work", "item-1", FromSequence: 10);
        StreamReadPage page = Page(request, [], false, 3);

        StreamReadPageValidator.ValidateAndGetNextSequence(request, page).ShouldBe(10);
    }

    /// <summary>The page count and last-position metadata must reflect the supplied evidence.</summary>
    [Fact]
    public void ConflictingPageMetadataFailsClosed()
    {
        StreamReadRequest request = new("tenant-a", "work", "item-1");
        StreamReadPage page = Page(request, [Event(1)], false, 1) with
        {
            Metadata = new StreamReadMetadata(0, null, 2, 2, 1, false, null),
        };

        _ = Should.Throw<InvalidOperationException>(() => StreamReadPageValidator.ValidateAndGetNextSequence(request, page));
    }

    private static StreamReadPage Page(
        StreamReadRequest request,
        IReadOnlyList<StreamReadEvent> events,
        bool isTruncated,
        long latestSequence)
    {
        return new StreamReadPage(
            request.Tenant,
            request.Domain,
            request.AggregateId,
            events,
            new StreamReadMetadata(
                request.FromSequence,
                request.ToSequence,
                events.Count == 0 ? null : events[^1].SequenceNumber,
                latestSequence,
                events.Count,
                isTruncated,
                null));
    }

    private static StreamReadEvent Event(long sequence)
    {
        return new StreamReadEvent(
            sequence,
            "ExampleEvent",
            [1],
            "json",
            1,
            $"message-{sequence}",
            $"correlation-{sequence}",
            null,
            DateTimeOffset.UnixEpoch,
            null);
    }
}
