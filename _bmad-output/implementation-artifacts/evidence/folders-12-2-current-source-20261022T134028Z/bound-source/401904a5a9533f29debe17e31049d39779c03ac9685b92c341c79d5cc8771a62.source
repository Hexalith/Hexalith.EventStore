using Hexalith.EventStore.Server.Commands;
using Hexalith.EventStore.Testing.Fakes;

using Shouldly;

namespace Hexalith.EventStore.Testing.Tests.Fakes;

public class InMemoryCommandCorrelationIndexTests
{
    [Fact]
    public async Task AddAsync_TwoMessagesForCorrelation_ResolvesAmbiguous()
    {
        var index = new InMemoryCommandCorrelationIndex();

        _ = await index.AddAsync("tenant-a", "corr-1", "message-1");
        _ = await index.AddAsync("tenant-a", "corr-1", "message-2");

        CommandCorrelationResolution result = await index.ResolveAsync("tenant-a", "corr-1");
        result.Outcome.ShouldBe(CommandCorrelationResolutionOutcome.Ambiguous);
    }

    [Fact]
    public async Task AddAsync_AtCapacity_MarksOverflowWithoutEviction()
    {
        var index = new InMemoryCommandCorrelationIndex { Capacity = 1 };
        _ = await index.AddAsync("tenant-a", "corr-1", "message-1");

        CommandCorrelationIndexAddOutcome outcome = await index.AddAsync(
            "tenant-a",
            "corr-1",
            "message-2");

        outcome.ShouldBe(CommandCorrelationIndexAddOutcome.Overflow);
        CommandCorrelationIndexRecord record = index.GetAllRecords()["tenant-a:corr-1:command-index"];
        record.Overflowed.ShouldBeTrue();
        record.Entries.Select(entry => entry.MessageId).ShouldBe(["message-1"]);
    }

    [Fact]
    public async Task AddAsync_ConflictsExceedRetries_ReturnsRetryExhaustedWithoutPrimaryImpact()
    {
        var index = new InMemoryCommandCorrelationIndex
        {
            ConflictsRemaining = 4,
            MaxConcurrencyRetries = 3,
        };

        CommandCorrelationIndexAddOutcome outcome = await index.AddAsync(
            "tenant-a",
            "corr-1",
            "message-1");

        outcome.ShouldBe(CommandCorrelationIndexAddOutcome.RetryExhausted);
        index.GetAllRecords().ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_PrunesExpiredEntries()
    {
        var clock = new AdjustableTimeProvider(new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.Zero));
        var index = new InMemoryCommandCorrelationIndex(clock) { TtlSeconds = 60 };
        _ = await index.AddAsync("tenant-a", "corr-1", "message-1");
        clock.Advance(TimeSpan.FromMinutes(2));

        CommandCorrelationResolution result = await index.ResolveAsync("tenant-a", "corr-1");

        result.Outcome.ShouldBe(CommandCorrelationResolutionOutcome.NotFound);
        index.GetAllRecords()["tenant-a:corr-1:command-index"].Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_ExpiredOverflowMarker_RestoresUnambiguousNotFound()
    {
        var clock = new AdjustableTimeProvider(new DateTimeOffset(2026, 7, 12, 10, 0, 0, TimeSpan.Zero));
        var index = new InMemoryCommandCorrelationIndex(clock) { Capacity = 1, TtlSeconds = 60 };
        _ = await index.AddAsync("tenant-a", "corr-1", "message-1");
        _ = await index.AddAsync("tenant-a", "corr-1", "message-2");
        clock.Advance(TimeSpan.FromMinutes(2));

        CommandCorrelationResolution result = await index.ResolveAsync("tenant-a", "corr-1");

        result.Outcome.ShouldBe(CommandCorrelationResolutionOutcome.NotFound);
        CommandCorrelationIndexRecord stored = index.GetAllRecords()["tenant-a:corr-1:command-index"];
        stored.Overflowed.ShouldBeFalse();
        stored.OverflowExpiresAt.ShouldBeNull();
    }
}
