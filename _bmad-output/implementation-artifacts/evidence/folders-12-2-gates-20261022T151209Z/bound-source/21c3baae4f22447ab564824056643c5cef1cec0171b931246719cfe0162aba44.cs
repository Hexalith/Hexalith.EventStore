
using Hexalith.EventStore.Server.Queries;
using Hexalith.EventStore.Server.Tests.TestUtilities;

using Microsoft.Extensions.Logging;

using Shouldly;

namespace Hexalith.EventStore.Server.Tests.Queries;

/// <summary>
/// Covers the startup-visibility task for the opt-in safe-denial boundary (Story 6.1-P2): a
/// typo'd domain/query type that never opts into safe-denial protection must be operator-visible
/// via a startup log line rather than a silent configuration gap.
/// </summary>
public class SafeDenialQueryRouteStartupLoggerTests {
    [Fact]
    public async Task StartAsync_RegisteredRoutes_LogsEachRouteAndSummaryCount() {
        var entries = new List<LogEntry>();
        var registry = new SafeDenialQueryRouteRegistry([("orders", "list-orders"), ("parties", "list-parties")]);
        var sut = new SafeDenialQueryRouteStartupLogger(registry, new TestLogger<SafeDenialQueryRouteStartupLogger>(entries));

        await sut.StartAsync(CancellationToken.None);

        entries.ShouldContain(e =>
            e.EventId.Id == 1221
            && e.Level == LogLevel.Information
            && e.Message.Contains("orders", StringComparison.Ordinal)
            && e.Message.Contains("list-orders", StringComparison.Ordinal));
        entries.ShouldContain(e =>
            e.EventId.Id == 1221
            && e.Level == LogLevel.Information
            && e.Message.Contains("parties", StringComparison.Ordinal)
            && e.Message.Contains("list-parties", StringComparison.Ordinal));
        entries.ShouldContain(e =>
            e.EventId.Id == 1222
            && e.Level == LogLevel.Information
            && e.Message.Contains('2', StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartAsync_NoRegisteredRoutes_LogsZeroSummaryCountAndNoRouteLines() {
        var entries = new List<LogEntry>();
        var registry = new SafeDenialQueryRouteRegistry([]);
        var sut = new SafeDenialQueryRouteStartupLogger(registry, new TestLogger<SafeDenialQueryRouteStartupLogger>(entries));

        await sut.StartAsync(CancellationToken.None);

        entries.ShouldNotContain(e => e.EventId.Id == 1221);
        entries.ShouldContain(e => e.EventId.Id == 1222 && e.Message.Contains('0', StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartAsync_NonRegistryPolicy_DoesNotThrowAndLogsNothing() {
        // A custom ISafeDenialQueryRoutePolicy implementation has no enumerable route list to
        // log -- the startup logger must not assume the concrete SafeDenialQueryRouteRegistry
        // type and must fail safe (no-op) rather than throw.
        var entries = new List<LogEntry>();
        var sut = new SafeDenialQueryRouteStartupLogger(
            new AlwaysOptedInPolicy(),
            new TestLogger<SafeDenialQueryRouteStartupLogger>(entries));

        await sut.StartAsync(CancellationToken.None);

        entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task StopAsync_CompletesSuccessfully() {
        var sut = new SafeDenialQueryRouteStartupLogger(
            new SafeDenialQueryRouteRegistry([]),
            new TestLogger<SafeDenialQueryRouteStartupLogger>([]));

        await sut.StopAsync(CancellationToken.None);
    }

    private sealed class AlwaysOptedInPolicy : ISafeDenialQueryRoutePolicy {
        public bool IsOptedIn(string domain, string queryType) => true;
    }
}
