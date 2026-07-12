using Dapr.Client;

using Hexalith.EventStore.Server.Commands;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Commands;

public class DaprCommandCorrelationIndexTests
{
    private readonly DaprClient _daprClient = Substitute.For<DaprClient>();

    [Fact]
    public async Task AddAsync_NewEntry_UsesTenantScopedEtagProtectedIndex()
    {
        _ = _daprClient.GetStateAndETagAsync<CommandCorrelationIndexRecord>(
            "statestore",
            "tenant-a:corr-123:command-index",
            Arg.Any<ConsistencyMode?>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns((default(CommandCorrelationIndexRecord)!, string.Empty));
        _ = _daprClient.TrySaveStateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CommandCorrelationIndexRecord>(),
            Arg.Any<string>(),
            Arg.Any<StateOptions>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(true);
        var index = new DaprCommandCorrelationIndex(
            _daprClient,
            Options.Create(new CommandStatusOptions()),
            Options.Create(new CommandCorrelationIndexOptions()),
            NullLogger<DaprCommandCorrelationIndex>.Instance);

        CommandCorrelationIndexAddOutcome outcome = await index.AddAsync(
            "tenant-a",
            "corr-123",
            "message-123",
            CancellationToken.None);

        outcome.ShouldBe(CommandCorrelationIndexAddOutcome.Added);
        _ = await _daprClient.Received(1).TrySaveStateAsync(
            "statestore",
            "tenant-a:corr-123:command-index",
            Arg.Is<CommandCorrelationIndexRecord>(record =>
                record.Entries.Count == 1
                && record.Entries[0].MessageId == "message-123"
                && !record.Overflowed),
            string.Empty,
            Arg.Is<StateOptions>(stateOptions => stateOptions.Concurrency == ConcurrencyMode.FirstWrite),
            Arg.Is<IReadOnlyDictionary<string, string>>(metadata => metadata["ttlInSeconds"] == "86400"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_MultipleLiveMessages_ReturnsAmbiguous()
    {
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        var record = new CommandCorrelationIndexRecord(
            [
                new CommandCorrelationIndexEntry("message-1", expiresAt),
                new CommandCorrelationIndexEntry("message-2", expiresAt),
            ],
            Overflowed: false);
        _ = _daprClient.GetStateAndETagAsync<CommandCorrelationIndexRecord>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ConsistencyMode?>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns((record, "etag-1"));
        var index = new DaprCommandCorrelationIndex(
            _daprClient,
            Options.Create(new CommandStatusOptions()),
            Options.Create(new CommandCorrelationIndexOptions()),
            NullLogger<DaprCommandCorrelationIndex>.Instance);

        CommandCorrelationResolution result = await index.ResolveAsync(
            "tenant-a",
            "corr-123",
            CancellationToken.None);

        result.Outcome.ShouldBe(CommandCorrelationResolutionOutcome.Ambiguous);
        result.MessageId.ShouldBeNull();
    }

    [Fact]
    public async Task AddAsync_AtCapacity_PersistsExpiringOverflowMarkerWithoutEviction()
    {
        DateTimeOffset now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now);
        var stored = new CommandCorrelationIndexRecord(
            [new CommandCorrelationIndexEntry("message-1", now.AddHours(1))],
            Overflowed: false);
        _ = _daprClient.GetStateAndETagAsync<CommandCorrelationIndexRecord>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ConsistencyMode?>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns((stored, "etag-1"));
        _ = _daprClient.TrySaveStateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CommandCorrelationIndexRecord>(),
            Arg.Any<string>(),
            Arg.Any<StateOptions>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(true);
        var index = new DaprCommandCorrelationIndex(
            _daprClient,
            Options.Create(new CommandStatusOptions { TtlSeconds = 60 }),
            Options.Create(new CommandCorrelationIndexOptions { Capacity = 1 }),
            NullLogger<DaprCommandCorrelationIndex>.Instance,
            clock);

        CommandCorrelationIndexAddOutcome outcome = await index.AddAsync(
            "tenant-a",
            "corr-123",
            "message-2");

        outcome.ShouldBe(CommandCorrelationIndexAddOutcome.Overflow);
        _ = await _daprClient.Received(1).TrySaveStateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<CommandCorrelationIndexRecord>(record =>
                record.Overflowed
                && record.OverflowExpiresAt == now.AddSeconds(60)
                && record.Entries.Count == 1
                && record.Entries[0].MessageId == "message-1"),
            "etag-1",
            Arg.Any<StateOptions>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_EtagConflictsExhaustConfiguredRetries_ReturnsRetryExhausted()
    {
        _ = _daprClient.GetStateAndETagAsync<CommandCorrelationIndexRecord>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ConsistencyMode?>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns((default(CommandCorrelationIndexRecord)!, string.Empty));
        _ = _daprClient.TrySaveStateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CommandCorrelationIndexRecord>(),
            Arg.Any<string>(),
            Arg.Any<StateOptions>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(false);
        var index = new DaprCommandCorrelationIndex(
            _daprClient,
            Options.Create(new CommandStatusOptions()),
            Options.Create(new CommandCorrelationIndexOptions { MaxConcurrencyRetries = 3 }),
            NullLogger<DaprCommandCorrelationIndex>.Instance);

        CommandCorrelationIndexAddOutcome outcome = await index.AddAsync(
            "tenant-a",
            "corr-123",
            "message-1");

        outcome.ShouldBe(CommandCorrelationIndexAddOutcome.RetryExhausted);
        _ = await _daprClient.Received(4).TrySaveStateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CommandCorrelationIndexRecord>(),
            Arg.Any<string>(),
            Arg.Any<StateOptions>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_ExpiredEntriesAndOverflowMarker_PrunesToNotFound()
    {
        DateTimeOffset now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        var stored = new CommandCorrelationIndexRecord(
            [new CommandCorrelationIndexEntry("message-1", now.AddSeconds(-1))],
            Overflowed: true,
            OverflowExpiresAt: now.AddSeconds(-1));
        _ = _daprClient.GetStateAndETagAsync<CommandCorrelationIndexRecord>(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<ConsistencyMode?>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns((stored, "etag-1"));
        _ = _daprClient.TrySaveStateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CommandCorrelationIndexRecord>(),
            Arg.Any<string>(),
            Arg.Any<StateOptions>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(true);
        var index = new DaprCommandCorrelationIndex(
            _daprClient,
            Options.Create(new CommandStatusOptions()),
            Options.Create(new CommandCorrelationIndexOptions()),
            NullLogger<DaprCommandCorrelationIndex>.Instance,
            new FakeTimeProvider(now));

        CommandCorrelationResolution result = await index.ResolveAsync("tenant-a", "corr-123");

        result.Outcome.ShouldBe(CommandCorrelationResolutionOutcome.NotFound);
        _ = await _daprClient.Received(1).TrySaveStateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<CommandCorrelationIndexRecord>(record =>
                !record.Overflowed
                && record.OverflowExpiresAt == null
                && record.Entries.Count == 0),
            "etag-1",
            Arg.Any<StateOptions>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>());
    }
}
