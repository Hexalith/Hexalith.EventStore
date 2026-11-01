using Shouldly;

namespace Hexalith.EventStore.PublishedCompatibility.Tests;

/// <summary>The per-capture index preserves exact interval boundaries and duplicate receipts.</summary>
public sealed class HistoricalTransportIndexTests
{
    [Fact]
    public void IndexKeepsDuplicatesAndBothBoundariesWithoutBorrowingOtherIntervals()
    {
        var began = DateTimeOffset.UnixEpoch; string address = "http://127.0.0.1:1/owned";
        QualificationHttpExchange Row(string url, double start, double end) => new(url, began.AddSeconds(start), began.AddSeconds(end), 200, "request", "response", "input", "output", "GET");
        var first = Row(address, 1, 2); var boundary = Row(address, 0, 3);
        var index = new HistoricalTransportIndex([Row(address, -1, 1), first, Row(address, 2, 4), first, Row("http://127.0.0.1:1/other", 1, 2), boundary]);
        var actual = index.Within(address, began, began.AddSeconds(3)).ToArray();
        actual.Length.ShouldBe(3); actual.Count(row => row == first).ShouldBe(2); actual.ShouldContain(boundary);
        index.Within(address, began.AddSeconds(1), began.AddSeconds(2)).Count().ShouldBe(2);
        index.Within(address, began.AddSeconds(4), began.AddSeconds(5)).ShouldBeEmpty();
    }
}
