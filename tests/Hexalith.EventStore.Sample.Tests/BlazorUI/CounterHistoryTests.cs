extern alias BlazorUI;

using System.Text.Json;

using Hexalith.EventStore.Client.Gateway;

using Shouldly;

using CounterHistory = BlazorUI::Hexalith.EventStore.Sample.BlazorUI.Services.CounterHistory;

namespace Hexalith.EventStore.Sample.Tests.BlazorUI;

/// <summary>Exercises history updates for successful, unchanged and unusable refreshes.</summary>
public sealed class CounterHistoryTests
{
    [Fact]
    public void Confirmed304RetainsTheOriginalRowAndNextProjectionAddsARow()
    {
        var history = new CounterHistory();
        DateTime first = new(2026, 11, 25, 6, 0, 0, DateTimeKind.Local);
        using var original = JsonDocument.Parse("""{"count":3}""");
        using var updated = JsonDocument.Parse("""{"count":4}""");

        history.TryApply(new("query", original.RootElement, false, "\"one\""), first).ShouldBeTrue();
        history.TryApply(new(null, null, true, "\"one\""), first.AddSeconds(1)).ShouldBeTrue();
        history.Entries.ShouldHaveSingleItem().Timestamp.ShouldBe(first);
        history.ETag.ShouldBe("\"one\"");

        history.TryApply(new("query", updated.RootElement, false, "\"two\""), first.AddSeconds(2)).ShouldBeTrue();
        history.Entries.Select(row => row.Count).ShouldBe([4, 3]);
        history.ETag.ShouldBe("\"two\"");
    }

    [Fact]
    public void Unconfirmed304DoesNotAddARowOrInventAValidator()
    {
        var history = new CounterHistory();

        history.TryApply(new EventStoreQueryResult(null, null, true, "\"unconfirmed\""), DateTime.UnixEpoch)
            .ShouldBeFalse();
        history.Entries.ShouldBeEmpty();
        history.ETag.ShouldBeNull();
    }

    [Fact]
    public void RepeatedMissingProjectionRetainsTheInitialEmptyRow()
    {
        var history = new CounterHistory();

        history.RecordMissingProjection(DateTime.UnixEpoch);
        history.RecordMissingProjection(DateTime.UnixEpoch.AddSeconds(1));

        history.Entries.ShouldHaveSingleItem().Timestamp.ShouldBe(DateTime.UnixEpoch);
        history.ETag.ShouldBeNull();
    }
}
